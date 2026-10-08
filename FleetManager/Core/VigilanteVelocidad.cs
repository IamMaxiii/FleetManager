namespace FleetManager.Core;

/// <summary>
/// Cuánto tiempo se lleva por encima del límite de velocidad.
/// </summary>
public enum NivelVelocidad
{
    /// <summary>Dentro del límite (o sin límite conocido).</summary>
    Correcta,

    /// <summary>Por encima del límite + tolerancia, menos de 10 s.</summary>
    Exceso,

    /// <summary>10 s o más por encima: se muestra en rojo.</summary>
    ExcesoProlongado,

    /// <summary>30 s o más por encima: símbolo de peligro.</summary>
    Peligro,

    /// <summary>60 s o más por encima: falta de velocidad registrada.</summary>
    Falta
}

/// <summary>
/// Vigila la velocidad respecto al límite de la vía (+ <see cref="Tolerancia"/> km/h).
/// Los tiempos son reales y solo cuentan mientras llegan lecturas (con el juego
/// en pausa no se leen); cada lectura cuenta como mucho 1 s. Se anota una sola
/// falta por episodio: hay que bajar del límite para que pueda haber otra.
/// </summary>
public sealed class VigilanteVelocidad
{
    public const double Tolerancia = 0.5;

    public static readonly TimeSpan TiempoExcesoProlongado = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan TiempoPeligro = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan TiempoFalta = TimeSpan.FromSeconds(60);

    private static readonly TimeSpan MaximoPorLectura = TimeSpan.FromSeconds(1);

    private readonly TimeProvider reloj;
    private DateTimeOffset? ultimaLectura;
    private bool faltaAnotada;

    public VigilanteVelocidad(TimeProvider reloj)
    {
        this.reloj = reloj;
    }

    public NivelVelocidad Nivel { get; private set; }

    /// <summary>Tiempo seguido por encima del límite en el episodio actual.</summary>
    public TimeSpan TiempoExceso { get; private set; }

    /// <returns>Verdadero solo en la lectura en que se comete una falta nueva.</returns>
    public bool Actualizar(double velocidad, double limite)
    {
        DateTimeOffset ahora = reloj.GetUtcNow();
        TimeSpan transcurrido = ultimaLectura is { } anterior && ahora > anterior
            ? (ahora - anterior < MaximoPorLectura ? ahora - anterior : MaximoPorLectura)
            : TimeSpan.Zero;
        ultimaLectura = ahora;

        if (limite <= 0 || velocidad <= limite + Tolerancia)
        {
            Reiniciar();
            return false;
        }

        TiempoExceso += transcurrido;

        Nivel = TiempoExceso >= TiempoFalta ? NivelVelocidad.Falta
            : TiempoExceso >= TiempoPeligro ? NivelVelocidad.Peligro
            : TiempoExceso >= TiempoExcesoProlongado ? NivelVelocidad.ExcesoProlongado
            : NivelVelocidad.Exceso;

        if (Nivel == NivelVelocidad.Falta && !faltaAnotada)
        {
            faltaAnotada = true;
            return true;
        }

        return false;
    }

    public void Reiniciar()
    {
        TiempoExceso = TimeSpan.Zero;
        Nivel = NivelVelocidad.Correcta;
        faltaAnotada = false;
    }
}
