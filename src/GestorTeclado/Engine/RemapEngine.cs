using GestorTeclado.Core;

namespace GestorTeclado.Engine;

/// <summary>
/// Núcleo del motor: decide, para cada pulsación, si coincide con una regla
/// y qué acción tomar (bloquear la original, emitir la salida). No depende de
/// WinForms ni de P/Invoke: es determinista y unit-testeable.
/// La capa <see cref="LowLevelKeyboardHook"/> lo conecta con Windows.
/// </summary>
public sealed class RemapEngine
{
    /// <summary>Resultado que el hook traduce a "consumir" o "dejar pasar".</summary>
    public enum Verdict { Pass, Block }

    private readonly IKeyboardSender _sender;
    private readonly List<RemapRule> _rules = new();

    // Estado entre pulsaciones (el hook low-level es síncrono y mono-hilo por diseño).
    private readonly HashSet<Guid> _firedThisPress = new();
    private readonly Dictionary<VirtualKey, int> _downScanCodes = new();
    private readonly Dictionary<VirtualKey, bool> _downExtended = new();

    public RemapEngine(IKeyboardSender sender) => _sender = sender;

    public IReadOnlyList<RemapRule> Rules => _rules;

    /// <summary>Activo / Pausado. En Pausado todo pasa sin tocar nada.</summary>
    public bool IsActive { get; set; }

    /// <summary>Modo Prueba: se notan los eventos pero no se aplican reglas.</summary>
    public bool SuspendRulesForTesting { get; set; }

    /// <summary>Evento de telemetría para la pestaña Pruebas (nunca desde UI thread garantizado).</summary>
    public event Action<KeyPress>? KeyObserved;

    /// <summary>Sustituye el conjunto de reglas en caliente (editor/Configuración).</summary>
    public void ReplaceRules(IEnumerable<RemapRule> rules)
    {
        _rules.Clear();
        _rules.AddRange(rules);
        ResetTransientState();
    }

    public void ResetTransientState()
    {
        _firedThisPress.Clear();
        _downScanCodes.Clear();
        _downExtended.Clear();
    }

    /// <summary>Detecta conflictos: dos reglas habilitadas con la misma entrada exacta.</summary>
    public static IReadOnlyList<(RemapRule A, RemapRule B)> FindConflicts(IEnumerable<RemapRule> rules)
    {
        var list = rules.Where(r => r.Enabled).ToList();
        var conflicts = new List<(RemapRule, RemapRule)>();
        for (var i = 0; i < list.Count; i++)
            for (var j = i + 1; j < list.Count; j++)
                if (list[i].Input.Equals(list[j].Input))
                    conflicts.Add((list[i], list[j]));
        return conflicts;
    }

    /// <summary>
    /// Procesa un evento de pulsación (WM_KEYDOWN). Los eventos auto-repetidos
    /// se detectan por _firedThisPress y se tratan en ProcessAutoRepeat.
    /// Devuelve Block si la pulsación debe suprimirse (la acción original no
    /// debe llegar a la aplicación). Nunca lanza: ante fallo del emisor, deja
    /// pasar la tecla para no perder escritura.
    /// </summary>
    public Verdict ProcessKeyDown(KeyPress press)
    {
        // 1) Las salidas generadas por nosotros nunca reactivan reglas (anti-ciclos).
        if (press.IsInjected) return Verdict.Pass;

        // 2) Telemetría siempre disponible para la pestaña Pruebas.
        KeyObserved?.Invoke(press with { EventType = KeyEventType.Pressed });

        if (!IsActive || SuspendRulesForTesting) return Verdict.Pass;

        // 3) Registro del escancode físico mientras la tecla está abajo,
        //    para poder soltarla correctamente si forma parte de una coincidencia.
        _downScanCodes[press.Key] = press.ScanCode;
        _downExtended[press.Key] = press.Extended;

        // 4) Coincidencia exacta sobre la combinación normalizada.
        var combo = press.ToCombination();
        var match = _rules.FirstOrDefault(r => r.Enabled && r.Input.Equals(combo));
        if (match is null) return Verdict.Pass;

        // Repeticiones por mantener pulsada: solo si la regla lo permite.
        if (_firedThisPress.Contains(match.Id))
        {
            if (!match.AllowRepeat) return Verdict.Block;
            return SafeEmit(match);
        }

        _firedThisPress.Add(match.Id);
        return SafeEmit(match);
    }

    public Verdict ProcessKeyUp(KeyPress press)
    {
        if (press.IsInjected) return Verdict.Pass;

        KeyObserved?.Invoke(press with { EventType = KeyEventType.Released });

        if (!IsActive || SuspendRulesForTesting)
        {
            _downScanCodes.Remove(press.Key);
            _downExtended.Remove(press.Key);
            return Verdict.Pass;
        }

        var combo = press.ToCombination();
        var match = _rules.FirstOrDefault(r => r.Enabled && r.Input.Equals(combo));

        // Fin de esa pulsación: se permite volver a disparar al siguiente down.
        if (match is not null) _firedThisPress.Remove(match.Id);

        _downScanCodes.Remove(press.Key);
        _downExtended.Remove(press.Key);

        // Consumir también el keyup de la tecla bloqueada evita que la aplicación
        // vea una liberación "huérfana" (p. ej. Alt pareciendo quedarse presionado).
        return match is not null ? Verdict.Block : Verdict.Pass;
    }

