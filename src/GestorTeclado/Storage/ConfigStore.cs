using System.Text.Json;
using System.Text.Json.Serialization;
using GestorTeclado.Core;

namespace GestorTeclado.Storage;

/// <summary>
/// Modelo de datos tal como se persiste en JSON. Las cadenas legibles
/// ("AltGr + Z", "Ctrl + C") hacen el archivo transportable y editable a mano.
/// </summary>
internal sealed class StoredConfig
{
    public int FormatVersion { get; set; } = AppSettings.CurrentFormatVersion;
    public StoredSettings? Settings { get; set; }
    public List<StoredRule> Rules { get; set; } = new();
}

internal sealed class StoredSettings
{
    public bool StartWithWindows { get; set; }
    public bool StartMinimized { get; set; } = true;
    public string StateOnStartup { get; set; } = nameof(EngineState.Active);
    public bool CloseToTray { get; set; } = true;
    public string? PauseHotkey { get; set; }
}

internal sealed class StoredRule
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public bool Enabled { get; set; } = true;
    public bool AllowRepeat { get; set; }
    public required string Input { get; set; }

    /// <summary>"text" | "key" | "combination"</summary>
    public required string OutputKind { get; set; }

    /// <summary>Contenido literal para outputKind=text.</summary>
    public string? OutputText { get; set; }

    /// <summary>Combinación legible para outputKind=key|combination.</summary>
    public string? OutputCombination { get; set; }
}

/// <summary>Resultado de una carga: configuración válida o error explicable.</summary>
public sealed record LoadResult(bool Ok, ConfigDocument? Config, string? Error);

/// <summary>Documento completo en memoria (reglas + preferencias).</summary>
public sealed class ConfigDocument
{
    public AppSettings Settings { get; set; } = new();
    public List<RemapRule> Rules { get; set; } = new();
}

