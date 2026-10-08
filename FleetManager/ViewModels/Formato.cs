using System.Globalization;
using FleetManager.Models;

namespace FleetManager.ViewModels;

/// <summary>
/// Cómo se escriben en pantalla horas, duraciones, distancias y actividades.
/// </summary>
public static class Formato
{
    private static readonly CultureInfo Espanol = CultureInfo.GetCultureInfo("es-ES");

    private static readonly string[] Dias = ["lun", "mar", "mié", "jue", "vie", "sáb", "dom"];

    /// <summary>Duración como horas:minutos ("05:30", "56:00"). Nunca negativa.</summary>
    public static string Duracion(TimeSpan duracion)
    {
        if (duracion < TimeSpan.Zero)
        {
            duracion = TimeSpan.Zero;
        }

        return $"{(int)duracion.TotalHours:00}:{duracion.Minutes:00}";
    }

    /// <summary>Hora del juego con el día de la semana ("mié 14:32").</summary>
    public static string HoraJuego(DateTime hora) =>
        $"{Dias[((int)hora.DayOfWeek + 6) % 7]} {hora.ToString("HH:mm", Espanol)}";

    public static string Kilometros(double kilometros) => kilometros.ToString("N1", Espanol) + " km";

    public static string Velocidad(double kmh) => kmh.ToString("0", Espanol) + " km/h";

    /// <summary>Número entero con separador de miles ("37.597").</summary>
    public static string Numero(double valor) => valor.ToString("N0", Espanol);

    /// <summary>Fracción de 0 a 1 como porcentaje ("40 %").</summary>
    public static string Porcentaje(double fraccion) => fraccion.ToString("0 %", Espanol);

    /// <summary>Peso en kg como toneladas ("18,5 t").</summary>
    public static string Toneladas(double kilos) => (kilos / 1000).ToString("0.0", Espanol) + " t";

    public static string Actividad(Actividad actividad) => actividad switch
    {
        Models.Actividad.Conduccion => "Conducción",
        Models.Actividad.OtrosTrabajos => "Otros trabajos",
        Models.Actividad.Disponibilidad => "Disponibilidad",
        _ => "Descanso"
    };
}
