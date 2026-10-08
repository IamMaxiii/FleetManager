namespace FleetManager.Models;

/// <summary>
/// Estado de la conexión con el juego, visto desde FleetManager.
/// </summary>
public enum EstadoJuego
{
    /// <summary>No hay datos del plugin: el juego está cerrado o el plugin no está instalado.</summary>
    NoDetectado,

    /// <summary>El plugin funciona y el juego es Euro Truck Simulator 2.</summary>
    Detectado,

    /// <summary>El plugin funciona pero el juego no es ETS2 (por ejemplo, American Truck Simulator).</summary>
    JuegoNoCompatible,

    /// <summary>El plugin dice que el juego está activo, pero su reloj interno lleva varios segundos parado sin estar en pausa (cuelgue o cierre brusco).</summary>
    NoResponde,

    /// <summary>No se pudo abrir la memoria compartida del plugin.</summary>
    Error
}

/// <summary>
/// Una lectura del juego con solo lo que usa FleetManager. Se rellena en
/// Telemetry/LectorJuego a partir del SDK; el resto de la aplicación solo
/// conoce este tipo, nunca el SDK.
/// </summary>
public sealed record DatosJuego
{
    public static readonly DatosJuego SinConexion = new() { Estado = EstadoJuego.NoDetectado };

    public EstadoJuego Estado { get; init; }

    /// <summary>Texto con el motivo del error, si <see cref="Estado"/> es <see cref="EstadoJuego.Error"/>.</summary>
    public string? Error { get; init; }

    public uint VersionPlugin { get; init; }

    /// <summary>Menú, mapa o pausa abiertos: el reloj del juego no avanza.</summary>
    public bool Pausado { get; init; }

    /// <summary>Hora del juego (el SDK la cuenta desde el lunes 1/1/0001, con precisión de minutos).</summary>
    public DateTime HoraJuego { get; init; }

    public bool Conectado => Estado == EstadoJuego.Detectado;

    // ---------- Conducción ----------

    /// <summary>Velocidad en km/h (siempre positiva, también marcha atrás).</summary>
    public double Velocidad { get; init; }

    /// <summary>Límite de velocidad de la vía en km/h; 0 si no hay.</summary>
    public double LimiteVelocidad { get; init; }

    public double Odometro { get; init; }

    public bool MotorEncendido { get; init; }

    public bool FrenoMano { get; init; }

    // ---------- Encargo ----------

    public string Origen { get; init; } = "";

    public string Destino { get; init; } = "";

    public string Carga { get; init; } = "";

    /// <summary>Peso de la carga en kg.</summary>
    public double PesoCarga { get; init; }

    /// <summary>Daño de la carga de 0 a 1.</summary>
    public double DanoCarga { get; init; }

    public uint DistanciaPlanificadaKm { get; init; }

    /// <summary>Hora del juego límite de entrega; vacía si no hay encargo.</summary>
    public DateTime? HoraEntrega { get; init; }

    // ---------- Camión ----------

    public string Marca { get; init; } = "";

    public string Modelo { get; init; } = "";

    public string Matricula { get; init; } = "";

    public double Combustible { get; init; }

    public double CapacidadCombustible { get; init; }

    public double AdBlue { get; init; }

    public double CapacidadAdBlue { get; init; }

    /// <summary>Desgastes de 0 a 1.</summary>
    public double DesgasteMotor { get; init; }

    public double DesgasteTransmision { get; init; }

    public double DesgasteCabina { get; init; }

    public double DesgasteChasis { get; init; }

    public double DesgasteRuedas { get; init; }

    public double PosicionX { get; init; }

    public double PosicionY { get; init; }

    public double PosicionZ { get; init; }

    /// <summary>
    /// Hacia dónde mira el camión, en fracción de vuelta: 0 = norte, 0,25 = oeste,
    /// 0,5 = sur, 0,75 = este (en el juego, el norte es hacia Z negativa).
    /// </summary>
    public double Rumbo { get; init; }

    // ---------- Remolque ----------

    public bool RemolqueEnganchado { get; init; }

    public string Remolque { get; init; } = "";

    public double DesgasteRemolque { get; init; }
}
