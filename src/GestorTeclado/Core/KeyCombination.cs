using System.Text.Json.Serialization;

namespace GestorTeclado.Core;

/// <summary>
/// Combinación de tecla: una tecla base + modificadores.
/// Inmutable, comparable y serializable en JSON como cadena legible
/// ("AltGr + Z", "Ctrl + Alt + M").
/// </summary>
public sealed class KeyCombination : IEquatable<KeyCombination>
{
    [JsonConstructor]
    public KeyCombination(VirtualKey baseKey, IReadOnlyList<VirtualKey> modifiers)
    {
        BaseKey = baseKey;
        // Normaliza y ordena determinísticamente para igualdad estable.
        Modifiers = modifiers
            .Select(KeyNormalizer.Normalize)
            .Where(m => m != baseKey)          // un modificador no puede ser también la base
            .Distinct()
            .OrderBy(m => (int)m)
            .ToList();
    }

    /// <summary>Combinación sin modificadores a partir de una sola tecla.</summary>
    public static KeyCombination FromKey(VirtualKey key) => new(key, Array.Empty<VirtualKey>());

    public VirtualKey BaseKey { get; }
    public IReadOnlyList<VirtualKey> Modifiers { get; }

    public bool Equals(KeyCombination? other) =>
        other is not null && BaseKey == other.BaseKey && Modifiers.SequenceEqual(other.Modifiers);

    public override bool Equals(object? obj) => Equals(obj as KeyCombination);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(BaseKey);
        foreach (var m in Modifiers) hash.Add(m);
        return hash.ToHashCode();
    }

    /// <summary>Ej.: "AltGr + Z". La tecla base siempre va al final.</summary>
    public override string ToString()
    {
        var parts = Modifiers.Select(KeyNames.Display).Append(KeyNames.Display(BaseKey));
        return string.Join(" + ", parts);
    }

    /// <summary>
    /// Coincidencia exacta por defecto: si el usuario añade Shift además de
    /// AltGr sobre la misma tecla, esta combinación NO iguala a "AltGr + Z".
    /// </summary>
    public bool ExactMatch(KeyCombination candidate) => Equals(candidate);

    /// <summary>Parsing desde la forma textual mostrada al usuario.</summary>
    public static KeyCombination Parse(string text)
    {
        var parts = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) throw new FormatException($"Combinación vacía: '{text}'");
        var mods = parts[..^1].Select(KeyNames.ParseModifier).ToList();
        var baseKey = KeyNames.Parse(parts[^1]);
        return new KeyCombination(baseKey, mods);
    }
}
