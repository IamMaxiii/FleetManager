using FleetManager.Models;

namespace FleetManager.Core;

/// <summary>
/// Comprueba que una jornada o un trayecto editados a mano tienen sentido antes de guardarlos.
/// </summary>
public static class ValidadorHistorial
{
    /// <returns>Lista de problemas; vacía si todo está bien.</returns>
    public static IReadOnlyList<string> ValidarTrayecto(Trayecto trayecto)
    {
        var errores = new List<string>();

        if (trayecto.Fin < trayecto.Inicio)
        {
            errores.Add("La hora de fin no puede ser anterior a la de inicio.");
        }

        if (trayecto.Kilometros < 0)
        {
            errores.Add("Los kilómetros no pueden ser negativos.");
        }

        if (trayecto.VelocidadMedia < 0 || trayecto.VelocidadMaxima < 0)
        {
            errores.Add("Las velocidades no pueden ser negativas.");
        }
        else if (trayecto.VelocidadMedia > trayecto.VelocidadMaxima)
        {
            errores.Add("La velocidad media no puede ser mayor que la máxima.");
        }

        return errores;
    }

    /// <returns>Lista de problemas; vacía si todo está bien.</returns>
    public static IReadOnlyList<string> ValidarJornada(Jornada jornada)
    {
        var errores = new List<string>();

        if (jornada.Fin is { } fin && fin < jornada.Inicio)
        {
            errores.Add("La hora de fin no puede ser anterior a la de inicio.");
        }

        if (jornada.Trayectos.Any(t => t.Inicio < jornada.Inicio || (jornada.Fin is { } f && t.Fin > f)))
        {
            errores.Add("Hay trayectos fuera del horario de la jornada.");
        }

        return errores;
    }
}
