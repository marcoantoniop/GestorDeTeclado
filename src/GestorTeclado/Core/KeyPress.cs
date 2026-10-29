namespace GestorTeclado.Core;

/// <summary>
/// Estado de una pulsación capturada por el motor o por la pestaña Pruebas.
/// </summary>
public enum KeyEventType
{
    Pressed,      // presionada
    Released,     // soltada
    AutoRepeat,   // repetida por mantenerla pulsada (transición FDown->FRepeat)
}

/// <summary>
/// Descripción normalizada de una pulsación física recibida de Windows:
/// tecla virtual, escancode, flag extendido y modificadores presentes.
/// Es el "verbo" del sistema: tanto la entrada de una regla como el
/// resultado de la pestaña Pruebas se expresan con este tipo.
/// </summary>
public sealed record KeyPress
{
    public required VirtualKey Key { get; init; }

    /// <summary>Código de tecla (scan code) físico, incluido el bit 0xE0 si es extendido.</summary>
    public int ScanCode { get; init; }

    /// <summary>Flag LLKHF_EXTENDED del evento low-level.</summary>
    public bool Extended { get; init; }

    /// <summary>Ctrl izquierdo activo (AltGr lo implica en teclados ES).</summary>
    public bool CtrlDown { get; init; }

    /// <summary>Alt derecho activo (componente real de AltGr junto a Ctrl izq.).</summary>
    public bool AltRightDown { get; init; }

    /// <summary>Alt izquierdo activo.</summary>
    public bool AltLeftDown { get; init; }

    public bool ShiftDown { get; init; }

    /// <summary>Windows (izq. o der.) activo.</summary>
    public bool WinDown { get; init; }

    /// <summary>
    /// true si esta pulsación fue generada por Gestor de Teclado mediante SendInput.
    /// El motor la ignora para evitar ciclos (una salida nunca reactiva otras reglas).
    /// Se marca con el tag de usuario del EVENTMSG/MSLLHOOKSTRUCT (dwExtraInfo).
    /// </summary>
    public bool IsInjected { get; init; }

    public KeyEventType EventType { get; init; } = KeyEventType.Pressed;

    /// <summary>Verdadero si Alt + Ctrl simultáneos = AltGr según representación de Windows.</summary>
    public bool IsAltGr => CtrlDown && AltRightDown && !AltLeftDown;

    /// <summary>Alt efectivo (cualquiera de los dos lados).</summary>
    public bool AltDown => AltLeftDown || AltRightDown;

    /// <summary>
    /// Clave canónica para comparar entradas de reglas: normaliza AltGr
    /// (CtrlIzq+AltDer -> AltGr), trata Ctrl/Shift/Alt/Win sin distinguir
    /// lado izquierdo/derecho (a menos que la regla se capture con lado exacto)
    /// y descarta el estado auto-repetido.
    /// </summary>
    public KeyCombination ToCombination()
    {
        var mods = new SortedSet<VirtualKey>();
        if (IsAltGr)
        {
            // Windows representa AltGr como LControl(0xA2)+RMenu(0xA5) extended.
            mods.Add(VirtualKey.AltGr);
        }
        else
        {
            if (CtrlDown) mods.Add(VirtualKey.ControlLeft);   // normalizado a "Ctrl"
            if (AltLeftDown) mods.Add(VirtualKey.AltLeft);    // normalizado a "Alt"
            if (AltRightDown) mods.Add(VirtualKey.AltRight);  // normalizado a "Alt"
        }
        if (ShiftDown) mods.Add(VirtualKey.ShiftLeft);        // normalizado a "Shift"
        if (WinDown) mods.Add(VirtualKey.LWin);               // normalizado a "Win"

        return new KeyCombination(Key, mods.Select(KeyNormalizer.Normalize).ToList());
    }
}

/// <summary>
/// Agrupa los lados de un modificador para que "CtrlIzq" y "CtrlDer"
/// signifiquen lo mismo en una combinación por defecto (coincidencia exacta
/// sobre la tecla base, flexible sobre modificadores equivalentes).
/// </summary>
public static class KeyNormalizer
{
    public static VirtualKey Normalize(VirtualKey k) => k switch
    {
        VirtualKey.ControlRight => VirtualKey.ControlLeft,
        VirtualKey.AltRight => VirtualKey.AltLeft,
        VirtualKey.ShiftRight => VirtualKey.ShiftLeft,
        VirtualKey.RWin => VirtualKey.LWin,
        _ => k,
    };
}
