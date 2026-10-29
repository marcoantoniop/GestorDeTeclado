namespace GestorTeclado.Core;

/// <summary>Estado global del motor, reflejado en el icono de bandeja.</summary>
public enum EngineState
{
    /// <summary>Reasignaciones funcionando.</summary>
    Active,
    /// <summary>Comportamiento normal del teclado conservado.</summary>
    Paused,
    /// <summary>Sesión de prueba abierta (el hook está instalado pero las reglas se suspenden).</summary>
    Testing,
    /// <summary>No se pudo activar el motor; requiere revisión.</summary>
    Error,
}

/// <summary>Preferencias de la aplicación (pestaña Configuración).</summary>
public sealed class AppSettings
{
    /// <summary>Iniciar con Windows (clave Run del registro del usuario actual).</summary>
    public bool StartWithWindows { get; set; }

    /// <summary>Abrir directamente minimizado en la bandeja.</summary>
    public bool StartMinimized { get; set; } = true;

    /// <summary>Estado al iniciar: activo o pausado.</summary>
    public EngineState StateOnStartup { get; set; } = EngineState.Active;

    /// <summary>La X oculta la ventana; "Salir" termina el programa.</summary>
    public bool CloseToTray { get; set; } = true;

    /// <summary>Atajo para desactivar reasignaciones rápidamente. Null = deshabilitado.</summary>
    public KeyCombination? PauseHotkey { get; set; }

    /// <summary>Versión del formato de archivo. Se incrementa al ampliar el esquema.</summary>
    public const int CurrentFormatVersion = 1;
}
