namespace FleetManager.Models;

/// <summary>
/// Datos del conductor. Se guarda en perfil.json.
/// </summary>
public sealed class PerfilConductor
{
    public string Nombre { get; set; } = "";

    public string Apellidos { get; set; } = "";

    /// <summary>Texto libre, tal como lo escribe el usuario.</summary>
    public string FechaNacimiento { get; set; } = "";

    /// <summary>Número del permiso de conducir (formato ES-######).</summary>
    public string NumeroPermiso { get; set; } = "";

    public string NumeroAdr { get; set; } = "";

    /// <summary>Kilómetros acumulados en la tarjeta del conductor.</summary>
    public double KilometrosTarjeta { get; set; }
}