    /// <summary>Procesa transiciones de auto-repetición (WM_KEYDOWN con transition 1).</summary>
    public Verdict ProcessAutoRepeat(KeyPress press)
    {
        if (press.IsInjected) return Verdict.Pass;
        KeyObserved?.Invoke(press with { EventType = KeyEventType.AutoRepeat });
        if (!IsActive || SuspendRulesForTesting) return Verdict.Pass;

        var combo = press.ToCombination();
        var match = _rules.FirstOrDefault(r => r.Enabled && r.Input.Equals(combo));
        if (match is null) return Verdict.Pass;
        if (!match.AllowRepeat) return Verdict.Block;
        return SafeEmit(match);
    }

    private Verdict SafeEmit(RemapRule match)
    {
        try
        {
            return ProcessMatchedDown(match);
        }
        catch
        {
            // Si SendInput falla, mejor dejar pasar la pulsación original
            // que perder la tecla. El hook elevará el estado a Error si persiste.
            return Verdict.Pass;
        }
    }

    private Verdict ProcessMatchedDown(RemapRule match)
    {
        var combo = match.Input;

        // Regla de liberación: cuando la entrada incluye modificadores físicos
        // (p. ej. AltGr = CtrlIzq+AltDer), Windows aún los ve presionados.
        // Para que una salida de texto sea literal (">" y no el efecto AltGr
        // sobre ">") y para que Ctrl no parezca quedarse pulsado, soltamos
        // los modificadores ANTES de emitir la salida. No los volvemos a
        // presionar: el keyup físico real se consumirá en ProcessKeyUp y la
        // aplicación verá un estado limpio (evita "z>" y "Ctrl fantasma").
        var modsToLift = TrackedEntryModifiers(combo);
        foreach (var m in modsToLift)
            _sender.KeyUp(m.key, m.scan, m.ext);

        switch (match.Output.Kind)
        {
            case OutputKind.Text:
                // El texto se envía como Unicode: es un carácter literal,
                // no una tecla simulada (diferencia "c" escrita vs tecla C).
                _sender.SendText(match.Output.Text!);
                break;

            case OutputKind.Key:
            case OutputKind.Combination:
                TapCombination(match.Output.Combination!, exclude: modsToLift);
                break;
        }

        return Verdict.Block; // la acción original no llega
    }

    /// <summary>
    /// Modificadores físicamente abajo que participan en la combinación de
    /// entrada disparada (con su escancode observado). AltGr se expande a
    /// CtrlIzq + AltDer, que es la representación real de Windows.
    /// </summary>
    private List<(VirtualKey key, int scan, bool ext)> TrackedEntryModifiers(KeyCombination input)
    {
        var wanted = new HashSet<VirtualKey>();
        foreach (var m in input.Modifiers)
        {
            if (m == VirtualKey.AltGr)
            {
                wanted.Add(VirtualKey.ControlLeft);
                wanted.Add(VirtualKey.AltRight);
            }
            else wanted.Add(m);
        }

        var result = new List<(VirtualKey, int, bool)>();
        foreach (var k in wanted)
            if (_downScanCodes.TryGetValue(k, out var scan))
                result.Add((k, scan, _downExtended.TryGetValue(k, out var e) && e));
        return result;
    }

    private void TapCombination(KeyCombination combo, IReadOnlyCollection<(VirtualKey key, int scan, bool ext)> exclude)
    {
        // Enviamos primero los modificadores de la SALIDA (down), luego la base
        // (tap) y después soltamos los nuestros (up) en orden inverso. Si un
        // modificador de la salida coincide con uno que acabamos de levantar
        // físicamente, lo re-pulsamos aquí para completar el atajo.
        foreach (var m in combo.Modifiers)
            _sender.KeyDown(m, ScanOf(m), ExtendedOf(m));
        _sender.TapKey(combo.BaseKey, ScanOf(combo.BaseKey), ExtendedOf(combo.BaseKey));
        foreach (var m in Enumerable.Reverse(combo.Modifiers))
            _sender.KeyUp(m, ScanOf(m), ExtendedOf(m));
    }

    private int ScanOf(VirtualKey k) =>
        _downScanCodes.TryGetValue(k, out var s) ? s : MapVirtualKeyDefaults.Scancode(k);

    private bool ExtendedOf(VirtualKey k) =>
        _downExtended.TryGetValue(k, out var e) ? e : MapVirtualKeyDefaults.IsExtended(k);
}
