namespace FleetManager.Models;

/// <summary>
/// Un trayecto dentro de una jornada: desde que el camión empieza a circular
/// hasta la siguiente pausa o el cierre de la jornada. Las horas son del juego.
/// </summary>
public sealed class Trayecto
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Origen { get; set; } = "";

    public string Destino { get; set; } = "";

    public string Carga { get; set; } = "";

    public DateTime Inicio { get; set; }

    public DateTime Fin { get; set; }

    public double Kilometros { get; set; }

    /// <summary>Velocidad media en km/h.</summary>
    public double VelocidadMedia { get; set; }

    /// <summary>Velocidad máxima en km/h.</summary>
    public double VelocidadMaxima { get; set; }

    /// <summary>Se superó el límite de velocidad durante demasiado tiempo.</summary>
    public bool FaltaVelocidad { get; set; }

    /// <summary>Hubo una infracción de tiempos de conducción (continua, diaria, semanal o bisemanal) durante el trayecto.</summary>
    public bool FaltaConduccion { get; set; }

    /// <summary>Odómetro al empezar, para calcular los kilómetros.</summary>
    public double OdometroInicial { get; set; }

    /// <summary>Tiempo del juego en conducción, para calcular la velocidad media.</summary>
    public TimeSpan TiempoConduccion { get; set; }
}
