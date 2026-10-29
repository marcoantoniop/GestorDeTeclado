namespace GestorTeclado.Core;

/// <summary>
/// Identificador virtual de tecla. Amplía <see cref="System.Windows.Forms.Keys"/>
/// con las teclas que WinForms no representa explícitamente (AltGr, separadores,
/// teclado numérico distinguido del teclado principal).
/// El valor numérico coincide con los códigos virtuales de Windows cuando existe.
/// </summary>
public enum VirtualKey
{
    // --- Modificadores y casos especiales de Gestor de Teclado ---
    AltGr = -1,          // Se representa internamente como CtrlDer + AltIzq (extended)
    LWin = 0x5B,
    RWin = 0x5C,
    Apps = 0x5D,
    Sleep = 0x5F,

    // --- Separadores y bloqueos ---
    Backspace = 0x08,
    Tab = 0x09,
    Clear = 0x0C,
    Enter = 0x0D,
    ShiftLeft = 0xA0,
    ShiftRight = 0xA1,
    ControlLeft = 0xA2,
    ControlRight = 0xA3,
    AltLeft = 0xA4,
    AltRight = 0xA5,
    Pause = 0x13,
    CapsLock = 0x14,
    Escape = 0x1B,
    Space = 0x20,
    PageUp = 0x21,
    PageDown = 0x22,
    End = 0x23,
    Home = 0x24,
    Left = 0x25,
    Up = 0x26,
    Right = 0x27,
    Down = 0x28,
    PrintScreen = 0x2C,
    Insert = 0x2D,
    Delete = 0x2E,
    Help = 0x2F,
    LButton = 0x01,
    RButton = 0x02,

    // --- Teclas alfanuméricas ---
    Key0 = 0x30, Key1, Key2, Key3, Key4, Key5, Key6, Key7, Key8, Key9,
    KeyA = 0x41, KeyB, KeyC, KeyD, KeyE, KeyF, KeyG, KeyH, KeyI, KeyJ,
    KeyK, KeyL, KeyM, KeyN, KeyO, KeyP, KeyQ, KeyR, KeyS, KeyT,
    KeyU, KeyV, KeyW, KeyX, KeyY, KeyZ,

    Multiply = 0x6A,
    Add = 0x6B,
    Separator = 0x6C,
    Subtract = 0x6D,
    Decimal = 0x6E,
    Divide = 0x6F,

    // --- Físicas OEM (dependen de distribución; se muestran por escancode) ---
    OemMinus = 0xC0,      // OEM_1 en ES: '¡' / '¿'
    OemPlus = 0xBB,       // '=' en español latino
    OemBraceOpen = 0xDB,  // '['
    OemBackslash = 0xDC,  // '\'
    OemBraceClose = 0xDD, // ']'
    OemTilde = 0xDE,      // '}' en ES español
    OemQuote = 0xBF,      // '-' en ES español
    OemPipe = 0xE2,       // '<' '>' '|' (ISO, extended)

    // --- Teclado numérico ---
    NumPad0 = 0x60, NumPad1, NumPad2, NumPad3, NumPad4,
    NumPad5, NumPad6, NumPad7, NumPad8, NumPad9,
    NumLock = 0x90,
    ScrollLock = 0x91,

    F1 = 0x70, F2, F3, F4, F5, F6, F7, F8,
    F9, F10, F11, F12, F13, F14, F15, F16,
    F17, F18, F19, F20, F21, F22, F23, F24,

    VolumeMute = 0xAD,
    VolumeDown = 0xAE,
    VolumeUp = 0xAF,
    MediaNextTrack = 0xB0,
    MediaPrevTrack = 0xB1,
    MediaStop = 0xB2,
    MediaPlayPause = 0xB3,
    LaunchMail = 0xB4,
    BrowserBack = 0xA6,
    BrowserForward = 0xA7,
}
