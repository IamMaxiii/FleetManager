using System.Windows;

namespace FleetManager.ViewModels;

/// <summary>
/// Coloca una ventana dentro de la zona visible de las pantallas.
/// </summary>
public static class PosicionVentana
{
    /// <summary>
    /// Devuelve la posición más cercana a la pedida en la que la ventana cabe entera
    /// dentro de <paramref name="zonaVisible"/> (por ejemplo, si se guardó en un
    /// monitor que ya no está conectado). Sin posición guardada, usa la esquina
    /// superior derecha con un margen.
    /// </summary>
    public static Point Ajustar(double? izquierda, double? arriba, Size tamano, Rect zonaVisible, double margen = 16)
    {
        double x = izquierda ?? zonaVisible.Right - tamano.Width - margen;
        double y = arriba ?? zonaVisible.Top + margen;

        x = Math.Clamp(x, zonaVisible.Left, Math.Max(zonaVisible.Left, zonaVisible.Right - tamano.Width));
        y = Math.Clamp(y, zonaVisible.Top, Math.Max(zonaVisible.Top, zonaVisible.Bottom - tamano.Height));

        return new Point(x, y);
    }
}
