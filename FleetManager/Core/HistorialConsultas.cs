using FleetManager.Models;

namespace FleetManager.Core;

/// <summary>Un día de una jornada, con sus trayectos.</summary>
public sealed record DiaHistorial(DateTime Fecha, IReadOnlyList<Trayecto> Trayectos)
{
    public double Kilometros => Trayectos.Sum(t => t.Kilometros);

    public int Faltas => Trayectos.Sum(HistorialConsultas.Faltas);
}

/// <summary>Totales de un grupo de trayectos.</summary>
public sealed record ResumenTrayectos(int Trayectos, double Kilometros, TimeSpan TiempoConduccion, int Faltas);

/// <summary>
/// Consultas sobre el historial. Los días no se guardan: se obtienen agrupando los
/// trayectos por la fecha (del juego) en que empiezan.
/// </summary>
public static class HistorialConsultas
{
    /// <summary>Faltas de un trayecto (0, 1 o 2).</summary>
    public static int Faltas(Trayecto trayecto) =>
        (trayecto.FaltaVelocidad ? 1 : 0) + (trayecto.FaltaConduccion ? 1 : 0);

    /// <summary>Días de una jornada, en orden, con sus trayectos terminados en orden.</summary>
    public static IReadOnlyList<DiaHistorial> Dias(Jornada jornada) =>
        jornada.Trayectos
            .GroupBy(t => t.Inicio.Date)
            .OrderBy(g => g.Key)
            .Select(g => new DiaHistorial(g.Key, g.OrderBy(t => t.Inicio).ToList()))
            .ToList();

    public static ResumenTrayectos Resumir(IEnumerable<Trayecto> trayectos)
    {
        var lista = trayectos.ToList();

        return new ResumenTrayectos(
            lista.Count,
            lista.Sum(t => t.Kilometros),
            TimeSpan.FromTicks(lista.Sum(t => t.TiempoConduccion.Ticks)),
            lista.Sum(Faltas));
    }
}
