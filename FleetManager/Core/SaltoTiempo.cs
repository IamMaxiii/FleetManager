using System.Globalization;

namespace FleetManager.Core;

/// <summary>
/// Cuentas para saltar el tiempo del juego con el comando de la consola
/// <c>g_set_time &lt;horas&gt; [minutos]</c>. Ese comando pone una hora del día y el
/// juego <b>solo avanza</b> hasta la próxima vez que sea esa hora, así que cualquier
/// salto de menos de 24 h se puede hacer con una sola orden.
/// </summary>
public static class SaltoTiempo
{
    /// <summary>Hora del juego tras saltar <paramref name="duracion"/> (menos de 24 h).</summary>
    public static DateTime Objetivo(DateTime ahora, TimeSpan duracion)
    {
        if (duracion <= TimeSpan.Zero || duracion >= TimeSpan.FromDays(1))
        {
            throw new ArgumentOutOfRangeException(nameof(duracion), "El salto debe ser de más de 0 y menos de 24 horas.");
        }

        return QuitarSegundos(ahora + duracion);
    }

    /// <summary>La próxima vez que sean las <paramref name="horaDelDia"/> (hoy si aún no ha llegado; si no, mañana).</summary>
    public static DateTime ObjetivoHasta(DateTime ahora, TimeSpan horaDelDia)
    {
        DateTime hoy = ahora.Date + horaDelDia;
        return hoy > ahora ? hoy : hoy.AddDays(1);
    }

    /// <summary>Texto que hay que escribir en la consola del juego.</summary>
    public static string Comando(DateTime objetivo) =>
        string.Create(CultureInfo.InvariantCulture, $"g_set_time {objetivo.Hour} {objetivo.Minute}");

    private static DateTime QuitarSegundos(DateTime fecha) =>
        new(fecha.Ticks - fecha.Ticks % TimeSpan.TicksPerMinute, fecha.Kind);
}
