using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using FleetManager.Core;
using FleetManager.Idiomas;
using FleetManager.Storage;
using FleetManager.Storage.Importacion;
using FleetManager.Telemetry;
using FleetManager.ViewModels;
using FleetManager.Views;

namespace FleetManager;

/// <summary>
/// Punto de arranque de la aplicación: carga los datos, ofrece importar los
/// antiguos, pone en marcha el guardado automático y la lectura del juego, y
/// abre la ventana.
/// </summary>
public partial class App : Application
{
    private const string Titulo = "FleetManager";
    private const string NombreInstanciaUnica = @"Local\FleetManager.InstanciaUnica";

    /// <summary>Argumento con el que FleetManager se abre a sí mismo al reiniciarse.</summary>
    private const string ArgumentoReinicio = "--reinicio";

    private static readonly TimeSpan EsperaReinicio = TimeSpan.FromSeconds(15);

    // Cada cuánto se comprueba si toca hacer el guardado automático.
    private static readonly TimeSpan IntervaloComprobacionGuardado = TimeSpan.FromSeconds(5);

    // Ritmo fijo de lectura del juego: 4 veces por segundo.
    private static readonly TimeSpan IntervaloLectura = TimeSpan.FromMilliseconds(250);

    private Mutex? instanciaUnica;
    private RutasDatos? rutas;
    private Registro? registro;
    private AlmacenDatos? almacen;
    private DispatcherTimer? temporizadorGuardado;
    private DispatcherTimer? temporizadorLectura;
    private LectorJuego? lector;
    private MainViewModel? mainViewModel;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Arrancado con permiso de administrador solo para copiar el plugin al juego: copia y sale.
        if (e.Args.Length == 2 && e.Args[0] == InstaladorPlugin.ArgumentoInstalar)
        {
            var rutasPlugin = RutasDatos.PorDefecto();
            Shutdown(InstaladorPlugin.CopiarComoProcesoAparte(e.Args[1], new Registro(rutasPlugin.Registro)));
            return;
        }

        // Hasta leer los ajustes, el idioma de Windows.
        Textos.Usar(Textos.ElegirSegunWindows(CultureInfo.CurrentUICulture));

        // Dos copias de FleetManager abiertas a la vez podrían pisarse los datos al guardar.
        // Al reiniciar (cambio de idioma) se espera a que la copia anterior termine de cerrarse.
        instanciaUnica = new Mutex(initiallyOwned: false, NombreInstanciaUnica);
        bool esLaPrimera;

        try
        {
            esLaPrimera = instanciaUnica.WaitOne(e.Args.Contains(ArgumentoReinicio) ? EsperaReinicio : TimeSpan.Zero);
        }
        catch (AbandonedMutexException)
        {
            esLaPrimera = true; // la copia anterior se cerró sin soltarlo
        }

