namespace FleetManager.Storage;

/// <summary>
/// Decide cuándo toca guardar: cuando hay cambios pendientes desde hace al
/// menos <see cref="Espera"/>. Así no se escribe en disco con cada pequeño
/// cambio, pero nunca se pierden más de unos segundos de datos.
///
/// Alguien debe llamar a <see cref="Comprobar"/> con regularidad (la aplicación
/// lo hace cada pocos segundos). Los momentos importantes, como cerrar una
/// jornada o salir de la aplicación, guardan al instante sin esperar.
/// </summary>
public sealed class GuardadoAutomatico
{
    public static readonly TimeSpan EsperaPorDefecto = TimeSpan.FromSeconds(30);

    private readonly AlmacenDatos almacen;
    private readonly TimeProvider reloj;

    public GuardadoAutomatico(AlmacenDatos almacen, TimeProvider reloj, TimeSpan espera)
    {
        this.almacen = almacen;
        this.reloj = reloj;
        Espera = espera;
    }

    public TimeSpan Espera { get; }

    /// <returns>Verdadero si se ha intentado guardar.</returns>
    public bool Comprobar()
    {
        if (almacen.PrimerCambioPendiente is not { } primerCambio ||
            reloj.GetUtcNow() - primerCambio < Espera)
        {
            return false;
        }

        almacen.GuardarCambiosPendientes();
        return true;
    }
}
