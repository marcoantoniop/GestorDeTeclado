using System.Runtime.InteropServices;
using GestorTeclado.Core;

namespace GestorTeclado.Engine;

/// <summary>
/// Implementación real sobre SendInput (user32). Marca cada evento inyectado
/// con un tag en dwExtraInfo para que el hook low-level lo reconozca y no
/// reactive reglas con sus propias salidas (prevención de ciclos).
/// Solo funciona en Windows.
/// </summary>
public sealed class SendInputKeyboardSender : IKeyboardSender
{
    /// <summary>Tag de identificación de eventos generados por Gestor de Teclado.</summary>
    public const uint InjectionTag = 0x4B424D52; // 'KBMR' = KeyboardMapRemap

    private const ushort VK_PACKET = 0xE7;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern ushort MapVirtualKeyW(uint uVirtKeyCode, uint uMapType);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
        public static int Size => Marshal.SizeOf<INPUT>();
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public UIntPtr dwExtraInfo; }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT { public uint uMsg; public ushort wParamL, wParamH; }

    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_UNICODE = 0x0004;
    private const uint KEYEVENTF_SCANCODE = 0x0008;

    private static INPUT MakeKey(ushort vk, ushort scan, uint flags) => new()
    {
        type = INPUT_KEYBOARD,
        U = new InputUnion
        {
            ki = new KEYBDINPUT
            {
                wVk = vk,
                wScan = scan,
                dwFlags = flags,
                time = 0,
                dwExtraInfo = new UIntPtr(InjectionTag),
            }
        }
    };

    private void Emit(params INPUT[] inputs)
    {
        var sent = SendInput((uint)inputs.Length, inputs, INPUT.Size);
        if (sent != (uint)inputs.Length)
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(),
                "SendInput no pudo enviar todos los eventos.");
    }

    private static ushort ScanFor(VirtualKey key, bool extended)
    {
        var scan = MapVirtualKeyW(KeyNames.ToVk(key), 0 /*MAPVK_VK_TO_VSC*/);
        return extended ? (ushort)(scan | 0xE000) : scan;
    }

    public void KeyDown(VirtualKey key, int scanCode, bool extended) =>
        Emit(MakeKey((ushort)KeyNames.ToVk(key), (ushort)(scanCode & 0xFF),
            extended ? KEYEVENTF_EXTENDEDKEY : 0));

    public void KeyUp(VirtualKey key, int scanCode, bool extended) =>
        Emit(MakeKey((ushort)KeyNames.ToVk(key), (ushort)(scanCode & 0xFF),
            (extended ? KEYEVENTF_EXTENDEDKEY : 0) | KEYEVENTF_KEYUP));

    public void TapKey(VirtualKey key, int scanCode, bool extended)
    {
        var vk = (ushort)KeyNames.ToVk(key);
        var scan = (ushort)(scanCode != 0 ? scanCode & 0xFF : ScanFor(key, extended));
        var extFlag = extended || IsNumpadOrNav(key) ? KEYEVENTF_EXTENDEDKEY : 0u;
        Emit(
            MakeKey(vk, scan, extFlag),
            MakeKey(vk, scan, extFlag | KEYEVENTF_KEYUP));
    }

    private static bool IsNumpadOrNav(VirtualKey k) => k switch
    {
        VirtualKey.Enter or VirtualKey.Divide or VirtualKey.Home or VirtualKey.End
        or VirtualKey.PageUp or VirtualKey.PageDown or VirtualKey.Insert or VirtualKey.Delete
        or VirtualKey.Left or VirtualKey.Right or VirtualKey.Up or VirtualKey.Down
        or VirtualKey.NumLock or VirtualKey.PrintScreen or VirtualKey.RWin or VirtualKey.AltRight
        or VirtualKey.ControlRight => true,
        _ => false,
    };

    public void SendText(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        // UTF-16: los caracteres fuera del BMP llegan como pares sustitutos ya emparejados.
        var inputs = new List<INPUT>(text.Length * 2);
        foreach (var ch in text.EnumerateRunes())
        {
            var rune = ch;
            if (rune.Value <= 0xFFFF)
            {
                inputs.Add(MakeKey(VK_PACKET, (ushort)rune.Value, KEYEVENTF_UNICODE));
                inputs.Add(MakeKey(VK_PACKET, (ushort)rune.Value, KEYEVENTF_UNICODE | KEYEVENTF_KEYUP));
            }
            else
            {
                rune.EncodeToUtf16(out char high, out char low);
                inputs.Add(MakeKey(VK_PACKET, (ushort)high, KEYEVENTF_UNICODE));
                inputs.Add(MakeKey(VK_PACKET, (ushort)high, KEYEVENTF_UNICODE | KEYEVENTF_KEYUP));
                inputs.Add(MakeKey(VK_PACKET, (ushort)low, KEYEVENTF_UNICODE));
                inputs.Add(MakeKey(VK_PACKET, (ushort)low, KEYEVENTF_UNICODE | KEYEVENTF_KEYUP));
            }
        }
        // Enviar en bloques para no superar límites razonables de la cola.
        const int batch = 16;
        for (var i = 0; i < inputs.Count; i += batch)
            Emit(inputs.Skip(i).Take(batch).ToArray());
    }

    public void ReleaseStuckModifiers(bool ctrl, bool alt, bool shift, bool win)
    {
        var up = new List<INPUT>();
        if (ctrl) up.Add(MakeKey((ushort)VirtualKey.ControlLeft, 0x1D, KEYEVENTF_KEYUP));
        if (alt) up.Add(MakeKey((ushort)VirtualKey.AltLeft, 0x38, KEYEVENTF_KEYUP));
        if (shift) up.Add(MakeKey((ushort)VirtualKey.ShiftLeft, 0x2A, KEYEVENTF_KEYUP));
        if (win) up.Add(MakeKey((ushort)VirtualKey.LWin, 0x5B, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP));
        if (up.Count > 0) Emit(up.ToArray());
    }
}
