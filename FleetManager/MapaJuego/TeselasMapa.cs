using System.IO;

namespace FleetManager.MapaJuego;

/// <summary>
/// Datos del mapa del juego ya generado (se guardan en info.json junto a las teselas).
/// </summary>
/// <param name="Version">Formato de la carpeta del mapa (para regenerar si cambia).</param>
/// <param name="MinX">Esquina superior izquierda del mapa (coordenadas del juego).</param>
/// <param name="MinZ">Esquina superior izquierda del mapa (coordenadas del juego).</param>
/// <param name="Lado">Lado del cuadrado que contiene todo el mapa (unidades del juego).</param>
/// <param name="ZoomMinimo">Primer nivel de zoom generado.</param>
/// <param name="ZoomMaximo">Último nivel de zoom generado (el más detallado).</param>
/// <param name="TamanoTesela">Lado de cada tesela en píxeles.</param>
/// <param name="Huella">Versión del juego y DLC con los que se generó.</param>
public sealed record InfoMapaJuego(int Version, double MinX, double MinZ, double Lado, int ZoomMinimo, int ZoomMaximo, int TamanoTesela, string Huella);

/// <summary>Nombre de una ciudad y dónde está, para dibujarlo encima del mapa.</summary>
public sealed record CiudadMapa(string Nombre, double X, double Z);

/// <summary>Rectángulo en coordenadas del juego (X hacia el este, Z hacia el sur).</summary>
public readonly record struct ZonaMundo(double X, double Z, double Ancho, double Alto)
{
    public double Derecha => X + Ancho;

    public double Abajo => Z + Alto;
}

/// <summary>
/// Cuentas de las teselas, compartidas por FleetManager (que las muestra) y por el
/// generador del mapa (que las dibuja). El mapa es un cuadrado que en el zoom <c>z</c>
/// se parte en 2^z × 2^z teselas cuadradas; la tesela (x, y) empieza en
/// (MinX + x·u, MinZ + y·u), con u = Lado / 2^z unidades del juego por tesela.
/// </summary>
public static class TeselasMapa
{
    /// <summary>Unidades del juego que cubre el lado de una tesela en ese zoom.</summary>
    public static double UnidadesPorTesela(InfoMapaJuego info, int zoom) => info.Lado / (1 << zoom);

    /// <summary>Píxeles por unidad del juego en ese zoom.</summary>
    public static double Escala(InfoMapaJuego info, int zoom) => info.TamanoTesela / UnidadesPorTesela(info, zoom);

    /// <summary>
    /// Zoom de teselas que conviene para una escala de pantalla: el primero con al menos
    /// ese detalle (así no se ven borrosas); si se acerca más que el máximo, el máximo.
    /// </summary>
    public static int ZoomPara(InfoMapaJuego info, double escala)
    {
        for (int zoom = info.ZoomMinimo; zoom <= info.ZoomMaximo; zoom++)
        {
            if (Escala(info, zoom) >= escala * 0.9)
            {
                return zoom;
            }
        }

        return info.ZoomMaximo;
    }

    /// <summary>Tesela que contiene un punto del mundo (puede quedar fuera del mapa).</summary>
    public static (int X, int Y) TeselaDe(InfoMapaJuego info, int zoom, double x, double z)
    {
        double u = UnidadesPorTesela(info, zoom);
        return ((int)Math.Floor((x - info.MinX) / u), (int)Math.Floor((z - info.MinZ) / u));
    }

    /// <summary>Zona del mundo que cubre una tesela.</summary>
    public static ZonaMundo ZonaDe(InfoMapaJuego info, int zoom, int x, int y)
    {
        double u = UnidadesPorTesela(info, zoom);
        return new ZonaMundo(info.MinX + x * u, info.MinZ + y * u, u, u);
    }

    /// <summary>Teselas (dentro del mapa) que tocan una zona del mundo.</summary>
    public static IEnumerable<(int X, int Y)> TeselasEn(InfoMapaJuego info, int zoom, ZonaMundo zona)
    {
        int ultima = (1 << zoom) - 1;
        var (x0, y0) = TeselaDe(info, zoom, zona.X, zona.Z);
        var (x1, y1) = TeselaDe(info, zoom, zona.Derecha, zona.Abajo);

        for (int y = Math.Max(0, y0); y <= Math.Min(ultima, y1); y++)
        {
            for (int x = Math.Max(0, x0); x <= Math.Min(ultima, x1); x++)
            {
                yield return (x, y);
            }
        }
    }

    public static string RutaTesela(string carpeta, int zoom, int x, int y) =>
        Path.Combine(carpeta, zoom.ToString(), $"{x}_{y}.png");
}
