using GestorTeclado.Core;

namespace GestorTeclado.UI;

/// <summary>
/// Pestaña Reasignaciones: lista de reglas (entrada a la izquierda, salida a
/// la derecha) y editor con los tres tipos de salida. El editor rechaza
/// entradas duplicadas activas y muestra conflictos detectables.
/// </summary>
public sealed class RemapTab : UserControl
{
    private readonly ApplicationController _app;

    private readonly ListView _list = new()
    {
        View = View.Details,
        FullRowSelect = true,
        Dock = DockStyle.Fill,
        CheckBoxes = true,
        HideSelection = false,
    };

    private readonly Button _addButton = new() { Text = "Nueva regla", AutoSize = true };
    private readonly Button _editButton = new() { Text = "Editar…", AutoSize = true, Enabled = false };
    private readonly Button _deleteButton = new() { Text = "Eliminar", AutoSize = true, Enabled = false };
    private readonly Label _conflictLabel = new() { AutoSize = true, ForeColor = Color.Firebrick };

    public RemapTab(ApplicationController app)
    {
        _app = app;
        BuildLayout();
        _app.RulesChanged += RefreshList;
        Disposed += (_, _) => _app.RulesChanged -= RefreshList;
        RefreshList();
    }

    private void BuildLayout()
    {
        _list.Columns.Add("✔", 32);
        _list.Columns.Add("Entrada", 190);
        _list.Columns.Add("Salida", 260);
        _list.Columns.Add("Nombre", 180);
        _list.Columns.Add("Repetición", 90);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
        buttons.Controls.Add(_addButton);
        buttons.Controls.Add(_editButton);
        buttons.Controls.Add(_deleteButton);

        Controls.Add(_list);
        Controls.Add(buttons);
        Controls.Add(_conflictLabel);

        _addButton.Click += (_, _) => OpenEditor(null);
        _editButton.Click += (_, _) =>
        {
            if (_list.SelectedItems.Count > 0)
                OpenEditor((Guid)_list.SelectedItems[0].Tag!);
        };
        _deleteButton.Click += (_, _) =>
        {
            foreach (ListViewItem item in _list.SelectedItems)
                _app.RemoveRule((Guid)item.Tag!);
        };
        _list.SelectedIndexChanged += (_, _) =>
            _editButton.Enabled = _deleteButton.Enabled = _list.SelectedItems.Count > 0;
        _list.ItemChecked += (_, e) =>
        {
            if (!_list.Focused) return; // ignorar cambios programáticos al refrescar
            var id = (Guid)e.Item.Tag!;
            _app.SetRuleEnabled(id, e.Item.Checked);
        };
        _list.DoubleClick += (_, _) =>
        {
            if (_list.SelectedItems.Count > 0)
                OpenEditor((Guid)_list.SelectedItems[0].Tag!);
        };
    }

    /// <summary>Crea una regla nueva pre-rellenada desde Pruebas ("Crear regla con esta combinación").</summary>
    public void CreateFromCombination(KeyCombination combo) => OpenEditor(null, combo);

    private void OpenEditor(Guid? existingId, KeyCombination? presetInput = null)
    {
        var existing = existingId is Guid id ? _app.Document.Rules.FirstOrDefault(r => r.Id == id) : null;
        using var dlg = new RuleEditorDialog(_app, existing, presetInput);
        if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
            RefreshList();
    }

    private void RefreshList()
    {
        var selected = _list.SelectedItems.Cast<ListViewItem>().Select(i => (Guid)i.Tag!).ToHashSet();
        _list.BeginUpdate();
        _suppressCheckEvents = true;
        _list.Items.Clear();
        foreach (var rule in _app.Document.Rules)
        {
            var item = new ListViewItem("");
            item.SubItems.Add(rule.Input.ToString());
            item.SubItems.Add(rule.Output.Describe());
            item.SubItems.Add(rule.Name ?? "");
            item.SubItems.Add(rule.AllowRepeat ? "sí" : "no");
            item.Tag = rule.Id;
            item.Checked = rule.Enabled;
            if (selected.Contains(rule.Id)) item.Selected = true;
            _list.Items.Add(item);
        }
        _suppressCheckEvents = false;
        _list.EndUpdate();

        var conflicts = _app.DetectConflicts();
        _conflictLabel.Text = conflicts.Count == 0
            ? ""
            : $"Conflictos: {string.Join("; ", conflicts.Select(c => $"{c.A.Input} en “{c.A.Name ?? c.A.Id.ToString()[..8]}” y “{c.B.Name ?? c.B.Id.ToString()[..8]}”"))}";
    }
}
