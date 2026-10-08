using FleetManager.Models;
using FleetManager.Idiomas;

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
            errores.Add(Textos.T("Validacion.FinAntesInicio"));
        }

        if (trayecto.Kilometros < 0)
        {
            errores.Add(Textos.T("Validacion.KmNegativos"));
        }

        if (trayecto.VelocidadMedia < 0 || trayecto.VelocidadMaxima < 0)
        {
            errores.Add(Textos.T("Validacion.VelocidadesNegativas"));
        }
        else if (trayecto.VelocidadMedia > trayecto.VelocidadMaxima)
        {
            errores.Add(Textos.T("Validacion.MediaMayorMaxima"));
        }

        return errores;
    }

    /// <returns>Lista de problemas; vacía si todo está bien.</returns>
    public static IReadOnlyList<string> ValidarJornada(Jornada jornada)
    {
        var errores = new List<string>();

        if (jornada.Fin is { } fin && fin < jornada.Inicio)
        {
            errores.Add(Textos.T("Validacion.FinAntesInicio"));
        }

        if (jornada.Trayectos.Any(t => t.Inicio < jornada.Inicio || (jornada.Fin is { } f && t.Fin > f)))
        {
            errores.Add(Textos.T("Validacion.TrayectosFuera"));
        }

        return errores;
    }
}
