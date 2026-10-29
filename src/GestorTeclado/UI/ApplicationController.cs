using GestorTeclado.Core;

namespace GestorTeclado.UI;

/// <summary>
/// Fachada de aplicación: une motor, almacenamiento e interfaz sin mezclar
/// responsabilidades. El formulario solo llama a métodos y se suscribe a
/// eventos; toda la lógica de estado vive aquí (testeable sin WinForms).
/// </summary>
public sealed class ApplicationController : IDisposable
{
    private readonly Engine.RemapEngine _engine;
    private readonly Engine.LowLevelKeyboardHook? _hook;      // null fuera de Windows
    private readonly Engine.IKeyboardSender _sender;
    private readonly Storage.ConfigStore _store;
    private Storage.ConfigDocument _doc;

    public event Action? StateChanged;
    public event Action? RulesChanged;

    public EngineState State { get; private set; } = EngineState.Paused;
    public string? LastError { get; private set; }
    public Storage.ConfigDocument Document => _doc;
    public int EnabledRuleCount => _doc.Rules.Count(r => r.Enabled);
    public Engine.RemapEngine Engine => _engine;
    public bool IsWindowsPlatform => OperatingSystem.IsWindows();

    /// <summary>Modo "Probar entrada original": suspende reglas mientras dura la captura.</summary>
    public bool TestingOriginalInput { get; set; }

    public ApplicationController(Storage.ConfigStore? store = null)
    {
        _store = store ?? new Storage.ConfigStore();
        _sender = OperatingSystem.IsWindows()
            ? new Engine.SendInputKeyboardSender()
            : new Engine.NullKeyboardSender(); // permite ejecutar la UI en otros SO para desarrollo
        _engine = new Engine.RemapEngine(_sender);

        var load = _store.Load();
        if (load.Ok && load.Config is not null)
        {
            _doc = load.Config;
        }
        else
        {
            // Archivo dañado o inexistente: conservamos/creamos configuración válida
            // y explicamos el error (requisito 4).
            LastError = load.Error;
            _doc = new Storage.ConfigDocument();
        }
        _engine.ReplaceRules(_doc.Rules);

        if (OperatingSystem.IsWindows())
        {
            _hook = new Engine.LowLevelKeyboardHook(_engine);
            _hook.EmitFailure += ex => SetError(ex.Message);
        }
    }

    /// <summary>Inicia según preferencias ("Estado al iniciar"). Devuelve false si el hook falló.</summary>
    public bool Start()
    {
        try
        {
            _hook?.Install();
            SetState(_doc.Settings.StateOnStartup == EngineState.Active ? EngineState.Active : EngineState.Paused);
            return true;
        }
        catch (Exception ex)
        {
            SetError("No se pudo activar el motor; requiere revisión. " + ex.Message);
            return false;
        }
    }

    public void Activate() => SetState(EngineState.Active);
    public void Pause() => SetState(EngineState.Paused);

    public void ToggleActive()
    {
        if (State == EngineState.Active) Pause();
        else Activate();
    }

    /// <summary>Atajo de pausa definido en Configuración (si existe).</summary>
    public bool HandlePauseHotkey(KeyPress press)
    {
        var hotkey = _doc.Settings.PauseHotkey;
        if (hotkey is null) return false;
        if (hotkey.Equals(press.ToCombination()))
        {
            ToggleActive();
            return true;
        }
        return false;
    }

    private void SetState(EngineState newState)
    {
        State = newState;
        _engine.IsActive = newState is EngineState.Active or EngineState.Testing;
        _engine.SuspendRulesForTesting = TestingOriginalInput && newState == EngineState.Testing;
        if (newState != EngineState.Error) LastError = null;
        StateChanged?.Invoke();
    }

    public void BeginTestSession(bool testOriginalInput)
    {
        TestingOriginalInput = testOriginalInput;
        State = EngineState.Testing;
        _engine.IsActive = true;
        _engine.SuspendRulesForTesting = testOriginalInput;
        StateChanged?.Invoke();
    }

