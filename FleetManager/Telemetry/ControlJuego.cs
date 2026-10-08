using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using FleetManager.Storage;

namespace FleetManager.Telemetry;

public enum ResultadoActivacion
{
    Hecho,
    YaEstaba,
    JuegoAbierto,
    ConfiguracionNoEncontrada,
    Error
}

/// <summary>
/// Acciones sobre el juego que no puede hacer el SDK (que solo lee): activar la
/// consola en config.cfg y escribir comandos en ella.
/// </summary>
public interface IControlJuego
{
    bool JuegoAbierto();

    bool ConsolaActivada();

    /// <summary>Activa la consola en config.cfg. Solo con el juego cerrado (al cerrarse, el juego reescribe el archivo).</summary>
    ResultadoActivacion ActivarConsola();

    /// <summary>
    /// Pone el juego en primer plano, abre la consola y escribe el comando (la consola se
    /// queda abierta: se cierra con <see cref="CerrarConsola"/> cuando el comando ya ha hecho efecto).
    /// </summary>
    /// <returns>Falso si no se encuentra la ventana del juego.</returns>
    Task<bool> EnviarComando(string comando);

    /// <summary>
    /// Cierra la consola. Hay que llamarlo cuando el juego ya ha procesado el comando: justo
    /// después del salto de hora el juego está ocupado y no recoge la tecla.
    /// </summary>
    Task CerrarConsola();
}

/// <summary>
/// Implementación real: lee y cambia config.cfg en Documentos\Euro Truck Simulator 2, y
/// teclea en el juego con SendInput. Las teclas se envían por su posición física
/// ("scancode"), así que funciona con cualquier distribución de teclado: la consola
/// se abre con la tecla que hay a la izquierda del 1 (º en un teclado español).
/// </summary>
public sealed class ControlJuego : IControlJuego
{
    private const string NombreProceso = "eurotrucks2";

    // Posiciones físicas de las teclas.
    private const ushort TeclaConsola = 0x29;
    private const ushort TeclaIntro = 0x1C;
    private const ushort TeclaMayusculas = 0x2A;

    private const uint EntradaTeclado = 1;
    private const uint PorPosicion = 0x0008;   // KEYEVENTF_SCANCODE
    private const uint Soltar = 0x0002;        // KEYEVENTF_KEYUP
    private const int Restaurar = 9;           // SW_RESTORE

    private static readonly TimeSpan PausaEntreTeclas = TimeSpan.FromMilliseconds(35);

    private readonly Registro registro;

    public ControlJuego(Registro registro)
    {
        this.registro = registro;
    }

