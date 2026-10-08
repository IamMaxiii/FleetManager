namespace FleetManager.Core;

/// <summary>
/// Límites del Reglamento (CE) 561/2006 sobre tiempos de conducción y descanso.
/// </summary>
public static class ReglasUE
{
    // ---------- Conducción continua y pausas ----------

    /// <summary>Conducción máxima sin hacer la pausa.</summary>
    public static readonly TimeSpan ConduccionContinuaMaxima = TimeSpan.FromHours(4.5);

    /// <summary>Pausa completa de una vez.</summary>
    public static readonly TimeSpan PausaCompleta = TimeSpan.FromMinutes(45);

    /// <summary>Primera parte de la pausa dividida.</summary>
    public static readonly TimeSpan PausaPrimeraParte = TimeSpan.FromMinutes(15);

    /// <summary>Segunda parte de la pausa dividida (después de la primera).</summary>
    public static readonly TimeSpan PausaSegundaParte = TimeSpan.FromMinutes(30);

    // ---------- Conducción diaria ----------

    public static readonly TimeSpan ConduccionDiariaMaxima = TimeSpan.FromHours(9);

    public static readonly TimeSpan ConduccionDiariaAmpliada = TimeSpan.FromHours(10);

    /// <summary>Veces por semana que se puede ampliar la conducción diaria a 10 h.</summary>
    public const int AmpliacionesPorSemana = 2;

    // ---------- Conducción semanal y bisemanal ----------

    public static readonly TimeSpan ConduccionSemanalMaxima = TimeSpan.FromHours(56);

    public static readonly TimeSpan ConduccionBisemanalMaxima = TimeSpan.FromHours(90);

    // ---------- Descanso diario ----------

    public static readonly TimeSpan DescansoDiarioNormal = TimeSpan.FromHours(11);

    public static readonly TimeSpan DescansoDiarioReducido = TimeSpan.FromHours(9);

    /// <summary>Primera parte del descanso diario dividido (3 h + 9 h).</summary>
    public static readonly TimeSpan DescansoDivididoPrimeraParte = TimeSpan.FromHours(3);

    /// <summary>Segunda parte del descanso diario dividido.</summary>
    public static readonly TimeSpan DescansoDivididoSegundaParte = TimeSpan.FromHours(9);

    /// <summary>Descansos reducidos permitidos entre dos descansos semanales.</summary>
    public const int DescansosReducidosMaximos = 3;

    /// <summary>Plazo para hacer el descanso diario desde el final del descanso anterior.</summary>
    public static readonly TimeSpan PlazoDescansoDiario = TimeSpan.FromHours(24);

    // ---------- Descanso semanal ----------

    public static readonly TimeSpan DescansoSemanalNormal = TimeSpan.FromHours(45);

    public static readonly TimeSpan DescansoSemanalReducido = TimeSpan.FromHours(24);

    /// <summary>El descanso semanal debe empezar, como tarde, 6 periodos de 24 h después del anterior.</summary>
    public static readonly TimeSpan PlazoDescansoSemanal = TimeSpan.FromHours(6 * 24);

    // ---------- Registro ----------

    /// <summary>
    /// Un salto del reloj del juego mayor que esto (por ejemplo, al dormir) se
    /// apunta como descanso.
    /// </summary>
    public static readonly TimeSpan SaltoConsideradoDescanso = TimeSpan.FromMinutes(10);

    /// <summary>Antigüedad máxima de los periodos que se conservan en el registro.</summary>
    public static readonly TimeSpan AntiguedadMaximaRegistro = TimeSpan.FromDays(8 * 7);

    /// <summary>Lunes a las 00:00 de la semana a la que pertenece una fecha.</summary>
    public static DateTime InicioSemana(DateTime fecha)
    {
        int diasDesdeLunes = ((int)fecha.DayOfWeek + 6) % 7;
        return fecha.Date.AddDays(-diasDesdeLunes);
    }
}
