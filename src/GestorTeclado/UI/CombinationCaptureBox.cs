using GestorTeclado.Core;

namespace GestorTeclado.UI;

/// <summary>
/// Control reutilizable "Capturar combinación": al hacer clic entra en modo
/// captura (KeyPreview en el formulario padre) y muestra la próxima pulsación.
/// También permite selección manual para teclas ausentes del teclado propio.
/// </summary>
public sealed class CombinationCaptureBox : UserControl
{
    private readonly TextBox _display = new();
    private readonly Button _captureButton = new();
    private readonly Label _hint = new();
    private bool _capturing;

    public event Action<KeyCombination>? CombinationCaptured;

    public CombinationCaptureBox()
    {
        Size = new Size(320, 84);
        _display.ReadOnly = true;
        _display.TabStop = false;
        _display.Dock = DockStyle.Top;
        _display.PlaceholderText = "Sin definir";

        _captureButton.Text = "Capturar combinación";
        _captureButton.Dock = DockStyle.Left;
        _captureButton.AutoSize = true;
        _captureButton.Click += (_, _) => ToggleCapture();

        _hint.AutoSize = true;
        _hint.ForeColor = Color.Gray;
        _hint.Text = "";

        var row = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true };
        row.Controls.Add(_captureButton);
        Controls.Add(_display);
        Controls.Add(row);
        Controls.Add(_hint);
    }

    public KeyCombination? Value { get; private set; }

    public void SetValue(KeyCombination? combo)
    {
        Value = combo;
        _display.Text = combo?.ToString() ?? "";
    }

    public void SetManual(VirtualKey key) => SetValue(KeyCombination.FromKey(key));

    private void ToggleCapture()
    {
        _capturing = !_capturing;
        _captureButton.Text = _capturing ? "Capturando… (Esc cancela)" : "Capturar combinación";
        _hint.Text = _capturing
            ? "Pulsa ahora la tecla o combinación. Si es Fn y no llega ningún evento, el teclado no transmite su pulsación."
            : "";
        if (_capturing) FindForm()?.Activate();
    }

    /// <summary>Llamado desde el formulario con KeyPreview=true mientras captura.</summary>
    public bool TryHandleKey(KeyEventArgs e)
    {
        if (!_capturing) return false;
        if (e.KeyCode == Keys.Escape && e.Modifiers == Keys.None)
        {
            ToggleCapture();
            return true;
        }

        // Construir KeyPress a partir de WinForms + estado físico aproximado.
        var mods = new List<VirtualKey>();
        if ((e.Modifiers & Keys.Control) != 0) mods.Add(VirtualKey.ControlLeft);
        if ((e.Modifiers & Keys.Alt) != 0) mods.Add(VirtualKey.AltLeft);
        if ((e.Modifiers & Keys.Shift) != 0) mods.Add(VirtualKey.ShiftLeft);
        var combo = new KeyCombination(Engine.WinKeyMapper.ToVirtualKey(e.KeyCode), mods);

        SetValue(combo);
        _capturing = false;
        _captureButton.Text = "Capturar combinación";
        _hint.Text = "";
        CombinationCaptured?.Invoke(combo);
        e.Handled = e.SuppressKeyPress = true;
        return true;
    }
}
