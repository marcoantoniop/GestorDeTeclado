using System.Text.Json.Serialization;

namespace GestorTeclado.Core;

/// <summary>
/// Regla de reasignación: entrada (combinación) → salida (texto/tecla/combinación).
/// </summary>
public sealed class RemapRule
{
    public RemapRule() { }

    [JsonConstructor]
    public RemapRule(Guid id, string? name, bool enabled, KeyCombination input, RuleOutput output, bool allowRepeat)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        Name = name;
        Enabled = enabled;
        Input = input ?? throw new ArgumentNullException(nameof(input));
        Output = output ?? throw new ArgumentNullException(nameof(output));
        AllowRepeat = allowRepeat;
    }

    /// <summary>Identificador estable para referencias internas y historial.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Nombre opcional mostrado en la lista.</summary>
    public string? Name { get; set; }

    /// <summary>Casilla de habilitación.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Entrada: tecla o combinación que se captura físicamente.</summary>
    public KeyCombination Input { get; set; } = KeyCombination.FromKey(VirtualKey.Escape);

    /// <summary>Salida configurada.</summary>
    public RuleOutput Output { get; set; } = RuleOutput.FromText("");

    /// <summary>
    /// Opción de repetición: si es true, mantener la pulsación repite la salida
    /// (útil para texto corto o Intro). Si es false, solo se emite una vez por pulsación.
    /// </summary>
    public bool AllowRepeat { get; set; }

    /// <summary>
    /// Coincidencia exacta por defecto: "AltGr + Z" no se ejecuta si además
    /// se mantiene Mayús. La comparación usa el estado normalizado de modificadores.
    /// </summary>
    public bool Matches(KeyPress press) =>
        Enabled && Input.ExactMatch(press.ToCombination());

    public override string ToString() => $"{Input}  →  {Output.Describe()}";
}
