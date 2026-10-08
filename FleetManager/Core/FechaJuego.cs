namespace FleetManager.Core;

/// <summary>
/// Fechas del juego. El SDK cuenta la hora del juego desde el lunes 1/1/0001, así
/// que el año no significa nada: en el historial se usa el número de día del juego
/// ("Día 176 (jue)"), como eligió el usuario.
/// </summary>
public static class FechaJuego
{
    private static readonly string[] Dias = ["lun", "mar", "mié", "jue", "vie", "sáb", "dom"];

    /// <summary>Número de día del juego, empezando en 1.</summary>
    public static int NumeroDia(DateTime fecha) => (int)(fecha.Date - DateTime.MinValue).TotalDays + 1;

    /// <summary>Abreviatura del día de la semana ("jue").</summary>
    public static string NombreDia(DateTime fecha) => Dias[((int)fecha.DayOfWeek + 6) % 7];

    /// <summary>"Día 176 (jue)".</summary>
    public static string TextoDia(DateTime fecha) => $"Día {NumeroDia(fecha)} ({NombreDia(fecha)})";

    /// <summary>"14:32".</summary>
    public static string TextoHora(DateTime fecha) => fecha.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>"Día 176 (jue) 14:32".</summary>
    public static string TextoDiaYHora(DateTime fecha) => $"{TextoDia(fecha)} {TextoHora(fecha)}";

    /// <summary>La hora del juego de un número de día y una hora del día.</summary>
    public static DateTime Componer(int dia, TimeSpan hora) => DateTime.MinValue.AddDays(dia - 1) + hora;
}
