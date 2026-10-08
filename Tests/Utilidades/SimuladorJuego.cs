using FleetManager.Core;
using FleetManager.Models;

namespace FleetManager.Tests.Utilidades;

/// <summary>
/// Imita al juego para probar el <see cref="ServicioTacografo"/>: envía lecturas
/// como lo hará la aplicación (4 por segundo real) con el reloj del juego
/// avanzando 1 minuto cada 3 segundos reales, como en ETS2.
/// </summary>
public sealed class SimuladorJuego
{
    public static readonly TimeSpan IntervaloLectura = TimeSpan.FromMilliseconds(250);

    /// <summary>Lecturas por minuto de juego (3 s reales / 250 ms).</summary>
    public const int LecturasPorMinuto = 12;

    private readonly ServicioTacografo servicio;
    private readonly RelojFalso reloj;

    public SimuladorJuego(ServicioTacografo servicio, RelojFalso reloj, DateTime inicio)
    {
        this.servicio = servicio;
        this.reloj = reloj;
        Hora = inicio;
    }

    public DateTime Hora { get; private set; }

    public double Odometro { get; set; } = 10_000;

    public string Origen { get; set; } = "Madrid";

    public string Destino { get; set; } = "Zaragoza";

    public string Carga { get; set; } = "Maquinaria";

    /// <summary>Posición del camión en el mundo del juego (al conducir avanza hacia el este, X positiva).</summary>
    public double X { get; set; } = 1000;

    public double Z { get; set; } = 2000;

    /// <summary>Una sola lectura en la hora actual.</summary>
    public SimuladorJuego Leer(double velocidad = 0, bool motor = true, double limite = 0, bool pausado = false)
    {
        reloj.Avanzar(IntervaloLectura);
        servicio.ProcesarLectura(new DatosJuego
        {
            Estado = EstadoJuego.Detectado,
            Pausado = pausado,
            HoraJuego = Hora,
            Velocidad = velocidad,
            LimiteVelocidad = limite,
            Odometro = Odometro,
            MotorEncendido = motor,
            Origen = Origen,
            Destino = Destino,
            Carga = Carga,
            PosicionX = X,
            PosicionZ = Z,
            Rumbo = 0.75 // mirando al este
        });
        return this;
    }

    public SimuladorJuego Conducir(int minutos, double velocidad = 80, double limite = 0)
    {
        for (int minuto = 0; minuto < minutos; minuto++)
        {
            Hora += TimeSpan.FromMinutes(1);

            for (int i = 0; i < LecturasPorMinuto; i++)
            {
                double kilometros = velocidad / 60.0 / LecturasPorMinuto;
                Odometro += kilometros;
                X += kilometros * 1000;
                Leer(velocidad, motor: true, limite);
            }
        }

        return this;
    }

    public SimuladorJuego Parar(int minutos, bool motor = true)
    {
        for (int minuto = 0; minuto < minutos; minuto++)
        {
            Hora += TimeSpan.FromMinutes(1);

            for (int i = 0; i < LecturasPorMinuto; i++)
            {
                Leer(0, motor);
            }
        }

        return this;
    }

    /// <summary>Salto del reloj del juego (dormir, cargar partida) seguido de una lectura.</summary>
    public SimuladorJuego Saltar(TimeSpan cuanto, bool motor = false)
    {
        Hora += cuanto;
        return Leer(0, motor);
    }
}
