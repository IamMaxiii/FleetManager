namespace FleetManager.Models;

/// <summary>
/// Preferencias de la aplicación. Se guarda en ajustes.json.
/// </summary>
public sealed class AjustesApp
{
    /// <summary>Posición del mini tacógrafo en pantalla; vacío hasta que se mueva por primera vez.</summary>
    public double? MiniTacografoIzquierda { get; set; }

    public double? MiniTacografoArriba { get; set; }

    /// <summary>El mini tacógrafo se muestra al abrir la aplicación.</summary>
    public bool MiniTacografoVisible { get; set; } = true;
}
