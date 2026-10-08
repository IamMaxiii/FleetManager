using FleetManager.Core;
using FleetManager.Models;

namespace FleetManager.Tests.Core;

/// <summary>
/// Puntos del recorrido para el mapa: pocos en recta, más en curva, tramo nuevo si la posición salta.
/// </summary>
public class RegistradorMapaTests
{
    private readonly MapaRecorrido mapa = new();
    private readonly RegistradorMapa registrador;

    public RegistradorMapaTests()
    {
        registrador = new RegistradorMapa(mapa);
    }

    private List<int[]> Tramo => Assert.Single(mapa.Tramos);

    [Fact]
    public void La_primera_posicion_empieza_un_tramo()
    {
        Assert.True(registrador.Registrar(100.4, -200.6, minuto: 7));

        Assert.Equal([100, -201, 7], Tramo[0]); // X, Z y minuto de la jornada
    }

    [Fact]
    public void Moverse_muy_poco_no_añade_puntos()
    {
        registrador.Registrar(0, 0);

        Assert.False(registrador.Registrar(10, 0));
        Assert.Single(Tramo);
    }

    [Fact]
    public void En_recta_solo_guarda_un_punto_cada_500_m()
    {
        for (int x = 0; x <= 5000; x += 10)
        {
            registrador.Registrar(x, 0);
        }

        // El primero, el segundo (a 30 m, para tener una dirección) y luego uno cada 500 m.
        Assert.InRange(Tramo.Count, 11, 13);
    }

    [Fact]
    public void En_una_curva_guarda_mas_puntos()
    {
        // Un cuarto de circunferencia de 300 m de radio, en pasos de 10 m.
        for (double angulo = 0; angulo <= Math.PI / 2; angulo += 10.0 / 300)
        {
            registrador.Registrar(300 * Math.Sin(angulo), 300 - 300 * Math.Cos(angulo));
        }

        Assert.True(Tramo.Count >= 6, $"Solo {Tramo.Count} puntos en la curva");
    }

    [Fact]
    public void Un_salto_de_posicion_empieza_un_tramo_nuevo()
    {
        registrador.Registrar(0, 0);
        registrador.Registrar(100, 0);

        Assert.True(registrador.Registrar(50_000, 50_000)); // ferri, otra partida...

        Assert.Equal(2, mapa.Tramos.Count);
        Assert.Equal([50_000, 50_000, 0], mapa.Tramos[1][0]);
    }
}
