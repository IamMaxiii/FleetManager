using System.Windows;
using System.Windows.Media;
using FleetManager.Models;

namespace FleetManager.ViewModels;

/// <summary>
/// Qué parte del mundo del juego se ve en el mapa: el punto del centro y la escala
/// (píxeles por metro del juego). En pantalla, X del juego va hacia la derecha y Z
/// hacia abajo (el norte del juego, Z negativa, queda arriba).
/// </summary>
public sealed class VistaMapa
{
    public const double EscalaMinima = 0.0005;
    public const double EscalaMaxima = 2;

    /// <summary>Al abrir: 1 km = 50 píxeles.</summary>
    public const double EscalaInicial = 0.05;

    public Point Centro { get; set; }

    public double Escala { get; private set; } = EscalaInicial;

    public Point APantalla(Point mundo, Size tamano) =>
        new((mundo.X - Centro.X) * Escala + tamano.Width / 2, (mundo.Y - Centro.Y) * Escala + tamano.Height / 2);

    public Point AMundo(Point pantalla, Size tamano) =>
        new((pantalla.X - tamano.Width / 2) / Escala + Centro.X, (pantalla.Y - tamano.Height / 2) / Escala + Centro.Y);

    /// <summary>Transformación de coordenadas del juego a píxeles, para dibujar el recorrido.</summary>
    public Matrix Transformacion(Size tamano) =>
        new(Escala, 0, 0, Escala, tamano.Width / 2 - Centro.X * Escala, tamano.Height / 2 - Centro.Y * Escala);

    /// <summary>Acerca (factor &gt; 1) o aleja (factor &lt; 1) dejando quieto el punto que hay bajo <paramref name="pantalla"/>.</summary>
    public void Acercar(double factor, Point pantalla, Size tamano)
    {
        Point fijo = AMundo(pantalla, tamano);
        Escala = Math.Clamp(Escala * factor, EscalaMinima, EscalaMaxima);
        Centro = new Point(
            fijo.X - (pantalla.X - tamano.Width / 2) / Escala,
            fijo.Y - (pantalla.Y - tamano.Height / 2) / Escala);
    }

    /// <summary>Mueve el mapa lo que se ha arrastrado el ratón (en píxeles).</summary>
    public void Desplazar(Vector arrastre) =>
        Centro = new Point(Centro.X - arrastre.X / Escala, Centro.Y - arrastre.Y / Escala);

    /// <summary>Centra y ajusta la escala para que quepa toda la zona indicada.</summary>
    public void Encuadrar(Rect zona, Size tamano, double margen = 0.85)
    {
        if (zona.IsEmpty || tamano.Width <= 0 || tamano.Height <= 0)
        {
            return;
        }

        Centro = new Point(zona.X + zona.Width / 2, zona.Y + zona.Height / 2);

        if (zona.Width > 0 || zona.Height > 0)
        {
            double escala = Math.Min(
                zona.Width > 0 ? tamano.Width / zona.Width : double.MaxValue,
                zona.Height > 0 ? tamano.Height / zona.Height : double.MaxValue);
            Escala = Math.Clamp(escala * margen, EscalaMinima, EscalaMaxima);
        }
    }

    /// <summary>Rectángulo que contiene todo el recorrido; vacío si no hay nada.</summary>
    public static Rect Limites(MapaRecorrido mapa)
    {
        Rect limites = Rect.Empty;

        foreach (List<int[]> tramo in mapa.Tramos)
        {
            foreach (int[] punto in tramo)
            {
                limites.Union(new Point(punto[0], punto[1]));
            }
        }

        return limites;
    }
}
