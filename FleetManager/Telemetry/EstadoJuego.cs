using FleetManager.Models;

namespace FleetManager.Telemetry;

/// <summary>
/// Decide el estado del juego a partir de los datos leídos del plugin.
/// No usa el SDK directamente, así que se puede probar sin el juego.
/// </summary>
public static class InterpreteEstadoJuego
{
    /// <param name="memoriaAbierta">Se pudo abrir la memoria compartida del plugin.</param>
    /// <param name="sdkActivo">El plugin indica que el juego está en marcha.</param>
    /// <param name="esEts2">El juego que escribe los datos es ETS2.</param>
    public static EstadoJuego Interpretar(bool memoriaAbierta, bool sdkActivo, bool esEts2)
    {
        if (!memoriaAbierta)
        {
            return EstadoJuego.Error;
        }

        if (!sdkActivo)
        {
            return EstadoJuego.NoDetectado;
        }

        return esEts2 ? EstadoJuego.Detectado : EstadoJuego.JuegoNoCompatible;
    }
}
