namespace GestorTeclado.Engine;

/// <summary>
/// Abstracción del envío de eventos al sistema. Permite sustituir la capa
/// real (SendInput) en pruebas unitarias y mantiene el motor independiente
/// de la interfaz, según la separación de responsabilidades propuesta.
/// </summary>
public interface IKeyboardSender
{
    /// <summary>Envía una pulsación completa (down+up) de una tecla con su escancode.</summary>
    void TapKey(VirtualKey key, int scanCode, bool extended);

    /// <summary>Estado de presión sostenido (para mantener modificadores mientras se escribe texto).</summary>
    void KeyDown(VirtualKey key, int scanCode, bool extended);
    void KeyUp(VirtualKey key, int scanCode, bool extended);

    /// <summary>
    /// Inserta texto literal como eventos Unicode (KEYEVENTF_UNICODE), sin
    /// depender de la distribución activa. Es la diferencia entre "escribir c"
    /// y "enviar la tecla C".
    /// </summary>
    void SendText(string text);

    /// <summary>Suelta modificadores que quedaron presionados por coincidencias parciales.</summary>
    void ReleaseStuckModifiers(bool ctrl, bool alt, bool shift, bool win);
}
