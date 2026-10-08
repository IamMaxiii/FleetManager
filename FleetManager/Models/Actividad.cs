using System.Text.Json.Serialization;

namespace FleetManager.Models;

/// <summary>
/// Las cuatro actividades del tacógrafo. En los archivos se guardan por su
/// nombre ("Conduccion"), no como número, para que se entiendan al abrirlos.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<Actividad>))]
public enum Actividad
{
    Descanso,
    Disponibilidad,
    OtrosTrabajos,
    Conduccion
}
