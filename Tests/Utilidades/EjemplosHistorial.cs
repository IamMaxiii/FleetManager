using FleetManager.Core;
using FleetManager.Models;

namespace FleetManager.Tests.Utilidades;

/// <summary>
/// Jornadas y trayectos de ejemplo para las pruebas del historial.
/// </summary>
public static class EjemplosHistorial
{
    public static Trayecto Trayecto(int dia, double horaInicio, double horas, string origen, string destino, double km, bool faltaVelocidad = false)
    {
        DateTime inicio = FechaJuego.Componer(dia, TimeSpan.FromHours(horaInicio));

        return new Trayecto
        {
            Origen = origen,
            Destino = destino,
            Carga = "Maquinaria",
            Inicio = inicio,
            Fin = inicio.AddHours(horas),
            Kilometros = km,
            VelocidadMedia = km / horas,
            VelocidadMaxima = Math.Ceiling(km / horas) + 10,
            TiempoConduccion = TimeSpan.FromHours(horas),
            FaltaVelocidad = faltaVelocidad
        };
    }

    /// <summary>
    /// Jornada 1 (cerrada, días 176 y 177) con 3 trayectos y jornada 2 (abierta, día 178) con 1.
    /// </summary>
    public static Historial DosJornadas()
    {
        var jornada1 = new Jornada
        {
            Numero = 1,
            Inicio = FechaJuego.Componer(176, TimeSpan.FromHours(6)),
            Fin = FechaJuego.Componer(177, TimeSpan.FromHours(2)),
            Trayectos =
            {
                Trayecto(176, 7, 3, "Madrid", "Zaragoza", 312.4),
                Trayecto(176, 11, 3, "Zaragoza", "Barcelona", 296.0, faltaVelocidad: true),
                Trayecto(177, 0.5, 1, "Barcelona", "Girona", 99.5)
            }
        };

        var jornada2 = new Jornada
        {
            Numero = 2,
            Inicio = FechaJuego.Componer(178, TimeSpan.FromHours(6)),
            Trayectos = { Trayecto(178, 7, 2, "Girona", "Perpiñán", 95.0) }
        };

        return new Historial { Jornadas = { jornada1, jornada2 } };
    }
}
