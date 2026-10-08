using System.IO;

namespace FleetManager.Storage;

/// <summary>
/// Dónde vive cada archivo de datos. Por defecto en %LOCALAPPDATA%\FleetManager;
/// las pruebas automáticas usan una carpeta temporal.
/// </summary>
public sealed class RutasDatos
{
    public RutasDatos(string carpeta)
    {
        Carpeta = carpeta;
    }

    public static RutasDatos PorDefecto() =>
        new(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FleetManager"));

    public string Carpeta { get; }

    public string Historial => Path.Combine(Carpeta, "historial.json");

    public string Perfil => Path.Combine(Carpeta, "perfil.json");

    public string Ajustes => Path.Combine(Carpeta, "ajustes.json");

    /// <summary>Registro de actividades del tacógrafo.</summary>
    public string Tacografo => Path.Combine(Carpeta, "tacografo.json");

    /// <summary>Recorridos de las jornadas (uno por jornada).</summary>
    public string CarpetaRecorridos => Path.Combine(Carpeta, "recorridos");

    /// <summary>Recorrido de una jornada.</summary>
    public string Recorrido(Guid jornada) => Path.Combine(CarpetaRecorridos, $"{jornada}.json");

    /// <summary>Mapa del juego generado (teselas y nombres de ciudades).</summary>
    public string CarpetaMapaJuego => Path.Combine(Carpeta, "mapa-juego");

    /// <summary>Copias de seguridad diarias.</summary>
    public string CarpetaCopias => Path.Combine(Carpeta, "copias");

    public string Registro => Path.Combine(Carpeta, "registro.log");

    /// <summary>Jornadas de la versión antigua de FleetManager (solo se leen, nunca se modifican).</summary>
    public string SesionesAntiguas => Path.Combine(Carpeta, "driving_sessions.dat");

    /// <summary>Perfil de la versión antigua de FleetManager (solo se lee, nunca se modifica).</summary>
    public string PerfilAntiguo => Path.Combine(Carpeta, "driver_profile.dat");
}
