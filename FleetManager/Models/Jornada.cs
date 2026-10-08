using System.Text.Json.Serialization;

namespace FleetManager.Models;

/// <summary>
/// Una jornada de trabajo: se abre y se cierra a mano y contiene sus trayectos.
/// Los días del historial no se guardan aparte: se obtienen agrupando los
/// trayectos por fecha, así nunca pueden quedar descuadrados.
/// </summary>
public sealed class Jornada
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Número correlativo que ve el usuario (1, 2, 3...).</summary>
    public int Numero { get; set; }

    /// <summary>Hora del juego al abrir la jornada.</summary>
    public DateTime Inicio { get; set; }

    /// <summary>Hora del juego al cerrar la jornada; vacío mientras sigue abierta.</summary>
    public DateTime? Fin { get; set; }

    public List<Trayecto> Trayectos { get; set; } = [];

    /// <summary>
    /// Trayecto que se está haciendo ahora. Se guarda también, para que no se pierda si
    /// la aplicación se cierra de golpe; al terminar pasa a <see cref="Trayectos"/>.
    /// </summary>
    public Trayecto? TrayectoEnCurso { get; set; }

    [JsonIgnore]
    public bool Abierta => Fin is null;

    [JsonIgnore]
    public double Kilometros => Trayectos.Sum(t => t.Kilometros);
}
