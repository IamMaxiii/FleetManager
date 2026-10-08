namespace FleetManager.Tests.Utilidades;

/// <summary>
/// Reloj controlado por la prueba: el tiempo solo avanza cuando se llama a <see cref="Avanzar"/>.
/// Usa la zona horaria UTC para que las pruebas den lo mismo en cualquier PC.
/// </summary>
public sealed class RelojFalso : TimeProvider
{
    private DateTimeOffset ahora;

    public RelojFalso(DateTimeOffset inicio)
    {
        ahora = inicio;
    }

    public RelojFalso()
        : this(new DateTimeOffset(2026, 10, 7, 12, 0, 0, TimeSpan.Zero))
    {
    }

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    public override DateTimeOffset GetUtcNow() => ahora;

    public void Avanzar(TimeSpan cuanto) => ahora += cuanto;
}
