using System.Text.Json.Serialization;

namespace GestorTeclado.Core;

/// <summary>Tipo de salida de una regla.</summary>
public enum OutputKind
{
    /// <summary>Inserta texto literal mediante caracteres UTF-16 (Unicode). No simula teclas.</summary>
    Text,
    /// <summary>Envía una pulsación de tecla (conserva su función: Intro, Tab, F5…).</summary>
    Key,
    /// <summary>Envía un atajo con modificadores (Ctrl+C, Alt+Tab…).</summary>
    Combination,
}

/// <summary>
/// Salida de una regla. Discriminada por <see cref="Kind"/>:
/// - Text: usa <see cref="Text"/>.
/// - Key/Combination: usa <see cref="Combination"/>.
/// Importante: insertar la letra "c" y enviar la tecla C son operaciones distintas.
/// </summary>
public sealed class RuleOutput
{
    [JsonConstructor]
    public RuleOutput(OutputKind kind, string? text, KeyCombination? combination)
    {
        Kind = kind;
        Text = text;
        Combination = combination;
        Validate();
    }

    public static RuleOutput FromText(string text) => new(OutputKind.Text, text, null);
    public static RuleOutput FromKey(VirtualKey key) => new(OutputKind.Key, null, KeyCombination.FromKey(key));
    public static RuleOutput FromCombination(KeyCombination combo) => new(OutputKind.Combination, null, combo);

    public OutputKind Kind { get; }
    public string? Text { get; }
    public KeyCombination? Combination { get; }

    private void Validate()
    {
        switch (Kind)
        {
            case OutputKind.Text:
                if (string.IsNullOrEmpty(Text))
                    throw new ArgumentException("La salida de texto no puede estar vacía.");
                break;
            case OutputKind.Key:
                if (Combination is null || Combination.Modifiers.Count > 0)
                    throw new ArgumentException("La salida 'tecla' debe ser una única tecla sin modificadores.");
                break;
            case OutputKind.Combination:
                if (Combination is null)
                    throw new ArgumentException("La salida 'combinación' requiere al menos un modificador y una tecla base.");
                if (Combination.Modifiers.Count == 0)
                    throw new ArgumentException("Una combinación necesita al menos un modificador; use el tipo 'tecla'.");
                break;
        }
    }

    public string Describe() => Kind switch
    {
        OutputKind.Text => $"Texto: “{Text}”",
        OutputKind.Key => $"Tecla: {Combination}",
        OutputKind.Combination => $"Combinación: {Combination}",
        _ => "?",
    };
}
