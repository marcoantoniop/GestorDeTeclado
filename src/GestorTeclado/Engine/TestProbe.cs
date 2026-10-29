using System.Runtime.InteropServices;
using GestorTeclado.Core;

namespace GestorTeclado.Engine;

/// <summary>
/// Utilidades para la pestaña Pruebas: traducir el VK de un evento WinForms a
/// nuestro modelo y obtener el carácter resultante según la distribución activa
/// (ToUnicode), sin depender del motor.
/// </summary>
public static class TestProbe
{
    [DllImport("user32.dll")]
    private static extern int ToUnicode(uint wVirtKey, uint wScanCode, byte[]? lpKeyState,
        [Out, MarshalAs(UnmanagedType.LPWStr, SizeParamIndex = 4)] out string pwszBuf,
        int cchBuf, uint wFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetKeyboardState(byte[] lpKeyState);

    /// <summary>Carácter que produciría esta pulsación con la distribución activa; null si no produce texto.</summary>
    public static string? ResultingChar(KeyPress press)
    {
        var state = new byte[256];
        GetKeyboardState(state);
        if (press.ShiftDown) state[(int)VirtualKey.ShiftLeft] |= 0x80;
        if (press.CtrlDown) state[(int)VirtualKey.ControlLeft] |= 0x80;
        if (press.AltDown) state[(int)VirtualKey.AltLeft] |= 0x80;

        int ret;
        try
        {
            ret = ToUnicode((uint)KeyNames.ToVk(press.Key), (uint)(press.ScanCode & 0xFF), state,
                out var buf, 16, 0);
        }
        catch (DllNotFoundException) { return null; } // fuera de Windows (p. ej. tests en Linux)

        return ret > 0 && !string.IsNullOrEmpty(buf) ? buf : null;
    }

    /// <summary>Convierte Keys (WinForms) + estado físico en un KeyPress para la captura de prueba.</summary>
    public static KeyPress FromKeys(Keys keyData, int scanCode, bool extended, bool injected, KeyEventType type)
    {
        var code = keyData & Keys.KeyCode;
        VirtualKey vk;
        try { vk = KeyNames.FromVk((int)code); }
        catch { vk = (VirtualKey)(int)code; }

        // WinForms informa Mayús/Ctrl/Alt como flags combinados; los lados exactos
        // se recuperan con GetAsyncKeyState cuando estamos en Windows.
        return new KeyPress
        {
            Key = vk,
            ScanCode = scanCode,
            Extended = extended,
            CtrlDown = (keyData & Keys.Control) != 0,
            AltLeftDown = (keyData & Keys.Alt) != 0,
            AltRightDown = false,
            ShiftDown = (keyData & Keys.Shift) != 0,
            WinDown = false,
            IsInjected = injected,
            EventType = type,
        };
    }
}
