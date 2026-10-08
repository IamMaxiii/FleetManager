using FleetManager.Models;

namespace FleetManager.Core;

/// <param name="Actividad">Actividad que se debe registrar con esta lectura.</param>
/// <param name="ReclasificarDesde">
/// Si tiene valor, el tramo desde esa hora del juego (que se había apuntado como
/// conducción mientras se esperaba) debe pasar a <paramref name="Actividad"/>.
/// </param>
public sealed record DecisionActividad(Actividad Actividad, DateTime? ReclasificarDesde);

/// <summary>
/// Decide la actividad en cada lectura (opción A2 elegida por el usuario):
///
/// - En marcha: conducción. Se olvida el botón pulsado. "En marcha" es más de
///   <see cref="VelocidadMovimientoClaro"/>, o más de <see cref="VelocidadMovimiento"/>
///   durante <see cref="LecturasParaConfirmarMovimiento"/> lecturas seguidas (así una
///   vibración suelta al ralentí no cuenta, pero maniobrar despacio sí).
/// - Parado con un botón de actividad pulsado: lo que diga el botón.
/// - Parado con el motor apagado: descanso.
/// - Parado con el motor encendido <b>después de conducir</b>: sigue en conducción durante
///   <see cref="EsperaAlParar"/> (tiempo real); si la parada se alarga, pasa a otros trabajos
///   desde el momento en que se paró (un semáforo no cambia nada; una espera larga, sí).
/// - Parado con el motor encendido sin venir de conducir (al arrancar tras un descanso o al
///   abrir la jornada): otros trabajos, como un tacógrafo real con el camión parado.
///
/// Los tiempos reales solo avanzan mientras llegan lecturas (con el juego en pausa no se
/// leen), y cada lectura cuenta como mucho 1 s, para que una pausa o un parón de la
/// aplicación no los dispare.
/// </summary>
public sealed class SelectorActividad
{
    /// <summary>
    /// Por encima de esta velocidad (km/h) el camión puede estar moviéndose. Es baja a
    /// propósito: maniobrar despacio también es conducir. Parado, el juego da exactamente 0.
    /// </summary>
    public const double VelocidadMovimiento = 1;

    /// <summary>Por encima de esta velocidad (km/h) el movimiento cuenta al instante.</summary>
    public const double VelocidadMovimientoClaro = 5;

    /// <summary>Lecturas seguidas (≈ 1 s real) por encima de 1 km/h para dar por bueno un movimiento lento.</summary>
    public const int LecturasParaConfirmarMovimiento = 4;

    /// <summary>Tiempo real parado con el motor encendido, después de conducir, antes de pasar a otros trabajos.</summary>
    public static readonly TimeSpan EsperaAlParar = TimeSpan.FromMinutes(2);

    private static readonly TimeSpan MaximoPorLectura = TimeSpan.FromSeconds(1);

    private readonly TimeProvider reloj;

    private DateTimeOffset? ultimaLectura;
    private int lecturasEnMovimiento;
    private bool vieneDeConducir;
    private DateTime? esperaDesde;
    private TimeSpan esperaAcumulada;
    private bool esperaAgotada;

    public SelectorActividad(TimeProvider reloj)
    {
        this.reloj = reloj;
    }

    /// <summary>Actividad elegida con un botón; vacía si no hay ninguna.</summary>
    public Actividad? ActividadManual { get; private set; }

    /// <summary>
    /// Elige una actividad a mano. Solo tiene efecto con el camión parado: en
    /// cuanto se mueve, vuelve a ser conducción y el botón se olvida.
    /// </summary>
    public void ElegirManual(Actividad actividad) => ActividadManual = actividad;

    /// <summary>Olvida todo (por ejemplo, al abrir una jornada).</summary>
    public void Reiniciar()
    {
        ActividadManual = null;
        ultimaLectura = null;
        lecturasEnMovimiento = 0;
        vieneDeConducir = false;
        TerminarEspera();
    }

    public DecisionActividad Decidir(double velocidad, bool motorEncendido, DateTime horaJuego)
    {
        DateTimeOffset ahora = reloj.GetUtcNow();
        TimeSpan transcurrido = TiempoDesdeLecturaAnterior(ahora);
        ultimaLectura = ahora;

        lecturasEnMovimiento = velocidad > VelocidadMovimiento ? lecturasEnMovimiento + 1 : 0;

        bool enMarcha = velocidad > VelocidadMovimientoClaro ||
                        lecturasEnMovimiento >= LecturasParaConfirmarMovimiento;

        if (enMarcha)
        {
            ActividadManual = null;
            vieneDeConducir = true;
            TerminarEspera();
            return new DecisionActividad(Actividad.Conduccion, null);
        }

        if (ActividadManual is { } elegida)
        {
            vieneDeConducir = false;
            TerminarEspera();
            return new DecisionActividad(elegida, null);
        }

        if (!motorEncendido)
        {
            vieneDeConducir = false;
            TerminarEspera();
            return new DecisionActividad(Actividad.Descanso, null);
        }

        if (!vieneDeConducir || esperaAgotada)
        {
            // Motor encendido sin haberse movido: no es un semáforo.
            return new DecisionActividad(Actividad.OtrosTrabajos, null);
        }

        // Parado con el motor encendido justo después de conducir: puede ser un semáforo.
        if (esperaDesde is null)
        {
            esperaDesde = horaJuego;
            esperaAcumulada = TimeSpan.Zero;
        }
        else
        {
            esperaAcumulada += transcurrido;
        }

        if (esperaAcumulada < EsperaAlParar)
        {
            return new DecisionActividad(Actividad.Conduccion, null);
        }

        esperaAgotada = true;
        vieneDeConducir = false;
        return new DecisionActividad(Actividad.OtrosTrabajos, esperaDesde);
    }

    private TimeSpan TiempoDesdeLecturaAnterior(DateTimeOffset ahora)
    {
        if (ultimaLectura is not { } anterior || ahora <= anterior)
        {
            return TimeSpan.Zero;
        }

        TimeSpan transcurrido = ahora - anterior;
        return transcurrido < MaximoPorLectura ? transcurrido : MaximoPorLectura;
    }

    private void TerminarEspera()
    {
        esperaDesde = null;
        esperaAcumulada = TimeSpan.Zero;
        esperaAgotada = false;
    }
}
