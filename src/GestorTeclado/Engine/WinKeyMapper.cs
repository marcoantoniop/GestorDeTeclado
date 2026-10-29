using GestorTeclado.Core;

namespace GestorTeclado.Engine;

/// <summary>
/// Traducción entre Keys (WinForms) y VirtualKey (modelo). Independiente del
/// hook: lo usan los controles de captura y la pestaña Pruebas.
/// </summary>
public static class WinKeyMapper
{
    public static VirtualKey ToVirtualKey(Keys key) => key switch
    {
        Keys.Back => VirtualKey.Backspace,
        Keys.Tab => VirtualKey.Tab,
        Keys.Enter => VirtualKey.Enter,
        Keys.ControlKey or Keys.LControlKey => VirtualKey.ControlLeft,
        Keys.RControlKey => VirtualKey.ControlRight,
        Keys.Menu or Keys.LMenu => VirtualKey.AltLeft,
        Keys.RMenu => VirtualKey.AltRight,
        Keys.ShiftKey or Keys.LShiftKey => VirtualKey.ShiftLeft,
        Keys.RShiftKey => VirtualKey.ShiftRight,
        Keys.CapsLock => VirtualKey.CapsLock,
        Keys.Escape => VirtualKey.Escape,
        Keys.Space => VirtualKey.Space,
        Keys.Prior => VirtualKey.PageUp,
        Keys.Next => VirtualKey.PageDown,
        Keys.End => VirtualKey.End,
        Keys.Home => VirtualKey.Home,
        Keys.Left => VirtualKey.Left,
        Keys.Up => VirtualKey.Up,
        Keys.Right => VirtualKey.Right,
        Keys.Down => VirtualKey.Down,
        Keys.PrintScreen => VirtualKey.PrintScreen,
        Keys.Insert => VirtualKey.Insert,
        Keys.Delete => VirtualKey.Delete,
        Keys.Help => VirtualKey.Help,
        Keys.NumLock => VirtualKey.NumLock,
        Keys.Scroll => VirtualKey.ScrollLock,
        Keys.LWin => VirtualKey.LWin,
        Keys.RWin => VirtualKey.RWin,
        Keys.Apps => VirtualKey.Apps,
        Keys.Sleep => VirtualKey.Sleep,
        Keys.D0 or Keys.NumPad0 => VirtualKey.Key0,
        Keys.D1 or Keys.NumPad1 => VirtualKey.Key1,
        Keys.D2 or Keys.NumPad2 => VirtualKey.Key2,
        Keys.D3 or Keys.NumPad3 => VirtualKey.Key3,
        Keys.D4 or Keys.NumPad4 => VirtualKey.Key4,
        Keys.D5 or Keys.NumPad5 => VirtualKey.Key5,
        Keys.D6 or Keys.NumPad6 => VirtualKey.Key6,
        Keys.D7 or Keys.NumPad7 => VirtualKey.Key7,
        Keys.D8 or Keys.NumPad8 => VirtualKey.Key8,
        Keys.D9 or Keys.NumPad9 => VirtualKey.Key9,
        Keys.Oemplus => VirtualKey.OemPlus,
        Keys.OemMinus or Keys.Subtract => VirtualKey.OemQuote,
        Keys.OemOpenBrackets => VirtualKey.OemBraceOpen,
        Keys.OemPipe => VirtualKey.OemBackslash,
        Keys.OemCloseBrackets => VirtualKey.OemBraceClose,
        Keys.OemQuotes => VirtualKey.OemTilde,
        Keys.OemQuestion => VirtualKey.OemPipe,
        Keys.Oemtilde => VirtualKey.OemMinus,
        Keys.Multiply => VirtualKey.Multiply,
        Keys.Add => VirtualKey.Add,
        Keys.Decimal => VirtualKey.Decimal,
        Keys.Divide => VirtualKey.Divide,
        Keys.F1 => VirtualKey.F1, Keys.F2 => VirtualKey.F2, Keys.F3 => VirtualKey.F3,
        Keys.F4 => VirtualKey.F4, Keys.F5 => VirtualKey.F5, Keys.F6 => VirtualKey.F6,
        Keys.F7 => VirtualKey.F7, Keys.F8 => VirtualKey.F8, Keys.F9 => VirtualKey.F9,
        Keys.F10 => VirtualKey.F10, Keys.F11 => VirtualKey.F11, Keys.F12 => VirtualKey.F12,
        Keys.F13 => VirtualKey.F13, Keys.F14 => VirtualKey.F14, Keys.F15 => VirtualKey.F15,
        Keys.F16 => VirtualKey.F16, Keys.F17 => VirtualKey.F17, Keys.F18 => VirtualKey.F18,
        Keys.F19 => VirtualKey.F19, Keys.F20 => VirtualKey.F20, Keys.F21 => VirtualKey.F21,
        Keys.F22 => VirtualKey.F22, Keys.F23 => VirtualKey.F23, Keys.F24 => VirtualKey.F24,
        Keys.VolumeMute => VirtualKey.VolumeMute,
        Keys.VolumeDown => VirtualKey.VolumeDown,
        Keys.VolumeUp => VirtualKey.VolumeUp,
        Keys.MediaNextTrack => VirtualKey.MediaNextTrack,
        Keys.MediaPreviousTrack => VirtualKey.MediaPrevTrack,
        Keys.MediaStop => VirtualKey.MediaStop,
        Keys.MediaPlayPause => VirtualKey.MediaPlayPause,
        Keys.LaunchMail => VirtualKey.LaunchMail,
        Keys.BrowserBack => VirtualKey.BrowserBack,
        Keys.BrowserForward => VirtualKey.BrowserForward,
        _ when key >= Keys.A && key <= Keys.Z => VirtualKey.KeyA + (key - Keys.A),
        _ => (VirtualKey)(int)key, // sin coincidencia: conservar el VK numérico
    };

    public static Keys ToKeys(VirtualKey vk) => vk switch
    {
        VirtualKey.Backspace => Keys.Back,
        VirtualKey.Tab => Keys.Tab,
        VirtualKey.Enter => Keys.Enter,
        VirtualKey.Escape => Keys.Escape,
        VirtualKey.Space => Keys.Space,
        >= VirtualKey.KeyA and <= VirtualKey.KeyZ => Keys.A + (vk - VirtualKey.KeyA),
        >= VirtualKey.Key0 and <= VirtualKey.Key9 => Keys.D0 + (vk - VirtualKey.Key0),
        >= VirtualKey.F1 and <= VirtualKey.F24 => Keys.F1 + (vk - VirtualKey.F1),
        _ => Keys.None,
    };
}
