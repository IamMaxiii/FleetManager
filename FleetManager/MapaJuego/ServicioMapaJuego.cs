using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using FleetManager.Idiomas;
using FleetManager.Storage;

namespace FleetManager.MapaJuego;

/// <summary>Cómo va la generación del mapa.</summary>
public sealed record ProgresoMapa(string Texto, double Fraccion);

/// <summary>Una línea que escribe el generador del mapa.</summary>
public sealed record LineaGenerador(bool EsProgreso, bool EsFin, string? Error, ProgresoMapa? Progreso)
{
    /// <summary>
    /// Entiende "PROGRESO|0.5|clave|valores...", "HECHO" y "ERROR|mensaje"; cualquier otra
    /// cosa se ignora (vacío). El texto del progreso se traduce al idioma de FleetManager.
    /// </summary>
    public static LineaGenerador? Interpretar(string linea)
    {
        string[] partes = linea.Split('|', 3);

        return partes[0] switch
        {
            "PROGRESO" when partes.Length == 3 &&
                            double.TryParse(partes[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double fraccion)
                => new LineaGenerador(true, false, null, new ProgresoMapa(Traducir(partes[2]), Math.Clamp(fraccion, 0, 1))),
            "HECHO" => new LineaGenerador(false, true, null, null),
            "ERROR" => new LineaGenerador(false, false, partes.Length > 1 ? partes[1] : Textos.T("Mapa.ErrorDesconocido"), null),
            _ => null
        };
    }

    /// <summary>"Generador.Dibujando|100|400" → el texto de esa clave con los números.</summary>
    private static string Traducir(string claveYValores)
    {
        string[] partes = claveYValores.Split('|');
        object[] valores = partes.Skip(1)
            .Select(v => long.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out long numero) ? (object)numero : v)
            .ToArray();

        return Textos.T(partes[0], valores);
    }
}

/// <summary>
/// El mapa del juego en FleetManager: busca el juego, carga el mapa ya generado
/// (teselas y nombres de ciudades) y, cuando se pide, lanza el programa generador
/// (FleetManager.GeneradorMapa.exe) y va leyendo su progreso.
///
/// Cada mapa se guarda en una carpeta con la huella del juego. Si el juego cambia
/// (actualización o DLC nuevo), se sigue usando el mapa anterior hasta que se genere
/// el nuevo.
/// </summary>
public sealed class ServicioMapaJuego
{
    public const string ProgramaGenerador = "FleetManager.GeneradorMapa.exe";

    private readonly string carpetaBase;
    private readonly Registro registro;

    public ServicioMapaJuego(string carpetaBase, Registro registro)
    {
        this.carpetaBase = carpetaBase;
        this.registro = registro;
    }

    public string? CarpetaJuego { get; private set; }

    public string? HuellaActual { get; private set; }

    public InfoMapaJuego? Info { get; private set; }

    public IReadOnlyList<CiudadMapa> Ciudades { get; private set; } = [];

    public string? CarpetaTeselas { get; private set; }

    public bool Disponible => Info is not null;

    /// <summary>Hay mapa, pero se hizo con otra versión del juego o con otros DLC.</summary>
    public bool NecesitaActualizar => Info is not null && HuellaActual is not null && (Info.Huella != HuellaActual || Info.Version < TeselasMapa.VersionFormato);

