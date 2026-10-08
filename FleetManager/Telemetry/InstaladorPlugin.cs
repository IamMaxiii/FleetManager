using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using FleetManager.Storage;

namespace FleetManager.Telemetry;

/// <summary>Cómo está el plugin de telemetría en la carpeta del juego.</summary>
public enum EstadoPlugin
{
    /// <summary>FleetManager no trae su copia del plugin (no debería pasar).</summary>
    SinCopiaIncluida,
    JuegoNoEncontrado,
    Falta,
    /// <summary>Hay un scs-telemetry.dll distinto del que trae FleetManager.</summary>
    OtraVersion,
    Instalado
}

/// <summary>Resultado de intentar instalar el plugin.</summary>
public enum ResultadoPlugin
{
    Hecho,
    /// <summary>El usuario dijo que no al permiso de administrador.</summary>
    Cancelado,
    /// <summary>El archivo está en uso: el juego está abierto.</summary>
    JuegoAbierto,
    Error
}

/// <summary>
/// Comprueba e instala el plugin de telemetría (scs-telemetry.dll) que FleetManager
/// necesita para leer el juego. FleetManager trae su copia en la carpeta "plugin".
/// Si la carpeta del juego está protegida (Archivos de programa), la copia la hace
/// FleetManager otra vez, con permiso de administrador (<see cref="ArgumentoInstalar"/>).
/// </summary>
public sealed class InstaladorPlugin
{
    public const string NombreArchivo = "scs-telemetry.dll";

    /// <summary>Argumento con el que FleetManager arranca solo para copiar el plugin.</summary>
    public const string ArgumentoInstalar = "--instalar-plugin";

    // Códigos de salida del modo "solo copiar el plugin".
    private const int CodigoHecho = 0;
    private const int CodigoError = 1;
    private const int CodigoEnUso = 2;
    private const int CodigoSinPermiso = 3;

    private readonly string pluginIncluido;
    private readonly Func<string?> buscarJuego;
    private readonly Func<string, int?> copiarComoAdministrador;
    private readonly Registro registro;

    /// <param name="pluginIncluido">La copia del plugin que trae FleetManager.</param>
    /// <param name="buscarJuego">Devuelve la carpeta del juego, o null.</param>
    /// <param name="copiarComoAdministrador">
    /// Copia el plugin a esa carpeta con permiso de administrador; devuelve el código
    /// de salida, o null si el usuario no da el permiso.
    /// </param>
    public InstaladorPlugin(string pluginIncluido, Func<string?> buscarJuego, Func<string, int?> copiarComoAdministrador, Registro registro)
    {
        this.pluginIncluido = pluginIncluido;
        this.buscarJuego = buscarJuego;
        this.copiarComoAdministrador = copiarComoAdministrador;
        this.registro = registro;
    }

    /// <summary>La copia del plugin que trae FleetManager (carpeta "plugin" junto al exe).</summary>
    public static string PluginJuntoALaApp => Path.Combine(AppContext.BaseDirectory, "plugin", NombreArchivo);

    public static string CarpetaPlugins(string carpetaJuego) => Path.Combine(carpetaJuego, "bin", "win_x64", "plugins");

    /// <summary>Carpeta del juego encontrada en la última comprobación.</summary>
    public string? CarpetaJuego { get; private set; }

    public EstadoPlugin Comprobar()
    {
        CarpetaJuego = null;

        if (!File.Exists(pluginIncluido))
        {
            return EstadoPlugin.SinCopiaIncluida;
        }

        CarpetaJuego = buscarJuego();

        if (CarpetaJuego is null)
        {
            return EstadoPlugin.JuegoNoEncontrado;
        }

        string instalado = Path.Combine(CarpetaPlugins(CarpetaJuego), NombreArchivo);

        if (!File.Exists(instalado))
        {
            return EstadoPlugin.Falta;
        }

        try
        {
            return File.ReadAllBytes(instalado).AsSpan().SequenceEqual(File.ReadAllBytes(pluginIncluido))
                ? EstadoPlugin.Instalado
                : EstadoPlugin.OtraVersion;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // No se puede leer (por ejemplo, el juego lo tiene abierto): está, y lo está usando.
            return EstadoPlugin.Instalado;
        }
    }

    /// <summary>Copia el plugin a la carpeta del juego; si hace falta, pide permiso de administrador.</summary>
    public ResultadoPlugin Instalar()
    {
        if (CarpetaJuego is null)
        {
            return ResultadoPlugin.Error;
        }

        string destino = CarpetaPlugins(CarpetaJuego);
        int codigo = CopiarConCodigo(pluginIncluido, destino, registro);

        if (codigo == CodigoSinPermiso)
        {
            registro.Info("La carpeta del juego está protegida: se pide permiso de administrador para copiar el plugin.");
            int? codigoAdministrador = copiarComoAdministrador(destino);

            if (codigoAdministrador is null)
            {
                return ResultadoPlugin.Cancelado;
            }

            codigo = codigoAdministrador.Value;
        }

        ResultadoPlugin resultado = codigo switch
        {
            CodigoHecho => ResultadoPlugin.Hecho,
            CodigoEnUso => ResultadoPlugin.JuegoAbierto,
            _ => ResultadoPlugin.Error
        };

        registro.Info($"Instalación del plugin en {destino}: {resultado}.");
        return resultado;
    }

    /// <summary>
    /// Modo "solo copiar el plugin" (FleetManager arrancado con permiso de administrador).
    /// Solo acepta como destino una carpeta "bin\win_x64\plugins".
    /// </summary>
    public static int CopiarComoProcesoAparte(string carpetaDestino, Registro registro)
    {
        string carpeta = Path.GetFullPath(carpetaDestino).TrimEnd('\\');

        if (!carpeta.EndsWith(@"\bin\win_x64\plugins", StringComparison.OrdinalIgnoreCase))
        {
            registro.Info($"Destino del plugin no válido: {carpeta}");
            return CodigoError;
        }

        return CopiarConCodigo(PluginJuntoALaApp, carpeta, registro);
    }

    /// <summary>Arranca FleetManager con permiso de administrador solo para copiar el plugin.</summary>
    public static int? CopiarComoAdministrador(string carpetaDestino)
    {
        var inicio = new ProcessStartInfo(Environment.ProcessPath!)
        {
            UseShellExecute = true,
            Verb = "runas",
            Arguments = $"{ArgumentoInstalar} \"{carpetaDestino}\""
        };

        try
        {
            using Process? proceso = Process.Start(inicio);

            if (proceso is null)
            {
                return CodigoError;
            }

            proceso.WaitForExit();
            return proceso.ExitCode;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) // el usuario pulsó "No"
        {
            return null;
        }
    }

    private static int CopiarConCodigo(string origen, string carpetaDestino, Registro registro)
    {
        try
        {
            Directory.CreateDirectory(carpetaDestino);
            File.Copy(origen, Path.Combine(carpetaDestino, NombreArchivo), overwrite: true);
            return CodigoHecho;
        }
        catch (UnauthorizedAccessException)
        {
            return CodigoSinPermiso;
        }
        catch (IOException ex)
        {
            registro.Error("No se pudo copiar el plugin (¿juego abierto?)", ex);
            return CodigoEnUso;
        }
    }
}
