namespace FleetManager.Core;

/// <summary>
/// Números de documento inventados para el perfil del conductor. Se generan una
/// sola vez, la primera vez que se usa el perfil.
/// </summary>
public static class GeneradorDocumentos
{
    /// <summary>Permiso de conducir: "ES-" y 6 cifras.</summary>
    public static string NumeroPermiso(Random azar) => $"ES-{azar.Next(0, 1_000_000):000000}";

    /// <summary>Certificado ADR (mercancías peligrosas): "ADR-" y 6 cifras.</summary>
    public static string NumeroAdr(Random azar) => $"ADR-{azar.Next(0, 1_000_000):000000}";
}