    /// <summary>Busca el juego y carga el mapa generado más adecuado (el de la versión actual, o el último que haya).</summary>
    public void Cargar()
    {
        try
        {
            CarpetaJuego = BuscadorJuego.Buscar();
            HuellaActual = CarpetaJuego is null ? null : BuscadorJuego.Huella(CarpetaJuego);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            registro.Aviso("No se pudo comprobar la instalación del juego", ex);
        }

        Info = null;
        Ciudades = [];
        CarpetaTeselas = null;

        if (!Directory.Exists(carpetaBase))
        {
            return;
        }

        var candidatas = new DirectoryInfo(carpetaBase).GetDirectories()
            .Where(d => !d.Name.EndsWith(".generando", StringComparison.Ordinal) && File.Exists(Path.Combine(d.FullName, "info.json")))
            .OrderByDescending(d => d.Name == HuellaActual)
            .ThenByDescending(d => d.LastWriteTimeUtc);

        foreach (DirectoryInfo carpeta in candidatas)
        {
            try
            {
                InfoMapaJuego? info = JsonSerializer.Deserialize<InfoMapaJuego>(File.ReadAllText(Path.Combine(carpeta.FullName, "info.json")));
                string archivoCiudades = Path.Combine(carpeta.FullName, "ciudades.json");

                if (info is null)
                {
                    continue;
                }

                Ciudades = File.Exists(archivoCiudades)
                    ? JsonSerializer.Deserialize<List<CiudadMapa>>(File.ReadAllText(archivoCiudades)) ?? []
                    : [];
                Info = info;
                CarpetaTeselas = carpeta.FullName;
                return;
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                registro.Aviso($"No se pudo cargar el mapa de {carpeta.FullName}", ex);
            }
        }
    }

    /// <summary>
    /// Genera el mapa con el programa generador. Mientras tanto se puede seguir usando
    /// la aplicación.
    /// </summary>
    /// <returns>Vacío si ha ido bien; si no, el motivo.</returns>
    public async Task<string?> Generar(IProgress<ProgresoMapa> progreso, CancellationToken cancelar)
    {
        if (CarpetaJuego is null || HuellaActual is null)
        {
            return Textos.T("Mapa.SinJuego");
        }

        string programa = Path.Combine(AppContext.BaseDirectory, ProgramaGenerador);

        if (!File.Exists(programa))
        {
            return Textos.T("Mapa.SinGenerador", ProgramaGenerador);
        }

        string destino = Path.Combine(carpetaBase, HuellaActual);
        Directory.CreateDirectory(carpetaBase);

        var inicio = new ProcessStartInfo(programa)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            StandardOutputEncoding = Encoding.UTF8
        };
        inicio.ArgumentList.Add(CarpetaJuego);
        inicio.ArgumentList.Add(destino);
        inicio.ArgumentList.Add(HuellaActual);

        registro.Info($"Generando el mapa del juego en {destino}");
        using Process proceso = Process.Start(inicio) ?? throw new InvalidOperationException("No se pudo lanzar el generador del mapa.");
        string? error = null;
        bool hecho = false;

        try
        {
            while (await proceso.StandardOutput.ReadLineAsync(cancelar) is { } texto)
            {
                switch (LineaGenerador.Interpretar(texto))
                {
                    case { EsProgreso: true, Progreso: { } valor }:
                        progreso.Report(valor);
                        break;
                    case { EsFin: true }:
                        hecho = true;
                        break;
                    case { Error: { } mensaje }:
                        error = mensaje;
                        break;
                }
            }

            await proceso.WaitForExitAsync(cancelar);
        }
        catch (OperationCanceledException)
        {
            proceso.Kill(entireProcessTree: true);
            registro.Info("Generación del mapa cancelada.");
            return Textos.T("Mapa.Cancelado");
        }

        if (!hecho || proceso.ExitCode != 0)
        {
            error ??= Textos.T("Mapa.CodigoSalida", proceso.ExitCode);
            registro.Error($"No se pudo generar el mapa: {error}");
            return error;
        }

        registro.Info("Mapa del juego generado.");
        Cargar();
        BorrarMapasAntiguos(destino);
        return null;
    }

    private void BorrarMapasAntiguos(string actual)
    {
        foreach (string carpeta in Directory.GetDirectories(carpetaBase).Where(c => !string.Equals(c, actual, StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                Directory.Delete(carpeta, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                registro.Aviso($"No se pudo borrar el mapa antiguo {carpeta}", ex);
            }
        }
    }
}
