using System.Text.Json.Serialization;

namespace FleetManager.Models;

/// <summary>
/// Un tramo del registro del tacógrafo: una actividad entre dos horas del juego.
/// </summary>
public sealed class PeriodoActividad
{
    public Actividad Actividad { get; set; }

    public DateTime Inicio { get; set; }

    public DateTime Fin { get; set; }

    [JsonIgnore]
    public TimeSpan Duracion => Fin - Inicio;
}
