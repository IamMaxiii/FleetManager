using FleetManager.Core;
using FleetManager.Tests.Utilidades;

namespace FleetManager.Tests.Core;

/// <summary>
/// Exceso de velocidad: límite + 0,5 km/h; 10 s en rojo, 30 s peligro, 60 s falta (una por episodio).
/// </summary>
public class VigilanteVelocidadTests
{
    private static readonly TimeSpan Lectura = TimeSpan.FromMilliseconds(250);

    private readonly RelojFalso reloj = new();
    private readonly VigilanteVelocidad vigilante;

    public VigilanteVelocidadTests()
    {
        vigilante = new VigilanteVelocidad(reloj);
    }

    /// <summary>Lecturas cada 250 ms durante <paramref name="tiempo"/>; cuenta las faltas nuevas.</summary>
    private int Circular(TimeSpan tiempo, double velocidad, double limite)
    {
        int faltas = vigilante.Actualizar(velocidad, limite) ? 1 : 0;

        for (TimeSpan t = Lectura; t <= tiempo; t += Lectura)
        {
            reloj.Avanzar(Lectura);
            faltas += vigilante.Actualizar(velocidad, limite) ? 1 : 0;
        }

        return faltas;
    }

    [Fact]
    public void Dentro_de_la_tolerancia_es_correcta()
    {
        Circular(TimeSpan.FromMinutes(2), 80.5, 80);

        Assert.Equal(NivelVelocidad.Correcta, vigilante.Nivel);
    }

    [Fact]
    public void Sin_limite_conocido_es_correcta()
    {
        Circular(TimeSpan.FromMinutes(2), 130, 0);

        Assert.Equal(NivelVelocidad.Correcta, vigilante.Nivel);
    }

    [Theory]
    [InlineData(5, NivelVelocidad.Exceso)]
    [InlineData(10, NivelVelocidad.ExcesoProlongado)]
    [InlineData(30, NivelVelocidad.Peligro)]
    [InlineData(60, NivelVelocidad.Falta)]
    public void El_nivel_sube_con_el_tiempo(int segundos, NivelVelocidad esperado)
    {
        Circular(TimeSpan.FromSeconds(segundos), 90, 80);

        Assert.Equal(esperado, vigilante.Nivel);
    }

    [Fact]
    public void Se_anota_una_sola_falta_por_episodio()
    {
        Assert.Equal(1, Circular(TimeSpan.FromMinutes(3), 95, 80));
    }

    [Fact]
    public void Bajar_del_limite_termina_el_episodio()
    {
        Circular(TimeSpan.FromSeconds(70), 95, 80);
        Circular(TimeSpan.FromSeconds(5), 70, 80);

        Assert.Equal(NivelVelocidad.Correcta, vigilante.Nivel);
        Assert.Equal(1, Circular(TimeSpan.FromSeconds(70), 95, 80));
    }

    [Fact]
    public void Una_pausa_del_juego_no_cuenta_como_tiempo_de_exceso()
    {
        vigilante.Actualizar(95, 80);
        reloj.Avanzar(TimeSpan.FromMinutes(5)); // juego en pausa: no hay lecturas
        vigilante.Actualizar(95, 80);

        Assert.Equal(NivelVelocidad.Exceso, vigilante.Nivel);
    }
}
