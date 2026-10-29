using System.Globalization;

namespace GestorTeclado.Core;

/// <summary>
/// Nombres mostrables y parseables de las teclas, en español.
/// Centraliza la convención "AltGr", "Enter", "AvPág"… usada por la UI,
/// el JSON y la pestaña Pruebas.
/// </summary>
public static class KeyNames
{
    private static readonly Dictionary<VirtualKey, string> DisplayMap = new()
    {
        [VirtualKey.AltGr] = "AltGr",
        [VirtualKey.ControlLeft] = "Ctrl",
        [VirtualKey.ControlRight] = "Ctrl der.",
        [VirtualKey.AltLeft] = "Alt",
        [VirtualKey.AltRight] = "Alt der.",
        [VirtualKey.ShiftLeft] = "Mayús",
        [VirtualKey.ShiftRight] = "Mayús der.",
        [VirtualKey.LWin] = "Win",
        [VirtualKey.RWin] = "Win der.",
        [VirtualKey.Enter] = "Intro",
        [VirtualKey.Escape] = "Esc",
        [VirtualKey.Backspace] = "Retroceso",
        [VirtualKey.Tab] = "Tab",
        [VirtualKey.Space] = "Espacio",
        [VirtualKey.Delete] = "Supr",
        [VirtualKey.Insert] = "Ins",
        [VirtualKey.Home] = "Inicio",
        [VirtualKey.End] = "Fin",
        [VirtualKey.PageUp] = "Repg",
        [VirtualKey.PageDown] = "AvPág",
        [VirtualKey.Left] = "←",
        [VirtualKey.Up] = "↑",
        [VirtualKey.Right] = "→",
        [VirtualKey.Down] = "↓",
        [VirtualKey.CapsLock] = "Bloq Mayús",
        [VirtualKey.NumLock] = "Bloq Num",
        [VirtualKey.ScrollLock] = "Bloq Despl",
        [VirtualKey.PrintScreen] = "Imp Pant",
        [VirtualKey.Pause] = "Pausa",
        [VirtualKey.Apps] = "Aplicaciones",
        [VirtualKey.OemMinus] = "¡/¿",
        [VirtualKey.OemPlus] = "=",
        [VirtualKey.OemBraceOpen] = "[",
        [VirtualKey.OemBackslash] = "\\",
        [VirtualKey.OemBraceClose] = "]",
        [VirtualKey.OemTilde] = "+",
        [VirtualKey.OemQuote] = "-",
        [VirtualKey.OemPipe] = "< >",
        [VirtualKey.Multiply] = "Num *",
        [VirtualKey.Add] = "Num +",
        [VirtualKey.Subtract] = "Num -",
        [VirtualKey.Decimal] = "Num ,",
        [VirtualKey.Divide] = "Num /",
        [VirtualKey.Separator] = "Num .",
        [VirtualKey.VolumeMute] = "Silencio",
        [VirtualKey.VolumeDown] = "Vol -",
        [VirtualKey.VolumeUp] = "Vol +",
        [VirtualKey.MediaPlayPause] = "Repr/Pausa",
        [VirtualKey.MediaNextTrack] = "Pista sig.",
        [VirtualKey.MediaPrevTrack] = "Pista ant.",
        [VirtualKey.MediaStop] = "Detener",
    };

    /// <summary>Nombre para la interfaz. Nunca lanza: caídas seguras.</summary>
    public static string Display(VirtualKey key)
    {
        if (DisplayMap.TryGetValue(key, out var name)) return name;
        if (key >= VirtualKey.F1 && key <= VirtualKey.F24) return "F" + (key - VirtualKey.F1 + 1);
        if (key >= VirtualKey.NumPad0 && key <= VirtualKey.NumPad9)
            return "Num " + (key - VirtualKey.NumPad0);
        if (key >= VirtualKey.KeyA && key <= VirtualKey.KeyZ) return ((char)('A' + (key - VirtualKey.KeyA))).ToString();
        if (key >= VirtualKey.Key0 && key <= VirtualKey.Key9) return ((char)('0' + (key - VirtualKey.Key0))).ToString();
        return key.ToString();
    }

