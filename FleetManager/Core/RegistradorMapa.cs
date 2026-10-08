using FleetManager.Models;

namespace FleetManager.Core;

/// <summary>
/// Apunta en el <see cref="MapaRecorrido"/> las posiciones por las que pasa el camión,
/// guardando solo los puntos necesarios para dibujar bien el recorrido:
///
/// - En recta, un punto cada <see cref="DistanciaMaxima"/>.
/// - En curva, un punto en cuanto la dirección gira <see cref="GiroMinimoGrados"/> o más
///   (con al menos <see cref="DistanciaMinima"/> desde el anterior).
/// - Si la posición salta más de <see cref="SaltoNuevoTramo"/>, empieza un tramo nuevo.
/// </summary>
public sealed class RegistradorMapa
{
    public const double DistanciaMinima = 30;
    public const double DistanciaMaxima = 500;
    public const double GiroMinimoGrados = 8;
    public const double SaltoNuevoTramo = 2000;

    private readonly MapaRecorrido mapa;

    public RegistradorMapa(MapaRecorrido mapa)
    {
        this.mapa = mapa;
    }

    /// <param name="minuto">Minutos de juego desde el inicio de la jornada.</param>
    /// <returns>Verdadero si se ha añadido algo al mapa.</returns>
    public bool Registrar(double x, double z, int minuto = 0)
    {
        int[] punto = [(int)Math.Round(x), (int)Math.Round(z), minuto];

        if (mapa.Tramos.Count == 0 || mapa.Tramos[^1].Count == 0)
        {
            mapa.Tramos.Add([punto]);
            return true;
        }

        List<int[]> tramo = mapa.Tramos[^1];
        int[] ultimo = tramo[^1];
        double distancia = Distancia(ultimo, punto);

        if (distancia > SaltoNuevoTramo)
        {
            mapa.Tramos.Add([punto]);
            return true;
        }

        if (distancia < DistanciaMinima)
        {
            return false;
        }

        bool tocaPorDistancia = distancia >= DistanciaMaxima;
        bool tocaPorGiro = tramo.Count >= 2 && Giro(tramo[^2], ultimo, punto) >= GiroMinimoGrados;

        if (!tocaPorDistancia && !tocaPorGiro && tramo.Count >= 2)
        {
            return false;
        }

        tramo.Add(punto);
        return true;
    }

    private static double Distancia(int[] a, int[] b) => Math.Sqrt(Math.Pow(b[0] - a[0], 2) + Math.Pow(b[1] - a[1], 2));

    /// <summary>Ángulo (grados) entre la dirección a→b y la dirección b→c.</summary>
    private static double Giro(int[] a, int[] b, int[] c)
    {
        double angulo1 = Math.Atan2(b[1] - a[1], b[0] - a[0]);
        double angulo2 = Math.Atan2(c[1] - b[1], c[0] - b[0]);
        double diferencia = Math.Abs(angulo2 - angulo1) * 180 / Math.PI;
        return diferencia > 180 ? 360 - diferencia : diferencia;
    }
}
