using FleetManager.Models;
using FleetManager.Telemetry;

namespace FleetManager.Tests.Telemetry;

/// <summary>
/// Pruebas de la decisión "¿está el juego en marcha?" sin necesidad de abrir ETS2.
/// </summary>
public class InterpreteEstadoJuegoTests
{
    [Fact]
    public void SinMemoriaCompartida_DevuelveError()
    {
        var estado = InterpreteEstadoJuego.Interpretar(memoriaAbierta: false, sdkActivo: false, esEts2: false);

        Assert.Equal(EstadoJuego.Error, estado);
    }

    [Fact]
    public void PluginInactivo_DevuelveNoDetectado()
    {
        var estado = InterpreteEstadoJuego.Interpretar(memoriaAbierta: true, sdkActivo: false, esEts2: true);

        Assert.Equal(EstadoJuego.NoDetectado, estado);
    }

    [Fact]
    public void PluginActivoConEts2_DevuelveDetectado()
    {
        var estado = InterpreteEstadoJuego.Interpretar(memoriaAbierta: true, sdkActivo: true, esEts2: true);

        Assert.Equal(EstadoJuego.Detectado, estado);
    }

    [Fact]
    public void PluginActivoConOtroJuego_DevuelveNoCompatible()
    {
        var estado = InterpreteEstadoJuego.Interpretar(memoriaAbierta: true, sdkActivo: true, esEts2: false);

        Assert.Equal(EstadoJuego.JuegoNoCompatible, estado);
    }
}
