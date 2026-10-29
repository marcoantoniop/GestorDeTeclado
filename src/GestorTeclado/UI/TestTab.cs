using GestorTeclado.Core;

namespace GestorTeclado.UI;

/// <summary>
/// Pestaña Pruebas. Dos modos seleccionables:
/// - "Probar entrada original": suspende las reasignaciones durante la captura
///   para identificar las teclas reales del teclado.
/// - "Probar resultado": aplica las reglas y permite escribir en un cuadro para
///   comprobar la salida.
/// El historial es temporal: solo se muestra mientras dura una prueba iniciada
/// por el usuario (se limpia al detener).
/// </summary>
public sealed class TestTab : UserControl
{
    private readonly ApplicationController _app;

    private readonly RadioButton _modeOriginal = new()
    { Text = "Probar entrada original", Checked = true, AutoSize = true };
    private readonly RadioButton _modeResult = new()
    { Text = "Probar resultado", AutoSize = true };
    private readonly Button _startButton = new() { Text = "Iniciar prueba", AutoSize = true };
    private readonly Button _stopButton = new() { Text = "Detener", AutoSize = true, Enabled = false };
    private readonly Button _clearButton = new() { Text = "Limpiar", AutoSize = true };
    private readonly Button _createRuleButton = new()
    { Text = "Crear regla con esta combinación", AutoSize = true, Enabled = false };

    private readonly ListView _history = new()
    {
        View = View.Details,
        FullRowSelect = true,
        Dock = DockStyle.Fill,
        HideSelection = false,
    };

    private readonly TextBox _outputBox = new()
    {
        Multiline = true,
        Dock = DockStyle.Bottom,
        Height = 80,
        PlaceholderText = "En “Probar resultado”, escribe aquí para verificar qué envían tus reglas.",
        Visible = false,
    };

    private readonly Label _statusLabel = new() { AutoSize = true, ForeColor = Color.Gray };
    private bool _running;
    private KeyCombination? _lastCombo;

    public event Action<KeyCombination>? CreateRuleRequested;

    public TestTab(ApplicationController app)
    {
        _app = app;
        BuildLayout();
        _app.Engine.KeyObserved += OnKeyObserved;
        Disposed += (_, _) => _app.Engine.KeyObserved -= OnKeyObserved;
    }

    private void BuildLayout()
    {
        var modes = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
        modes.Controls.Add(_modeOriginal);
        modes.Controls.Add(_modeResult);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
        buttons.Controls.Add(_startButton);
        buttons.Controls.Add(_stopButton);
        buttons.Controls.Add(_clearButton);
        buttons.Controls.Add(_createRuleButton);

        _history.Columns.Add("Tecla detectada", 140);
        _history.Columns.Add("Combinación reconocida", 170);
        _history.Columns.Add("Evento", 110);
        _history.Columns.Add("Carácter", 90);
        _history.Columns.Add("VK / Escancode", 130);
        _history.Columns.Add("Nota", 260);

        Controls.Add(_history);
        Controls.Add(_outputBox);
        Controls.Add(buttons);
        Controls.Add(modes);
        Controls.Add(_statusLabel);

        _startButton.Click += (_, _) => StartTest();
        _stopButton.Click += (_, _) => StopTest();
        _clearButton.Click += (_, _) => _history.Items.Clear();
        _createRuleButton.Click += (_, _) =>
        {
            if (_lastCombo is not null) CreateRuleRequested?.Invoke(_lastCombo);
        };
        _modeResult.CheckedChanged += (_, _) =>
            _outputBox.Visible = _modeResult.Checked && _running;
    }

    private void StartTest()
    {
        _running = true;
        _history.Items.Clear();
        _lastCombo = null;
        _startButton.Enabled = false;
        _stopButton.Enabled = true;
        _createRuleButton.Enabled = false;
        _outputBox.Visible = _modeResult.Checked;
        _app.BeginTestSession(testOriginalInput: _modeOriginal.Checked);
        _statusLabel.Text = _modeOriginal.Checked
            ? "Capturando entrada original (las reglas están suspendidas)."
            : "Las reglas se aplican: escribe en el cuadro para comprobar la salida.";
        // La pestaña necesita recibir teclas aunque no tenga el foco directo.
        FindForm()?.KeyPreview = true;
    }

    private void StopTest()
    {
        _running = false;
        _startButton.Enabled = true;
        _stopButton.Enabled = false;
        _outputBox.Visible = false;
        _history.Items.Clear(); // historial temporal: desaparece al detener
        _app.EndTestSession();
        _statusLabel.Text = "Prueba detenida.";
    }

    /// <summary>El formulario reenvía aquí las teclas cuando la pestaña está visible.</summary>
    public bool TryHandleKey(KeyEventArgs e, int scanCode, bool extended)
    {
        if (!_running) return false;
        var press = new KeyPress
        {
            Key = Engine.WinKeyMapper.ToVirtualKey(e.KeyCode),
            ScanCode = scanCode,
            Extended = extended,
            CtrlDown = (e.Modifiers & Keys.Control) != 0,
            AltLeftDown = (e.Modifiers & Keys.Alt) != 0,
            ShiftDown = (e.Modifiers & Keys.Shift) != 0,
            EventType = KeyEventType.Pressed,
        };
        Register(press, note: press.IsAltGr ? "AltGr = Ctrl izq. + Alt der." : null);
        // En modo resultado dejamos que el control reciba la tecla normalmente.
        return _modeOriginal.Checked;
    }

    private void OnKeyObserved(KeyPress press)
    {
        if (!_running || !IsHandleCreated) return;
        BeginInvoke(() =>
        {
            if (press.EventType == KeyEventType.Released) return; // menos ruido
            Register(press, note: press.IsInjected ? "Generada por Gestor de Teclado" : null);
        });
    }

    private void Register(KeyPress press, string? note)
    {
        var combo = press.ToCombination();
        _lastCombo = combo;
        _createRuleButton.Enabled = _running;

        var charOut = press.Key switch
        {
            _ when press.EventType == KeyEventType.Released => "",
            _ => SafeChar(press),
        };

        var item = new ListViewItem(KeyNames.Display(press.Key));
        item.SubItems.Add(combo.ToString());
        item.SubItems.Add(press.EventType switch
        {
            KeyEventType.Pressed => "presionada",
            KeyEventType.Released => "soltada",
            KeyEventType.AutoRepeat => "repetida",
            _ => "?",
        });
        item.SubItems.Add(charOut);
        item.SubItems.Add($"0x{(int)press.Key:X2} / 0x{press.ScanCode:X2}{(press.Extended ? " ext" : "")}");
        item.SubItems.Add(note ?? "");
        _history.Items.Add(item);
        const int maxItems = 200;
        while (_history.Items.Count > maxItems) _history.Items.RemoveAt(0);
        item.EnsureVisible();
    }

    private static string SafeChar(KeyPress press)
    {
        try { return Engine.TestProbe.ResultingChar(press) ?? ""; }
        catch { return ""; }
    }

    /// <summary>Mensaje específico si el usuario pulsa Fn y no llega nada.</summary>
    public void ShowNoDetectablePress()
    {
        _statusLabel.Text = "No se recibió una pulsación detectable. " +
            "Si has pulsado Fn, es probable que tu teclado no transmita esa pulsación a Windows.";
    }
}
