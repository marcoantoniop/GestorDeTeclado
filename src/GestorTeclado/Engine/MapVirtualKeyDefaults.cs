using GestorTeclado.Core;

namespace GestorTeclado.Engine;

/// <summary>
/// Tabla de escancodes por defecto (set 1) y flags extendidos para teclas
/// que pueden emitirse sin haberse observado físicamente (reglas creadas a
/// mano, salidas con modificadores no pulsados). Basada en la tabla estándar
/// de Windows (MAPVK_VK_TO_VSC).
/// </summary>
public static class MapVirtualKeyDefaults
{
    public static int Scancode(VirtualKey key) => key switch
    {
        >= VirtualKey.KeyA and <= VirtualKey.KeyZ => ScanLetter(key),
        >= VirtualKey.Key0 and <= VirtualKey.Key9 => 0x0B + (key - VirtualKey.Key0), // 0 está en 0x0B
        >= VirtualKey.F1 and <= VirtualKey.F24 => 0x3B + (key - VirtualKey.F1),
        >= VirtualKey.NumPad0 and <= VirtualKey.NumPad9 => 0x52 + (key - VirtualKey.NumPad0),
        VirtualKey.Escape => 0x01,
        VirtualKey.Backspace => 0x0E,
        VirtualKey.Tab => 0x0F,
        VirtualKey.Enter => 0x1C,
        VirtualKey.Space => 0x39,
        VirtualKey.CapsLock => 0x3A,
        VirtualKey.ShiftLeft => 0x2A,
        VirtualKey.ShiftRight => 0x36,
        VirtualKey.ControlLeft => 0x1D,
        VirtualKey.ControlRight => 0x1D, // extended
        VirtualKey.AltLeft => 0x38,
        VirtualKey.AltRight => 0x38,    // extended
        VirtualKey.LWin or VirtualKey.RWin => 0x5B,
        VirtualKey.Apps => 0x5D,
        VirtualKey.OemMinus => 0x29,
        VirtualKey.OemPlus => 0x0D,
        VirtualKey.OemBraceOpen => 0x1A,
        VirtualKey.OemBackslash => 0x2B,
        VirtualKey.OemBraceClose => 0x1B,
        VirtualKey.OemTilde => 0x15,
        VirtualKey.OemQuote => 0x0C,
        VirtualKey.OemPipe => 0x56,
        VirtualKey.Multiply => 0x37,
        VirtualKey.Add => 0x4E,
        VirtualKey.Subtract => 0x4A,
        VirtualKey.Decimal => 0x4F,
        VirtualKey.Divide => 0x35,
        VirtualKey.Separator => 0x53,
        VirtualKey.NumLock => 0x45,
        VirtualKey.ScrollLock => 0x46,
        VirtualKey.PrintScreen => 0x37,
        VirtualKey.Pause => 0x45,
        VirtualKey.Delete => 0x53,
        VirtualKey.Insert => 0x52,
        VirtualKey.Home => 0x47,
        VirtualKey.End => 0x4F,
        VirtualKey.PageUp => 0x49,
        VirtualKey.PageDown => 0x51,
        VirtualKey.Left => 0x4B,
        VirtualKey.Up => 0x48,
        VirtualKey.Right => 0x4D,
        VirtualKey.Down => 0x50,
        _ => 0, // desconocida: SendInput usará wVk sin escancode explícito
    };

    private static int ScanLetter(VirtualKey k) => k switch
    {
        VirtualKey.KeyA => 0x1E, VirtualKey.KeyB => 0x30, VirtualKey.KeyC => 0x2E,
        VirtualKey.KeyD => 0x20, VirtualKey.KeyE => 0x12, VirtualKey.KeyF => 0x21,
        VirtualKey.KeyG => 0x22, VirtualKey.KeyH => 0x23, VirtualKey.KeyI => 0x17,
        VirtualKey.KeyJ => 0x24, VirtualKey.KeyK => 0x25, VirtualKey.KeyL => 0x26,
        VirtualKey.KeyM => 0x32, VirtualKey.KeyN => 0x31, VirtualKey.KeyO => 0x18,
        VirtualKey.KeyP => 0x19, VirtualKey.KeyQ => 0x10, VirtualKey.KeyR => 0x13,
        VirtualKey.KeyS => 0x1F, VirtualKey.KeyT => 0x14, VirtualKey.KeyU => 0x16,
        VirtualKey.KeyV => 0x2F, VirtualKey.KeyW => 0x11, VirtualKey.KeyX => 0x2D,
        VirtualKey.KeyY => 0x15, VirtualKey.KeyZ => 0x2C,
        _ => 0,
    };

    /// <summary>Teclas que siempre se envían con KEYEVENTF_EXTENDEDKEY.</summary>
    public static bool IsExtended(VirtualKey key) => key switch
    {
        VirtualKey.Enter or VirtualKey.Divide or VirtualKey.PrintScreen or VirtualKey.Pause
        or VirtualKey.Home or VirtualKey.End or VirtualKey.PageUp or VirtualKey.PageDown
        or VirtualKey.Insert or VirtualKey.Delete or VirtualKey.Left or VirtualKey.Right
        or VirtualKey.Up or VirtualKey.Down or VirtualKey.NumLock or VirtualKey.ScrollLock
        or VirtualKey.ControlRight or VirtualKey.AltRight or VirtualKey.LWin or VirtualKey.RWin
        or VirtualKey.Apps => true,
        VirtualKey.Multiply => false, // Num * usa el prefijo E0 en escancode, no flag VK ext. habitual
        _ => false,
    };
}
