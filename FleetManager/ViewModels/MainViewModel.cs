using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using FleetManager.Core;
using FleetManager.MapaJuego;
using FleetManager.Models;
using FleetManager.Storage;
using FleetManager.Telemetry;

namespace FleetManager.ViewModels;

/// <summary>
/// Secciones del menú lateral.
/// </summary>
public enum Seccion
{
    Inicio,
    Tacografo,
    Camion,
    Conductor,
    Historial,
    Ayuda
}

/// <summary>
/// ViewModel de la aplicación: menú lateral, secciones, mini tacógrafo y estado
/// del guardado. El <see cref="Tacografo"/> es el mismo objeto para la ventana
/// principal y para el mini tacógrafo.
/// </summary>
public sealed class MainViewModel : ObjetoObservable, IDisposable
{
    private readonly AlmacenDatos almacen;
    private readonly RutasDatos rutas;

    private Seccion seccion = Seccion.Inicio;
    private bool miniVisible;
    private string textoGuardado = "";
    private bool errorGuardado;

    public MainViewModel(
        ServicioTacografo servicio,
        AlmacenDatos almacen,
        RutasDatos rutas,
        IDialogos dialogos,
        IControlJuego controlJuego,
        ServicioMapaJuego servicioMapaJuego,
        InstaladorPlugin instaladorPlugin)
    {
        this.almacen = almacen;
        this.rutas = rutas;

        Plugin = new PluginViewModel(instaladorPlugin, dialogos);
        Plugin.Comprobar();

        MapaJuego = new MapaJuegoViewModel(servicioMapaJuego, dialogos);
        MapaJuego.Cargar();

        Inicio = new InicioViewModel(servicio, MapaJuego);
        SaltoTiempo = new SaltoTiempoViewModel(servicio, controlJuego, dialogos, SaltoTiempoViewModel.EsperaPorDefecto);
        Tacografo = new TacografoViewModel(servicio, dialogos);
        Camion = new CamionViewModel(servicio);
        Conductor = new ConductorViewModel(almacen, servicio, dialogos, Random.Shared);
        Historial = new HistorialViewModel(almacen, dialogos, MapaJuego);

        miniVisible = almacen.Ajustes.MiniTacografoVisible;
        AvisoDatos = string.Join(Environment.NewLine + Environment.NewLine, almacen.AvisosCarga);

        ComandoAbrirCarpetaDatos = new Comando(AbrirCarpetaDatos);

        almacen.EstadoCambiado += AlmacenEstadoCambiado;
        ActualizarGuardado();
    }

    /// <summary>Mapa del juego, compartido por Inicio e Historial.</summary>
    public MapaJuegoViewModel MapaJuego { get; }

    /// <summary>Aviso y botón si falta el plugin de telemetría en el juego.</summary>
    public PluginViewModel Plugin { get; }

    public InicioViewModel Inicio { get; }

    public SaltoTiempoViewModel SaltoTiempo { get; }

    public TacografoViewModel Tacografo { get; }

    public CamionViewModel Camion { get; }

    public ConductorViewModel Conductor { get; }

    public HistorialViewModel Historial { get; }

    public Seccion Seccion { get => seccion; set => Asignar(ref seccion, value); }

    /// <summary>Mostrar u ocultar el mini tacógrafo. Se recuerda para la próxima vez.</summary>
    public bool MiniVisible
    {
        get => miniVisible;
        set
        {
            if (Asignar(ref miniVisible, value))
            {
                almacen.Ajustes.MiniTacografoVisible = value;
                almacen.MarcarCambios(ArchivosDatos.Ajustes);
            }
        }
    }

    public string TextoGuardado { get => textoGuardado; private set => Asignar(ref textoGuardado, value); }

    public bool ErrorGuardado { get => errorGuardado; private set => Asignar(ref errorGuardado, value); }

    /// <summary>Problemas encontrados al cargar los datos (archivos dañados, recuperaciones...).</summary>
    public string AvisoDatos { get; }

    public bool HayAvisoDatos => AvisoDatos.Length > 0;

    public ICommand ComandoAbrirCarpetaDatos { get; }

    public AjustesApp Ajustes => almacen.Ajustes;

    /// <summary>La ventana del mini tacógrafo llama aquí al terminar de arrastrarla.</summary>
    public void GuardarPosicionMini(double izquierda, double arriba)
    {
        if (Ajustes.MiniTacografoIzquierda == izquierda && Ajustes.MiniTacografoArriba == arriba)
        {
            return;
        }

        Ajustes.MiniTacografoIzquierda = izquierda;
        Ajustes.MiniTacografoArriba = arriba;
        almacen.MarcarCambios(ArchivosDatos.Ajustes);
    }

    public void Dispose()
    {
        almacen.EstadoCambiado -= AlmacenEstadoCambiado;
        Inicio.Dispose();
        SaltoTiempo.Dispose();
        Tacografo.Dispose();
        Camion.Dispose();
        Conductor.Dispose();
        Historial.Dispose();
    }

    private void AlmacenEstadoCambiado(object? sender, EventArgs e) => ActualizarGuardado();

    private void ActualizarGuardado()
    {
        ErrorGuardado = almacen.UltimoError is not null;
        TextoGuardado = almacen.UltimoError is { } error
            ? $"Error al guardar: {error}"
            : almacen.UltimoGuardado is { } momento
                ? $"Guardado a las {momento.ToLocalTime():HH:mm}"
                : "Datos al día";
    }

    private void AbrirCarpetaDatos()
    {
        Directory.CreateDirectory(rutas.Carpeta);
        Process.Start(new ProcessStartInfo { FileName = rutas.Carpeta, UseShellExecute = true });
    }
}
