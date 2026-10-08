using System.Globalization;
using FleetManager.Core;
using FleetManager.Idiomas;
using FleetManager.Models;

namespace FleetManager.ViewModels;

/// <summary>
/// Cómo se escriben en pantalla horas, duraciones, distancias y actividades.
/// </summary>
public static class Formato
{
    // Números con la coma o el punto decimal del idioma de la aplicación.
    private static CultureInfo Cultura => Textos.Cultura;

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
        $"{FechaJuego.NombreDia(hora)} {hora.ToString("HH:mm", CultureInfo.InvariantCulture)}";

    public static string Kilometros(double kilometros) => kilometros.ToString("N1", Cultura) + " km";

    public static string Velocidad(double kmh) => kmh.ToString("0", Cultura) + " km/h";

    /// <summary>Número entero con separador de miles ("37.597").</summary>
    public static string Numero(double valor) => valor.ToString("N0", Cultura);

    /// <summary>Fracción de 0 a 1 como porcentaje ("40 %").</summary>
    public static string Porcentaje(double fraccion) => fraccion.ToString("0 %", Cultura);

    /// <summary>Peso en kg como toneladas ("18,5 t").</summary>
    public static string Toneladas(double kilos) => (kilos / 1000).ToString("0.0", Cultura) + " t";

    public static string Actividad(Actividad actividad) => actividad switch
    {
        Models.Actividad.Conduccion => Textos.T("Actividad.Conduccion"),
        Models.Actividad.OtrosTrabajos => Textos.T("Actividad.OtrosTrabajos"),
        Models.Actividad.Disponibilidad => Textos.T("Actividad.Disponibilidad"),
        _ => Textos.T("Actividad.Descanso")
    };
}
