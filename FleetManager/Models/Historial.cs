using System.Text.Json.Serialization;

namespace FleetManager.Models;

/// <summary>
/// Todas las jornadas guardadas. Se guarda en historial.json.
/// No tiene límite de tamaño: nunca se borran jornadas automáticamente.
/// </summary>
public sealed class Historial
{
    public List<Jornada> Jornadas { get; set; } = [];

    [JsonIgnore]
    public int NumeroTrayectos => Jornadas.Sum(j => j.Trayectos.Count);

    public int SiguienteNumeroJornada() =>
        Jornadas.Count == 0 ? 1 : Jornadas.Max(j => j.Numero) + 1;
}