    public static string RutaConfiguracion => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "Euro Truck Simulator 2",
        "config.cfg");

    public bool JuegoAbierto() => Process.GetProcessesByName(NombreProceso).Length > 0;

    public bool ConsolaActivada()
    {
        try
        {
            return File.Exists(RutaConfiguracion) && ConfiguracionConsola.EstaActivada(File.ReadAllText(RutaConfiguracion));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            registro.Aviso("No se pudo leer config.cfg del juego", ex);
            return false;
        }
    }

    public ResultadoActivacion ActivarConsola()
    {
        if (JuegoAbierto())
        {
            return ResultadoActivacion.JuegoAbierto;
        }

        if (!File.Exists(RutaConfiguracion))
        {
            return ResultadoActivacion.ConfiguracionNoEncontrada;
        }

        try
        {
            string texto = File.ReadAllText(RutaConfiguracion);

            if (ConfiguracionConsola.EstaActivada(texto))
            {
                return ResultadoActivacion.YaEstaba;
            }

            // Copia del archivo original, por si hubiera que volver atrás (solo la primera vez).
            string copia = RutaConfiguracion + ".antes-de-fleetmanager";

            if (!File.Exists(copia))
            {
                File.Copy(RutaConfiguracion, copia);
            }

            File.WriteAllText(RutaConfiguracion, ConfiguracionConsola.Activar(texto), new UTF8Encoding(false));
            registro.Info($"Consola del juego activada en {RutaConfiguracion} (copia del original: {copia}).");
            return ResultadoActivacion.Hecho;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            registro.Error("No se pudo activar la consola en config.cfg", ex);
            return ResultadoActivacion.Error;
        }
    }

    public async Task<bool> EnviarComando(string comando)
    {
        if (!await PonerJuegoDelante())
        {
            return false;
        }

        await Pulsar(TeclaConsola);
        await Task.Delay(400); // que se abra la consola

        foreach (char letra in comando)
        {
            await Escribir(letra);
        }

        await Task.Delay(100);
        await Pulsar(TeclaIntro);

        registro.Info($"Comando enviado a la consola del juego: {comando}");
        return true;
    }

    public async Task CerrarConsola()
    {
        if (await PonerJuegoDelante())
        {
            await Task.Delay(500); // margen para que el juego termine con el comando
            await Pulsar(TeclaConsola);
            registro.Info("Consola del juego cerrada.");
        }
    }

    /// <summary>Pone el juego en primer plano (restaurándolo si está minimizado); falso si no está abierto.</summary>
    private static async Task<bool> PonerJuegoDelante()
    {
        IntPtr ventana = Process.GetProcessesByName(NombreProceso)
            .Select(p => p.MainWindowHandle)
            .FirstOrDefault(h => h != IntPtr.Zero);

        if (ventana == IntPtr.Zero)
        {
            return false;
        }

        if (IsIconic(ventana))
        {
            ShowWindow(ventana, Restaurar);
        }

        SetForegroundWindow(ventana);
        await Task.Delay(600); // que el juego tome el foco y salga de la pausa
        return true;
    }

    private static async Task Escribir(char letra)
    {
        // Qué tecla (y si hace falta Mayúsculas) produce esta letra con la distribución de teclado actual.
        short codigo = VkKeyScan(letra);

        if (codigo == -1)
        {
            return;
        }

        bool conMayusculas = (codigo & 0x100) != 0;
        ushort posicion = (ushort)MapVirtualKey((uint)(codigo & 0xFF), 0);

        if (conMayusculas)
        {
            Tecla(TeclaMayusculas, soltar: false);
        }

        await Pulsar(posicion);

        if (conMayusculas)
        {
            Tecla(TeclaMayusculas, soltar: true);
        }
    }

    private static async Task Pulsar(ushort posicion)
    {
        Tecla(posicion, soltar: false);
        await Task.Delay(PausaEntreTeclas);
        Tecla(posicion, soltar: true);
        await Task.Delay(PausaEntreTeclas);
    }

    private static void Tecla(ushort posicion, bool soltar)
    {
        var entrada = new Entrada
        {
            Tipo = EntradaTeclado,
            Datos = new DatosEntrada
            {
                Teclado = new EntradaDeTeclado
                {
                    Posicion = posicion,
                    Opciones = PorPosicion | (soltar ? Soltar : 0)
                }
            }
        };

        SendInput(1, [entrada], Marshal.SizeOf<Entrada>());
    }

    // ---------- Funciones de Windows ----------

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint cantidad, Entrada[] entradas, int tamano);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr ventana);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr ventana, int modo);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr ventana);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern short VkKeyScan(char letra);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint codigo, uint tipo);

    [StructLayout(LayoutKind.Sequential)]
    private struct Entrada
    {
        public uint Tipo;
        public DatosEntrada Datos;
    }

    // La unión incluye la entrada de ratón (la más grande) para que el tamaño sea el que espera Windows.
    [StructLayout(LayoutKind.Explicit)]
    private struct DatosEntrada
    {
        [FieldOffset(0)] public EntradaDeRaton Raton;
        [FieldOffset(0)] public EntradaDeTeclado Teclado;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct EntradaDeRaton
    {
        public int X;
        public int Y;
        public uint Datos;
        public uint Opciones;
        public uint Momento;
        public IntPtr Extra;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct EntradaDeTeclado
    {
        public ushort CodigoVirtual;
        public ushort Posicion;
        public uint Opciones;
        public uint Momento;
        public IntPtr Extra;
    }
}