    public void EndTestSession()
    {
        TestingOriginalInput = false;
        _engine.SuspendRulesForTesting = false;
        SetState(State == EngineState.Testing ? EngineState.Paused : State);
        if (_doc.Settings.StateOnStartup == EngineState.Active && State != EngineState.Error)
            SetState(EngineState.Active);
    }

    private void SetError(string message)
    {
        LastError = message;
        State = EngineState.Error;
        _engine.IsActive = false;
        StateChanged?.Invoke();
    }

    // ---------- Reglas ----------

    /// <summary>Valida duplicados activos y conflictos antes de insertar/reemplazar.</summary>
    public (bool Ok, string? Error) TrySetRule(RemapRule rule, Guid? replaceId = null)
    {
        var others = _doc.Rules.Where(r => r.Id != replaceId && r.Id != rule.Id).ToList();
        if (rule.Enabled && others.Any(r => r.Enabled && r.Input.Equals(rule.Input)))
            return (false, $"La entrada “{rule.Input}” ya está asignada a otra regla habilitada.");

        if (replaceId is Guid id)
        {
            var idx = _doc.Rules.FindIndex(r => r.Id == id);
            if (idx >= 0) _doc.Rules[idx] = rule; else _doc.Rules.Add(rule);
        }
        else
        {
            _doc.Rules.Add(rule);
        }
        ApplyRulesToEngine();
        SaveQuietly();
        RulesChanged?.Invoke();
        return (true, null);
    }

    public void RemoveRule(Guid id)
    {
        _doc.Rules.RemoveAll(r => r.Id == id);
        ApplyRulesToEngine();
        SaveQuietly();
        RulesChanged?.Invoke();
    }

    public void SetRuleEnabled(Guid id, bool enabled)
    {
        var rule = _doc.Rules.FirstOrDefault(r => r.Id == id);
        if (rule is null) return;
        if (enabled)
        {
            var clash = _doc.Rules.FirstOrDefault(r => r.Id != id && r.Enabled && r.Input.Equals(rule.Input));
            if (clash is not null)
            {
                StateChanged?.Invoke();
                return; // el editor debe informar del conflicto; no lo habilitamos
            }
        }
        rule.Enabled = enabled;
        ApplyRulesToEngine();
        SaveQuietly();
        RulesChanged?.Invoke();
    }

    /// <summary>Conflictos detectables entre reglas habilitadas (mostrar en el editor).</summary>
    public IReadOnlyList<(RemapRule A, RemapRule B)> DetectConflicts() =>
        Engine.RemapEngine.FindConflicts(_doc.Rules);

    private void ApplyRulesToEngine() => _engine.ReplaceRules(_doc.Rules);

    // ---------- Persistencia ----------

    /// <summary>Guardado automático de cambios confirmados. No lanza: informa vía LastError.</summary>
    public void SaveQuietly()
    {
        try { _store.Save(_doc); }
        catch (Exception ex) { LastError = "No se pudo guardar: " + ex.Message; }
    }

    public Storage.LoadResult LoadFromFile(string path)
    {
        var res = _store.LoadFrom(path);
        if (res.Ok && res.Config is not null)
        {
            _doc = res.Config;
            ApplyRulesToEngine();
            SaveQuietly(); // confirmar la carga reescribiendo el archivo estándar
            RulesChanged?.Invoke();
            StateChanged?.Invoke();
        }
        return res;
    }

    public void ExportTo(string path) => _store.ExportTo(_doc, path);

    public void RestoreDefaults()
    {
        _doc = new Storage.ConfigDocument();
        ApplyRulesToEngine();
        SaveQuietly();
        RulesChanged?.Invoke();
        StateChanged?.Invoke();
    }

    public void UpdateSettings(Action<Core.AppSettings> mutate)
    {
        mutate(_doc.Settings);
        AutoStartRegistry.Apply(_doc.Settings.StartWithWindows);
        SaveQuietly();
        StateChanged?.Invoke();
    }

    public void Dispose()
    {
        _hook?.Dispose();
    }
}