        if (!esLaPrimera)
        {
            instanciaUnica.Dispose();
            instanciaUnica = null;
            MessageBox.Show(Textos.T("App.YaAbierto"), Titulo, MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        rutas = RutasDatos.PorDefecto();
        registro = new Registro(rutas.Registro);
        registro.Info("Inicio de FleetManager.");

        DispatcherUnhandledException += ErrorInesperado;
        SessionEnding += (_, _) => almacen?.GuardarCambiosPendientes();

        almacen = new AlmacenDatos(rutas, registro, TimeProvider.System);
        almacen.Cargar();
        UsarIdioma(almacen.Ajustes.Idioma);

        if (almacen.AvisosCarga.Count > 0)
        {
            MessageBox.Show(
                Textos.T("App.ProblemasCarga") + "\n\n" + string.Join("\n\n", almacen.AvisosCarga),
                Titulo,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        OfrecerImportacion(rutas, registro, almacen);

        var guardadoAutomatico = new GuardadoAutomatico(almacen, TimeProvider.System, GuardadoAutomatico.EsperaPorDefecto);
        temporizadorGuardado = new DispatcherTimer { Interval = IntervaloComprobacionGuardado };
        temporizadorGuardado.Tick += (_, _) => guardadoAutomatico.Comprobar();
        temporizadorGuardado.Start();

        // Una sola pieza central, compartida por todas las ventanas.
        var servicio = new ServicioTacografo(almacen, TimeProvider.System);
        lector = new LectorJuego(TimeProvider.System);
        temporizadorLectura = new DispatcherTimer { Interval = IntervaloLectura };
        temporizadorLectura.Tick += (_, _) => servicio.ProcesarLectura(lector.Leer());

        mainViewModel = new MainViewModel(
            servicio,
            almacen,
            rutas,
            new DialogosWpf(),
            new ControlJuego(registro),
            new FleetManager.MapaJuego.ServicioMapaJuego(rutas.CarpetaMapaJuego, registro),
            new InstaladorPlugin(
                InstaladorPlugin.PluginJuntoALaApp,
                FleetManager.MapaJuego.BuscadorJuego.Buscar,
                InstaladorPlugin.CopiarComoAdministrador,
                registro),
            Reiniciar);

        MainWindow = new MainWindow(mainViewModel);
        MainWindow.Show();

        // Mini tacógrafo: sin ventana "dueña", para que siga visible al minimizar la principal.
        // Se cierra junto con la principal porque la aplicación termina al cerrarla.
        var mini = new MiniTacografoWindow(mainViewModel);
        mainViewModel.PropertyChanged += (_, cambio) =>
        {
            if (cambio.PropertyName == nameof(MainViewModel.MiniVisible))
            {
                MostrarMini(mini, mainViewModel.MiniVisible);
            }
        };
        MostrarMini(mini, mainViewModel.MiniVisible);

        servicio.ProcesarLectura(lector.Leer());
        temporizadorLectura.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        temporizadorLectura?.Stop();
        temporizadorGuardado?.Stop();

        if (almacen is not null && !almacen.GuardarCambiosPendientes())
        {
            MessageBox.Show(
                Textos.T("App.NoGuardado", almacen.UltimoError),
                Titulo,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        mainViewModel?.Dispose();
        lector?.Dispose();
        registro?.Info("Cierre de FleetManager.");

        if (instanciaUnica is not null)
        {
            instanciaUnica.ReleaseMutex();
            instanciaUnica.Dispose();
        }

        base.OnExit(e);
    }

    /// <summary>
    /// Idioma de los textos y de los números (el elegido o, si no hay, el de Windows).
    /// Solo se puede fijar una vez por ejecución: cambiarlo reinicia la aplicación.
    /// </summary>
    private static void UsarIdioma(string? codigo)
    {
        Textos.Usar(string.IsNullOrEmpty(codigo) ? Textos.ElegirSegunWindows(CultureInfo.CurrentUICulture) : codigo);
        CultureInfo.CurrentCulture = Textos.Cultura;
        CultureInfo.CurrentUICulture = Textos.Cultura;
        CultureInfo.DefaultThreadCurrentCulture = Textos.Cultura;
        CultureInfo.DefaultThreadCurrentUICulture = Textos.Cultura;

        // Los números que formatea WPF (por ejemplo, en la tabla del historial) en ese idioma.
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(System.Windows.Markup.XmlLanguage.GetLanguage(Textos.Cultura.IetfLanguageTag)));
    }

    /// <summary>Abre otra vez FleetManager (que espera a que esta copia se cierre) y cierra esta.</summary>
    private void Reiniciar()
    {
        Process.Start(new ProcessStartInfo(Environment.ProcessPath!, ArgumentoReinicio) { UseShellExecute = false });
        Shutdown();
    }

    private static void MostrarMini(MiniTacografoWindow mini, bool visible)
    {
        if (visible)
        {
            mini.Show();
            mini.Colocar();
        }
        else
        {
            mini.Hide();
        }
    }

    /// <summary>
    /// Si es la primera vez que se usa esta versión y existen datos de la versión
    /// antigua en la misma carpeta, pregunta si se quieren importar.
    /// </summary>
    private static void OfrecerImportacion(RutasDatos rutas, Registro registro, AlmacenDatos almacen)
    {
        if (!almacen.HistorialEsNuevo || !File.Exists(rutas.SesionesAntiguas))
        {
            return;
        }

        var respuesta = MessageBox.Show(
            Textos.T("Importar.Pregunta", rutas.SesionesAntiguas),
            Textos.T("Importar.Titulo"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (respuesta != MessageBoxResult.Yes)
        {
            // Se guarda el historial vacío para no volver a preguntar en cada arranque.
            registro.Info("El usuario ha rechazado importar los datos antiguos.");
            almacen.MarcarCambios(ArchivosDatos.Historial);
            almacen.GuardarCambiosPendientes();
            return;
        }

        try
        {
            ResultadoImportacion resultado = ImportadorDatosAntiguos.ImportarSesiones(rutas.SesionesAntiguas);
            var avisos = new List<string>(resultado.Avisos);

            if (almacen.PerfilEsNuevo && File.Exists(rutas.PerfilAntiguo))
            {
                try
                {
                    almacen.ReemplazarPerfil(ImportadorDatosAntiguos.ImportarPerfil(rutas.PerfilAntiguo));
                }
                catch (Exception ex) when (ex is FormatoAntiguoException or IOException or UnauthorizedAccessException)
                {
                    registro.Error("No se pudo importar el perfil antiguo", ex);
                    avisos.Add(Textos.T("Importar.PerfilNo", ex.Message));
                }
            }

            almacen.ReemplazarHistorial(resultado.Historial);
            almacen.GuardarCambiosPendientes();

            registro.Info(
                $"Importación correcta (formato {resultado.Formato}): {resultado.Historial.Jornadas.Count} jornadas, " +
                $"{resultado.Historial.NumeroTrayectos} trayectos, {avisos.Count} avisos.");

            var mensaje = new StringBuilder()
                .AppendLine(Textos.T("Importar.Terminada", resultado.Formato))
                .AppendLine()
                .AppendLine(Textos.T("Importar.Jornadas", resultado.Historial.Jornadas.Count))
                .AppendLine(Textos.T("Importar.Trayectos", resultado.Historial.NumeroTrayectos));

            if (avisos.Count > 0)
            {
                mensaje.AppendLine().AppendLine(Textos.T("Importar.Avisos"));

                foreach (string aviso in avisos)
                {
                    mensaje.AppendLine("• " + aviso);
                }
            }

            MessageBox.Show(mensaje.ToString(), Textos.T("Importar.Titulo"), MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) when (ex is FormatoAntiguoException or IOException or UnauthorizedAccessException)
        {
            registro.Error("No se pudieron importar los datos antiguos", ex);

            MessageBox.Show(
                Textos.T("Importar.Error", ex.Message),
                Textos.T("Importar.Titulo"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void ErrorInesperado(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        registro?.Error("Error inesperado", e.Exception);
        almacen?.GuardarCambiosPendientes();

        MessageBox.Show(
            Textos.T("App.ErrorInesperado", e.Exception.Message),
            Titulo,
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        // La aplicación sigue abierta: el error ya está anotado y los datos guardados.
        e.Handled = true;
    }
}
