using FleetManager.Models;
using SCSSdkClient;
using SCSSdkClient.Object;

namespace FleetManager.Telemetry;

/// <summary>
/// Lee la memoria compartida del plugin scs-sdk-plugin cuando se le pide y
/// devuelve un <see cref="DatosJuego"/>. Es el único sitio de la aplicación
/// que usa el SDK. No tiene temporizador propio: quien lo usa decide el ritmo.
/// </summary>
public sealed class LectorJuego : IDisposable
{
    // Nombre de la memoria compartida que crea el plugin dentro del juego.
    private const string NombreMemoria = @"Local\SCSTelemetry";

    private readonly SharedMemory memoria = new();
    private readonly DetectorCuelgue detectorCuelgue;
    private bool conectado;

    public LectorJuego(TimeProvider reloj)
    {
        detectorCuelgue = new DetectorCuelgue(reloj, DetectorCuelgue.EsperaPorDefecto);
    }

    /// <summary>
    /// Lee una vez la memoria del plugin. Nunca lanza excepciones: cualquier
    /// fallo se devuelve con <see cref="EstadoJuego.Error"/>.
    /// </summary>
    public DatosJuego Leer()
    {
        try
        {
            if (!conectado)
            {
                memoria.Connect(NombreMemoria);
                conectado = true;
            }

            if (!memoria.Hooked)
            {
                return new DatosJuego
                {
                    Estado = EstadoJuego.Error,
                    Error = memoria.HookException?.Message ?? "No se pudo abrir la memoria del plugin."
                };
            }

            SCSTelemetry datos = memoria.Update<SCSTelemetry>();
            EstadoJuego estado = InterpreteEstadoJuego.Interpretar(true, datos.SdkActive, datos.Game == SCSGame.Ets2);

            if (estado != EstadoJuego.Detectado)
            {
                detectorCuelgue.Reiniciar();
                return new DatosJuego { Estado = estado, VersionPlugin = datos.DllVersion };
            }

            if (detectorCuelgue.EstaColgado(datos.Timestamp, datos.Paused))
            {
                return new DatosJuego { Estado = EstadoJuego.NoResponde, VersionPlugin = datos.DllVersion };
            }

            return Convertir(datos);
        }
        catch (Exception ex)
        {
            return new DatosJuego { Estado = EstadoJuego.Error, Error = ex.Message };
        }
    }

    public void Dispose()
    {
        if (conectado)
        {
            memoria.Disconnect();
            conectado = false;
        }
    }

    private static DatosJuego Convertir(SCSTelemetry datos)
    {
        var camion = datos.TruckValues;
        var constantes = camion.ConstantsValues;
        var actual = camion.CurrentValues;
        var salpicadero = actual.DashboardValues;
        var danos = actual.DamageValues;
        var encargo = datos.JobValues;
        var posicion = actual.PositionValue.Position;

        bool hayEncargo = !string.IsNullOrEmpty(encargo.CargoValues.Name);
        var remolque = datos.TrailerValues is { Length: > 0 } remolques ? remolques[0] : null;
        bool remolqueEnganchado = remolque is { Attached: true };

        return new DatosJuego
        {
            Estado = EstadoJuego.Detectado,
            VersionPlugin = datos.DllVersion,
            Pausado = datos.Paused,
            HoraJuego = datos.CommonValues.GameTime.Date,

            Velocidad = Math.Abs(salpicadero.Speed.Kph),
            LimiteVelocidad = Math.Max(0, datos.NavigationValues.SpeedLimit.Kph),
            Odometro = salpicadero.Odometer,
            MotorEncendido = actual.EngineEnabled,
            FrenoMano = actual.MotorValues.BrakeValues.ParkingBrake,

            Origen = encargo.CitySource ?? "",
            Destino = encargo.CityDestination ?? "",
            Carga = encargo.CargoValues.Name ?? "",
            PesoCarga = encargo.CargoValues.Mass,
            DanoCarga = encargo.CargoValues.CargoDamage,
            DistanciaPlanificadaKm = encargo.PlannedDistanceKm,
            HoraEntrega = hayEncargo ? encargo.DeliveryTime.Date : null,

            Marca = constantes.Brand ?? "",
            Modelo = constantes.Name ?? "",
            Matricula = constantes.LicensePlate ?? "",
            Combustible = salpicadero.FuelValue.Amount,
            CapacidadCombustible = constantes.CapacityValues.Fuel,
            AdBlue = salpicadero.AdBlue,
            CapacidadAdBlue = constantes.CapacityValues.AdBlue,
            DesgasteMotor = danos.Engine,
            DesgasteTransmision = danos.Transmission,
            DesgasteCabina = danos.Cabin,
            DesgasteChasis = danos.Chassis,
            DesgasteRuedas = danos.WheelsAvg,
            PosicionX = posicion.X,
            PosicionY = posicion.Y,
            PosicionZ = posicion.Z,
            Rumbo = actual.PositionValue.Orientation.Heading,

            RemolqueEnganchado = remolqueEnganchado,
            Remolque = remolqueEnganchado ? $"{remolque!.Brand} {remolque.Name}".Trim() : "",
            DesgasteRemolque = remolqueEnganchado
                ? (remolque!.DamageValues.Body + remolque.DamageValues.Chassis + remolque.DamageValues.Wheels) / 3.0
                : 0
        };
    }
}
