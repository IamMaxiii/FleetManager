namespace FleetManager.Core;

public enum TipoInfraccion
{
    /// <summary>Más de 4 h 30 de conducción sin la pausa obligatoria.</summary>
    ConduccionContinua,

    /// <summary>Más de 9 h de conducción diaria sin ampliaciones disponibles, o más de 10 h.</summary>
    ConduccionDiaria,

    /// <summary>Más de 56 h de conducción en una semana.</summary>
    ConduccionSemanal,

    /// <summary>Más de 90 h de conducción en dos semanas consecutivas.</summary>
    ConduccionBisemanal,

    /// <summary>El descanso diario no se hizo dentro de las 24 h.</summary>
    DescansoDiarioFueraDePlazo,

    /// <summary>Más de 3 descansos diarios reducidos entre dos descansos semanales.</summary>
    DemasiadosDescansosReducidos,

    /// <summary>El descanso semanal no empezó dentro de los 6 periodos de 24 h.</summary>
    DescansoSemanalFueraDePlazo,

    /// <summary>Dos descansos semanales reducidos seguidos.</summary>
    DescansosSemanalesReducidosSeguidos
}

/// <param name="Tipo">Qué norma se incumplió.</param>
/// <param name="Momento">Hora del juego en que se incumplió.</param>
/// <param name="Descripcion">Explicación para el usuario.</param>
public sealed record Infraccion(TipoInfraccion Tipo, DateTime Momento, string Descripcion);