/// <summary>
/// Guarda/carga reglas y preferencias en JSON (sin base de datos).
/// Un archivo dañado no borra la configuración anterior: Load devuelve el
/// error y el llamador decide; Save escribe de forma atómica (.tmp + replace).
/// </summary>
public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "GestorTeclado", "config.json");

    private readonly string _path;
    public ConfigStore(string? path = null) => _path = path ?? DefaultPath;

    // ---------- Conversión modelo <-> almacenamiento ----------

    internal static StoredConfig ToStored(ConfigDocument doc) => new()
    {
        FormatVersion = AppSettings.CurrentFormatVersion,
        Settings = new StoredSettings
        {
            StartWithWindows = doc.Settings.StartWithWindows,
            StartMinimized = doc.Settings.StartMinimized,
            StateOnStartup = doc.Settings.StateOnStartup.ToString(),
            CloseToTray = doc.Settings.CloseToTray,
            PauseHotkey = doc.Settings.PauseHotkey?.ToString(),
        },
        Rules = doc.Rules.Select(r => new StoredRule
        {
            Id = r.Id,
            Name = r.Name,
            Enabled = r.Enabled,
            AllowRepeat = r.AllowRepeat,
            Input = r.Input.ToString(),
            OutputKind = r.Output.Kind switch
            {
                OutputKind.Text => "text",
                OutputKind.Key => "key",
                OutputKind.Combination => "combination",
                _ => "text",
            },
            OutputText = r.Output.Kind == OutputKind.Text ? r.Output.Text : null,
            OutputCombination = r.Output.Kind != OutputKind.Text ? r.Output.Combination!.ToString() : null,
        }).ToList(),
    };

    internal static ConfigDocument FromStored(StoredConfig stored)
    {
        if (stored.FormatVersion > AppSettings.CurrentFormatVersion)
            throw new NotSupportedException(
                $"El archivo usa formato v{stored.FormatVersion}, pero esta versión del programa soporta hasta v{AppSettings.CurrentFormatVersion}. Actualice Gestor de Teclado.");

        var settings = new AppSettings
        {
            StartWithWindows = stored.Settings?.StartWithWindows ?? false,
            StartMinimized = stored.Settings?.StartMinimized ?? true,
            CloseToTray = stored.Settings?.CloseToTray ?? true,
            StateOnStartup = Enum.TryParse<EngineState>(stored.Settings?.StateOnStartup, ignoreCase: true, out var st)
                ? st : EngineState.Active,
            PauseHotkey = string.IsNullOrWhiteSpace(stored.Settings?.PauseHotkey)
                ? null : KeyCombination.Parse(stored.Settings!.PauseHotkey!),
        };

        var rules = new List<RemapRule>();
        foreach (var sr in stored.Rules)
        {
            var input = KeyCombination.Parse(sr.Input);
            RuleOutput output = sr.OutputKind switch
            {
                "text" => RuleOutput.FromText(sr.OutputText
                    ?? throw new InvalidDataException($"Regla '{sr.Name ?? sr.Id}': salida de texto vacía.")),
                "key" => RuleOutput.FromKey(KeyCombination.Parse(sr.OutputCombination
                    ?? throw new InvalidDataException($"Regla '{sr.Name ?? sr.Id}': falta la combinación.")).BaseKey),
                "combination" => RuleOutput.FromCombination(KeyCombination.Parse(sr.OutputCombination
                    ?? throw new InvalidDataException($"Regla '{sr.Name ?? sr.Id}': falta la combinación."))),
                _ => throw new InvalidDataException($"Regla '{sr.Name ?? sr.Id}': tipo de salida desconocido '{sr.OutputKind}'."),
            };
            rules.Add(new RemapRule
            {
                Name = sr.Name,
                Enabled = sr.Enabled,
                AllowRepeat = sr.AllowRepeat,
                Input = input,
                Output = output,
            });
        }
        return new ConfigDocument { Settings = settings, Rules = rules };
    }

    // ---------- API pública ----------

    /// <summary>Guarda de forma atómica. Lanza IOException con mensaje útil si falla.</summary>
    public void Save(ConfigDocument doc)
    {
        var dir = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(dir);
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(ToStored(doc), JsonOpts));
        File.Move(tmp, _path, overwrite: true); // reemplazo atómico en el mismo volumen
    }

    /// <summary>Carga desde la ruta por defecto del programa.</summary>
    public LoadResult Load() => LoadFrom(_path);

    /// <summary>
    /// Carga validando el archivo. Si está dañado, Ok=false y Error explica
    /// el problema; el llamador conserva la configuración válida anterior.
    /// </summary>
    public LoadResult LoadFrom(string path)
    {
        try
        {
            if (!File.Exists(path))
                return new LoadResult(false, null, $"No existe el archivo: {path}");
            var stored = JsonSerializer.Deserialize<StoredConfig>(File.ReadAllText(path), JsonOpts)
                ?? throw new InvalidDataException("El archivo está vacío o no es un objeto JSON válido.");
            return new LoadResult(true, FromStored(stored), null);
        }
        catch (JsonException ex)
        {
            return new LoadResult(false, null, $"JSON inválido: {ex.Message}");
        }
        catch (Exception ex) when (ex is InvalidDataException or FormatException
                                       or NotSupportedException or ArgumentException)
        {
            return new LoadResult(false, null, ex.Message);
        }
        catch (IOException ex)
        {
            return new LoadResult(false, null, $"Error de lectura/escritura: {ex.Message}");
        }
    }

    /// <summary>Exporta una copia transportable (mismo formato, otra ruta).</summary>
    public void ExportTo(ConfigDocument doc, string destination)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))!);
        File.WriteAllText(destination, JsonSerializer.Serialize(ToStored(doc), JsonOpts));
    }

    /// <summary>Configuración vacía para "Restaurar valores iniciales".</summary>
    public static ConfigDocument CreateDefault() => new();
}
