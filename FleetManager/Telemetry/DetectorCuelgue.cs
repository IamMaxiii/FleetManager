namespace FleetManager.Telemetry;

/// <summary>
/// Detecta un juego colgado o cerrado de golpe: el plugin solo avisa de que el
/// juego se ha cerrado si se cierra bien, así que también se vigila que su reloj
/// interno (Timestamp) avance. Si no avanza durante <see cref="Espera"/> sin estar
/// en pausa, se considera que el juego no responde.
/// </summary>
public sealed class DetectorCuelgue
{
    public static readonly TimeSpan EsperaPorDefecto = TimeSpan.FromSeconds(5);

    private readonly TimeProvider reloj;
    private ulong ultimaMarca;
    private DateTimeOffset ultimoCambio;
    private bool hayLectura;

    public DetectorCuelgue(TimeProvider reloj, TimeSpan espera)
    {
        this.reloj = reloj;
        Espera = espera;
    }

    public TimeSpan Espera { get; }

    /// <param name="marcaTiempo">Reloj interno del juego (no avanza en pausa).</param>
    /// <param name="pausado">El juego está en pausa o en un menú.</param>
    /// <returns>Verdadero si el juego parece colgado.</returns>
    public bool EstaColgado(ulong marcaTiempo, bool pausado)
    {
        DateTimeOffset ahora = reloj.GetUtcNow();

        if (!hayLectura || marcaTiempo != ultimaMarca || pausado)
        {
            hayLectura = true;
            ultimaMarca = marcaTiempo;
            ultimoCambio = ahora;
            return false;
        }

        return ahora - ultimoCambio >= Espera;
    }

    /// <summary>Olvida lo visto (por ejemplo, cuando el juego se cierra bien).</summary>
    public void Reiniciar() => hayLectura = false;
}