    private static readonly Dictionary<string, VirtualKey> ParseMap = BuildParseMap();

    private static Dictionary<string, VirtualKey> BuildParseMap()
    {
        var map = new Dictionary<string, VirtualKey>(StringComparer.OrdinalIgnoreCase);
        foreach (var (vk, name) in DisplayMap) map[name] = vk;
        // Sinónimos habituales en español e inglés.
        map["mayus"] = map["shift"] = VirtualKey.ShiftLeft;
        map["control"] = VirtualKey.ControlLeft;
        map["ctrll"] = VirtualKey.ControlLeft;
        map["ctrlr"] = VirtualKey.ControlRight;
        map["altgr"] = VirtualKey.AltGr;
        map["return"] = map["intro"] = VirtualKey.Enter;
        map["escape"] = VirtualKey.Escape;
        map["retroceso"] = map["backspace"] = VirtualKey.Backspace;
        map["supr"] = map["delete"] = VirtualKey.Delete;
        map["espacio"] = map["space"] = VirtualKey.Space;
        map["repag"] = map["pageup"] = VirtualKey.PageUp;
        map["avpag"] = map["pagedown"] = VirtualKey.PageDown;
        map["inicio"] = map["home"] = VirtualKey.Home;
        map["fin"] = map["end"] = VirtualKey.End;
        map["izquierda"] = map["left"] = VirtualKey.Left;
        map["derecha"] = map["right"] = VirtualKey.Right;
        map["arriba"] = map["up"] = VirtualKey.Up;
        map["abajo"] = map["down"] = VirtualKey.Down;
        map["windows"] = map["win"] = VirtualKey.LWin;
        for (var i = 0; i <= 9; i++) map[(VirtualKey.Key0 + i).ToString()] = VirtualKey.Key0 + i;
        for (var c = 'A'; c <= 'Z'; c++) map[c.ToString()] = VirtualKey.KeyA + (c - 'A');
        for (var f = 1; f <= 24; f++) map["F" + f] = VirtualKey.F1 + (f - 1);
        for (var n = 0; n <= 9; n++) map["NUM" + n] = VirtualKey.NumPad0 + n;
        return map;
    }

    /// <summary>Convierte un código virtual Windows (VK_*) a nuestro enumerado.</summary>
    public static VirtualKey FromVk(int vk) => Enum.IsDefined(typeof(VirtualKey), vk)
        ? (VirtualKey)vk
        : throw new ArgumentException($"Código virtual desconocido: 0x{vk:X}");

    /// <summary>Ancho de código virtual para el enumerado (para SendInput).</summary>
    public static int ToVk(VirtualKey key) => (int)key;

    /// <summary>Parsea un nombre suelto ("Z", "Intro", "F8"). Lanza si es desconocido.</summary>
    public static VirtualKey Parse(string name)
    {
        var clean = name.Trim();
        if (ParseMap.TryGetValue(clean, out var vk)) return vk;
        // Acentos tolerados: "Repágina" no, pero "INTRO"/"Intro" sí. Normalizamos NFD.
        var normalized = clean.Normalize(NormalizationForm.FormD)
            .Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            .Aggregate("", (s, ch) => s + ch)
            .Normalize(NormalizationForm.FormC);
        if (ParseMap.TryGetValue(normalized, out var vk2)) return vk2;
        throw new FormatException($"Tecla desconocida: '{name}'");
    }

    /// <summary>Parsea solo modificadores ("Ctrl", "Alt", "Mayús", "AltGr", "Win").</summary>
    public static VirtualKey ParseModifier(string name)
    {
        var vk = Parse(name);
        return vk switch
        {
            VirtualKey.AltGr or VirtualKey.ControlLeft or VirtualKey.ControlRight
            or VirtualKey.AltLeft or VirtualKey.AltRight
            or VirtualKey.ShiftLeft or VirtualKey.ShiftRight
            or VirtualKey.LWin or VirtualKey.RWin => vk,
            _ => throw new FormatException($"'{name}' no es un modificador válido."),
        };
    }
}
