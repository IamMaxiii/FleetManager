namespace FleetManager.Storage;

/// <summary>
/// Envoltorio de cada archivo JSON: lleva el número de formato para poder
/// adaptar los datos si el formato cambia en versiones futuras.
/// </summary>
public sealed class DocumentoJson<T> where T : class
{
    public int Version { get; set; }

    /// <summary>Fecha y hora real (no del juego) del guardado.</summary>
    public DateTimeOffset GuardadoEn { get; set; }

    public T? Datos { get; set; }
}
