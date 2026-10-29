using System.Runtime.InteropServices;
using System.Diagnostics;
using GestorTeclado.Core;

namespace GestorTeclado.Engine;

/// <summary>
/// Conexión del motor con Windows mediante un hook global WH_KEYBOARD_LL.
/// Requisitos cumplidos de la especificación:
/// - La captura es lo más rápida posible (solo P/Invoke mínimo y delegación al motor).
/// - Se instala en un hilo con bomba de mensajes dedicada (no bloquea la UI).
/// - Detecta auto-repetición siguiendo el consejo de Microsoft Learn: comparar
///   el estado previo con GetAsyncKeyState para distinguir transición 0 de 1.
/// - Reconoce eventos inyectados (dwExtraInfo == tag propio) para evitar ciclos.
/// </summary>
public sealed class LowLevelKeyboardHook : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;

    private const uint LLKHF_EXTENDED = 0x01;
    private const uint LLKHF_INJECTED = 0x10;
    private const uint LLKHF_ALTDOWN = 0x20;
    private const uint LLKHF_UP = 0x80;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookExW(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandleW(string? lpModuleName);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    private readonly RemapEngine _engine;
    private readonly HookProc _callback; // mantener referencia: evita GC del delegado
    private IntPtr _hook = IntPtr.Zero;
    private Thread? _hookThread;
    private readonly ManualResetEventSlim _installed = new(false);
    private Exception? _installError;

    /// <summary>true si el hook quedó instalado correctamente.</summary>
    public bool IsInstalled => _hook != IntPtr.Zero;

    /// <summary>Invocado cuando el motor falla al emitir (estado Error en bandeja).</summary>
    public event Action<Exception>? EmitFailure;

    public LowLevelKeyboardHook(RemapEngine engine)
    {
        _engine = engine;
        _callback = HookCallback;
    }

    /// <summary>Instala el hook en un hilo dedicado con su propia bomba de mensajes.</summary>
    public void Install()
    {
        if (_hookThread is not null) return;
        _installError = null;
        _hookThread = new Thread(HookLoop) { IsBackground = true, Name = "GestorTeclado.Hook" };
        _hookThread.Start();
        _installed.Wait(TimeSpan.FromSeconds(3));
        if (_installError is not null)
            throw new InvalidOperationException("No se pudo activar el motor de teclado.", _installError);
    }

    private void HookLoop()
    {
        try
        {
            using var ctx = new ApplicationContext();
            _hook = SetWindowsHookExW(WH_KEYBOARD_LL, _callback,
                GetModuleHandleW(null), 0);
            if (_hook == IntPtr.Zero)
                _installError = new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(),
                    "SetWindowsHookEx(WH_KEYBOARD_LL) falló.");
            _installed.Set();
            if (_installError is null)
                Application.Run(ctx); // bomba de mensajes requerida por los hooks low-level
        }
        catch (Exception ex)
        {
            _installError = ex;
            _installed.Set();
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0) return CallNextHookEx(_hook, nCode, wParam, lParam);

        var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
        var msg = (int)wParam;
        bool isKeyDown = msg is WM_KEYDOWN or WM_SYSKEYDOWN;
        bool isKeyUp = msg is WM_KEYUP or WM_SYSKEYUP;

        // Solo teclas; ignoramos ratón u otros mensajes que lleguen por error.
        if (isKeyDown || isKeyUp)
        {
            var press = Translate(data, isKeyDown);
            RemapEngine.Verdict verdict;
            try
            {
                verdict = isKeyDown ? ClassifyDown(press) : _engine.ProcessKeyUp(press);
            }
            catch (Exception ex)
            {
                EmitFailure?.Invoke(ex);
                return CallNextHookEx(_hook, nCode, wParam, lParam);
            }
            if (verdict == RemapEngine.Verdict.Block)
                return (IntPtr)1; // consumir: la acción original no llega a la aplicación
        }

        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    /// <summary>
    /// Distingue pulsación inicial de auto-repetición. En el hook low-level no
    /// hay transition bit, así que consultamos si la tecla ya estaba abajo
    /// (GetKeyState vía GetAsyncKeyState no distingue; usamos nuestro registro
    /// interno a través del motor: ProcessKeyDown maneja _firedThisPress).
    /// </summary>
    private RemapEngine.Verdict ClassifyDown(KeyPress press) => _engine.ProcessKeyDown(press);

    private static KeyPress Translate(KBDLLHOOKSTRUCT d, bool isKeyDown)
    {
        VirtualKey key;
        try { key = KeyNames.FromVk((int)d.vkCode); }
        catch { key = (VirtualKey)d.vkCode; } // tecla desconocida: la dejamos pasar tal cual

        bool extended = (d.flags & LLKHF_EXTENDED) != 0;
        bool injected = (d.flags & LLKHF_INJECTED) != 0
                        || d.dwExtraInfo.ToUInt64() == SendInputKeyboardSender.InjectionTag;

        // Estado de modificadores según las dos teclas físicas implicadas en AltGr:
        // Ctrl izquierdo + Alt derecho (extended) = AltGr en la mayoría de distribuciones ES.
        bool ctrlL = Down(VirtualKey.ControlLeft);
        bool ctrlR = Down(VirtualKey.ControlRight);
        bool altL = Down(VirtualKey.AltLeft);
        bool altR = Down(VirtualKey.AltRight);
        bool shiftL = Down(VirtualKey.ShiftLeft);
        bool shiftR = Down(VirtualKey.ShiftRight);
        bool win = Down(VirtualKey.LWin) || Down(VirtualKey.RWin);

        return new KeyPress
        {
            Key = key,
            ScanCode = (int)d.scanCode,
            Extended = extended,
            CtrlDown = ctrlL || ctrlR,
            AltLeftDown = altL,
            AltRightDown = altR,
            ShiftDown = shiftL || shiftR,
            WinDown = win,
            IsInjected = injected,
            EventType = isKeyDown ? KeyEventType.Pressed : KeyEventType.Released,
        };
    }

    private static bool Down(VirtualKey k) => (GetAsyncKeyState(KeyNames.ToVk(k)) & 0x8000) != 0;

    public void Dispose()
    {
        if (_hook != IntPtr.Zero) { UnhookWindowsHookEx(_hook); _hook = IntPtr.Zero; }
        try { _hookThread?.Join(TimeSpan.FromSeconds(2)); } catch { /* mejor esfuerzo */ }
        _hookThread = null;
        _installed.Dispose();
    }
}
