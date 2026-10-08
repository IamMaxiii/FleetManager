namespace FleetManager.Models;

/// <summary>
/// Recorrido de una jornada, para dibujarlo en el mapa. Cada jornada tiene el suyo en
/// recorridos\&lt;id de la jornada&gt;.json.
///
/// Cada tramo es una línea continua de puntos [X, Z, minuto]: X y Z son coordenadas
/// del mundo del juego (redondeadas a metros) y minuto, los minutos de juego desde el
/// inicio de la jornada (sirve para resaltar un día o un trayecto en el historial).
/// Se empieza un tramo nuevo cuando la posición salta (ferri, cambio de partida).
/// </summary>
public sealed class MapaRecorrido
{
    public List<List<int[]>> Tramos { get; set; } = [];
}
