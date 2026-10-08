using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using FleetManager.Core;
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

        // Los números que formatea WPF (por ejemplo, en la tabla del historial) en español: "12,5".
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(System.Windows.Markup.XmlLanguage.GetLanguage("es-ES")));

        // Arrancado con permiso de administrador solo para copiar el plugin al juego: copia y sale.
        if (e.Args.Length == 2 && e.Args[0] == InstaladorPlugin.ArgumentoInstalar)
        {
            var rutasPlugin = RutasDatos.PorDefecto();
            Shutdown(InstaladorPlugin.CopiarComoProcesoAparte(e.Args[1], new Registro(rutasPlugin.Registro)));
            return;
        }

        // Dos copias de FleetManager abiertas a la vez podrían pisarse los datos al guardar.
        instanciaUnica = new Mutex(initiallyOwned: true, @"Local\FleetManager.InstanciaUnica", out bool esLaPrimera);

        if (!esLaPrimera)
        {
            instanciaUnica.Dispose();
            instanciaUnica = null;
            MessageBox.Show("FleetManager ya está abierto.", Titulo, MessageBoxButton.OK, MessageBoxImage.Information);
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

        if (almacen.AvisosCarga.Count > 0)
        {
            MessageBox.Show(
                "Ha habido problemas al cargar los datos:\n\n" + string.Join("\n\n", almacen.AvisosCarga),
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
                registro));

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
                $"No se han podido guardar los últimos cambios:\n\n{almacen.UltimoError}",
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
            "Se han encontrado datos de la versión anterior de FleetManager:\n\n" +
            $"{rutas.SesionesAntiguas}\n\n" +
            "¿Quieres importarlos? El archivo antiguo no se modificará.",
            "Importar datos antiguos",
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
                    avisos.Add($"El perfil del conductor no se pudo importar: {ex.Message}");
                }
            }

            almacen.ReemplazarHistorial(resultado.Historial);
            almacen.GuardarCambiosPendientes();

            registro.Info(
                $"Importación correcta (formato {resultado.Formato}): {resultado.Historial.Jornadas.Count} jornadas, " +
                $"{resultado.Historial.NumeroTrayectos} trayectos, {avisos.Count} avisos.");

            var mensaje = new StringBuilder()
                .AppendLine($"Importación terminada (formato {resultado.Formato}).")
                .AppendLine()
                .AppendLine($"Jornadas: {resultado.Historial.Jornadas.Count}")
                .AppendLine($"Trayectos: {resultado.Historial.NumeroTrayectos}");

            if (avisos.Count > 0)
            {
                mensaje.AppendLine().AppendLine("Avisos:");

                foreach (string aviso in avisos)
                {
                    mensaje.AppendLine("• " + aviso);
                }
            }

            MessageBox.Show(mensaje.ToString(), "Importar datos antiguos", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) when (ex is FormatoAntiguoException or IOException or UnauthorizedAccessException)
        {
            registro.Error("No se pudieron importar los datos antiguos", ex);

            MessageBox.Show(
                "No se han podido importar los datos antiguos. No se ha importado nada y el archivo " +
                $"antiguo sigue intacto.\n\n{ex.Message}",
                "Importar datos antiguos",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void ErrorInesperado(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        registro?.Error("Error inesperado", e.Exception);
        almacen?.GuardarCambiosPendientes();

        MessageBox.Show(
            "Ha ocurrido un error inesperado. Se ha anotado en registro.log y los datos se han guardado.\n\n" +
            e.Exception.Message,
            Titulo,
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        // La aplicación sigue abierta: el error ya está anotado y los datos guardados.
        e.Handled = true;
    }
}
