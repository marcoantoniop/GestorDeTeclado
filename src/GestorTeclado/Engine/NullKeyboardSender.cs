using GestorTeclado.Core;

namespace GestorTeclado.Engine;

/// <summary>
/// Emisor inerte para ejecutar la interfaz fuera de Windows (desarrollo/tests).
/// Registra las acciones en memoria para poder verificarlas unitariamente.
/// </summary>
public sealed class NullKeyboardSender : IKeyboardSender
{
    public List<string> Log { get; } = new();

    public void TapKey(VirtualKey key, int scanCode, bool extended) =>
        Log.Add($"tap {KeyNames.Display(key)}");

    public void KeyDown(VirtualKey key, int scanCode, bool extended) =>
        Log.Add($"down {KeyNames.Display(key)}");

    public void KeyUp(VirtualKey key, int scanCode, bool extended) =>
        Log.Add($"up {KeyNames.Display(key)}");

    public void SendText(string text) => Log.Add($"text “{text}”");

    public void ReleaseStuckModifiers(bool ctrl, bool alt, bool shift, bool win) =>
        Log.Add("release-stuck");
}
