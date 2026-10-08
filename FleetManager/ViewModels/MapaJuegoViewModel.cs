using System.Windows.Input;
using FleetManager.MapaJuego;

namespace FleetManager.ViewModels;

/// <summary>
/// El mapa del juego para las páginas que lo muestran (Inicio e Historial): si está
/// preparado, sus teselas y ciudades; y el botón para prepararlo (con su progreso).
/// Una sola instancia compartida.
/// </summary>
public sealed class MapaJuegoViewModel : ObjetoObservable
{
    private readonly ServicioMapaJuego servicio;
    private readonly IDialogos dialogos;

    private bool generando;
    private double progreso;
    private string textoProgreso = "";
    private string mensaje = "";
    private int version;
    private CancellationTokenSource? cancelacion;

    public MapaJuegoViewModel(ServicioMapaJuego servicio, IDialogos dialogos)
    {
        this.servicio = servicio;
        this.dialogos = dialogos;

        ComandoPreparar = new Comando(() => _ = Preparar(), () => !Generando);
        ComandoCancelar = new Comando(() => cancelacion?.Cancel(), () => Generando);
    }

    public bool Disponible => servicio.Disponible;

    /// <summary>El juego ha cambiado (actualización o DLC) desde que se preparó el mapa.</summary>
    public bool NecesitaActualizar => servicio.NecesitaActualizar;

    public InfoMapaJuego? Info => servicio.Info;

    public string? CarpetaTeselas => servicio.CarpetaTeselas;

    public IReadOnlyList<CiudadMapa> Ciudades => servicio.Ciudades;

    /// <summary>Cambia cuando se carga un mapa nuevo (para que los lienzos se redibujen).</summary>
    public int Version { get => version; private set => Asignar(ref version, value); }

    public bool Generando { get => generando; private set => Asignar(ref generando, value); }

    public double Progreso { get => progreso; private set => Asignar(ref progreso, value); }

    public string TextoProgreso { get => textoProgreso; private set => Asignar(ref textoProgreso, value); }

    public string Mensaje { get => mensaje; private set => Asignar(ref mensaje, value); }

    public ICommand ComandoPreparar { get; }

    public ICommand ComandoCancelar { get; }

    /// <summary>Busca el juego y carga el mapa ya preparado, si lo hay.</summary>
    public void Cargar()
    {
        servicio.Cargar();
        AvisarCambios();
    }

    public async Task Preparar()
    {
        if (Generando)
        {
            return;
        }

        if (!dialogos.Confirmar(
                "FleetManager va a leer los archivos de tu juego y dibujar el mapa. Tarda unos minutos y " +
                "ocupa unos 150 MB. Puedes seguir usando la aplicación mientras tanto. ¿Empezar?"))
        {
            return;
        }

        Generando = true;
        Progreso = 0;
        Mensaje = "";
        TextoProgreso = "Empezando…";
        cancelacion = new CancellationTokenSource();
        CommandManager.InvalidateRequerySuggested();

        try
        {
            var avance = new Progress<ProgresoMapa>(p =>
            {
                Progreso = p.Fraccion;
                TextoProgreso = p.Texto;
            });

            string? error = await servicio.Generar(avance, cancelacion.Token);
            Mensaje = error is null ? "Mapa del juego preparado." : $"No se ha podido preparar el mapa: {error}";
        }
        finally
        {
            Generando = false;
            cancelacion.Dispose();
            cancelacion = null;
            AvisarCambios();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private void AvisarCambios()
    {
        Avisar(nameof(Disponible));
        Avisar(nameof(NecesitaActualizar));
        Avisar(nameof(Info));
        Avisar(nameof(CarpetaTeselas));
        Avisar(nameof(Ciudades));
        Version++;
    }
}
