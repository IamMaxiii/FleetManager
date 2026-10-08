using FleetManager.Core;
using FleetManager.Models;

namespace FleetManager.ViewModels;

/// <summary>
/// Página de inicio: posición y rumbo del camión y recorrido de la jornada (la abierta,
/// o la última) para el mapa. El resto de la página (actividad, trayecto, encargo) usa
/// los ViewModels del tacógrafo y del camión.
/// </summary>
public sealed class InicioViewModel : ObjetoObservable, IDisposable
{
    private readonly ServicioTacografo servicio;

    private bool hayPosicion;
    private double posicionX;
    private double posicionZ;
    private double rumbo;
    private MapaRecorrido recorrido;
    private int versionRecorrido;
    private bool seguirCamion = true;
    private string coordenadas = "";

    public InicioViewModel(ServicioTacografo servicio, MapaJuegoViewModel mapaJuego)
    {
        this.servicio = servicio;
        MapaJuego = mapaJuego;
        recorrido = servicio.Recorrido;
        servicio.Actualizado += ServicioActualizado;
        Actualizar();
    }

    /// <summary>Mapa del juego (teselas y ciudades), compartido con el historial.</summary>
    public MapaJuegoViewModel MapaJuego { get; }

    /// <summary>Recorrido de la jornada abierta (o de la última): se vacía al abrir una jornada nueva.</summary>
    public MapaRecorrido Recorrido { get => recorrido; private set => Asignar(ref recorrido, value); }

    /// <summary>Cambia cuando se añade algo al recorrido: el mapa se redibuja entonces.</summary>
    public int VersionRecorrido { get => versionRecorrido; private set => Asignar(ref versionRecorrido, value); }

    /// <summary>Hay conexión con el juego y una posición válida del camión.</summary>
    public bool HayPosicion { get => hayPosicion; private set => Asignar(ref hayPosicion, value); }

    public double PosicionX { get => posicionX; private set => Asignar(ref posicionX, value); }

    public double PosicionZ { get => posicionZ; private set => Asignar(ref posicionZ, value); }

    /// <summary>Fracción de vuelta: 0 = norte, 0,25 = oeste, 0,5 = sur, 0,75 = este.</summary>
    public double Rumbo { get => rumbo; private set => Asignar(ref rumbo, value); }

    /// <summary>El mapa se mueve solo para seguir al camión. Arrastrar el mapa lo desactiva.</summary>
    public bool SeguirCamion { get => seguirCamion; set => Asignar(ref seguirCamion, value); }

    public string Coordenadas { get => coordenadas; private set => Asignar(ref coordenadas, value); }

    public void Dispose() => servicio.Actualizado -= ServicioActualizado;

    private void ServicioActualizado(object? sender, EventArgs e) => Actualizar();

    private void Actualizar()
    {
        DatosJuego datos = servicio.Datos;

        Recorrido = servicio.Recorrido;
        VersionRecorrido = servicio.VersionRecorrido;

        if (!datos.Conectado || (datos.PosicionX == 0 && datos.PosicionZ == 0))
        {
            HayPosicion = false;
            return; // se mantiene la última posición conocida
        }

        HayPosicion = true;
        PosicionX = datos.PosicionX;
        PosicionZ = datos.PosicionZ;
        Rumbo = datos.Rumbo;
        Coordenadas = $"X {datos.PosicionX:0} · Z {datos.PosicionZ:0}";
    }
}
