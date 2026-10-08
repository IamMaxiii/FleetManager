using FleetManager.Models;

namespace FleetManager.Core;

/// <summary>
/// Foto completa del tacógrafo en un momento: todo lo que se muestra en pantalla.
/// La calcula <see cref="AnalizadorTacografo"/> a partir del registro.
/// Los tiempos "restantes" nunca son negativos.
/// </summary>
public sealed record EstadoTacografo
{
    /// <summary>Hora del juego de la última lectura.</summary>
    public DateTime Ahora { get; init; }

    public Actividad ActividadActual { get; init; } = Actividad.Descanso;

    /// <summary>Tiempo seguido en la actividad actual.</summary>
    public TimeSpan TiempoActividadActual { get; init; }

    // ---------- Conducción continua y pausa ----------

    /// <summary>Conducción desde la última pausa válida.</summary>
    public TimeSpan ConduccionContinua { get; init; }

    public TimeSpan ConduccionContinuaRestante { get; init; }

    /// <summary>Ya se hizo la primera parte (15 min) de una pausa dividida.</summary>
    public bool PrimeraPartePausaHecha { get; init; }

    /// <summary>Lo que falta de la pausa: 45 min, 30 min si ya se hicieron los 15, o lo que quede de la pausa en curso.</summary>
    public TimeSpan PausaRestante { get; init; }

    // ---------- Conducción diaria ----------

    /// <summary>Conducción desde el final del último descanso diario o semanal (no desde medianoche).</summary>
    public TimeSpan ConduccionDiaria { get; init; }

    /// <summary>9 h, o 10 h si hay ampliación disponible o ya se está usando.</summary>
    public TimeSpan LimiteConduccionDiaria { get; init; }

    public TimeSpan ConduccionDiariaRestante { get; init; }

    /// <summary>Ampliaciones a 10 h que quedan esta semana.</summary>
    public int AmpliacionesRestantes { get; init; }

    /// <summary>Final del último descanso diario o semanal: inicio de la jornada diaria.</summary>
    public DateTime InicioJornadaDiaria { get; init; }

    /// <summary>Tiempo desde el inicio de la jornada diaria.</summary>
    public TimeSpan AmplitudJornada { get; init; }

    // ---------- Descanso diario ----------

    /// <summary>Duración del descanso en curso (cero si no se está descansando).</summary>
    public TimeSpan DescansoActual { get; init; }

    /// <summary>Lo que falta para completar el descanso diario (11 h, o 9 h si es la segunda parte de uno dividido).</summary>
    public TimeSpan DescansoDiarioRestante { get; init; }

    /// <summary>Ya se hizo la primera parte (3 h) de un descanso diario dividido.</summary>
    public bool PrimeraParteDescansoDivididoHecha { get; init; }

    public int DescansosReducidosRestantes { get; init; }

    /// <summary>Hora límite para haber terminado el descanso diario (24 h tras el anterior).</summary>
    public DateTime PlazoDescansoDiario { get; init; }

    /// <summary>Hora límite para empezar el descanso diario y que quepa en el plazo.</summary>
    public DateTime LimiteInicioDescansoDiario { get; init; }

    // ---------- Semanal y bisemanal ----------

    /// <summary>Conducción de esta semana (lunes 00:00 a domingo 24:00).</summary>
    public TimeSpan ConduccionSemanal { get; init; }

    public TimeSpan ConduccionSemanalRestante { get; init; }

    public TimeSpan ConduccionSemanaAnterior { get; init; }

    /// <summary>Conducción de esta semana más la anterior.</summary>
    public TimeSpan ConduccionBisemanal { get; init; }

    public TimeSpan ConduccionBisemanalRestante { get; init; }

    // ---------- Descanso semanal ----------

    /// <summary>Hora límite para empezar el descanso semanal (6 × 24 h tras el anterior).</summary>
    public DateTime PlazoDescansoSemanal { get; init; }

    /// <summary>El descanso en curso ya dura 24 h o más.</summary>
    public bool DescansoSemanalEnCurso { get; init; }

    // ---------- Resumen ----------

    /// <summary>Lo que se puede conducir antes de tener que parar por cualquier motivo.</summary>
    public TimeSpan SiguienteParada { get; init; }

    public IReadOnlyList<Infraccion> Infracciones { get; init; } = [];
}
