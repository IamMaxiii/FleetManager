using FleetManager.Core;
using FleetManager.Tests.Utilidades;
using static FleetManager.Tests.Utilidades.ConstructorRegistro;

namespace FleetManager.Tests.Core;

/// <summary>
/// Descanso diario: normal (11 h), dividido (3 h + 9 h), reducido (9 h, máx. 3) y plazo de 24 h.
/// </summary>
public class DescansoDiarioTests
{
    private static readonly DateTime Lunes = new(2026, 3, 2, 6, 0, 0);

    [Fact]
    public void Un_descanso_de_9h_es_reducido()
    {
        EstadoTacografo estado = new ConstructorRegistro(Lunes)
            .ConducirConPausas(H(8)).Descansar(H(9)).Conducir(H(1))
            .Analizar();

        Assert.Equal(2, estado.DescansosReducidosRestantes);
        Assert.Equal(H(1), estado.ConduccionDiaria);
        Assert.Empty(estado.Infracciones);
    }

    [Fact]
    public void El_cuarto_descanso_reducido_es_infraccion()
    {
        var registro = new ConstructorRegistro(Lunes);

        for (int i = 0; i < 4; i++)
        {
            registro.Conducir(H(4)).Descansar(H(9));
        }

        EstadoTacografo estado = registro.Conducir(H(1)).Analizar();

        Infraccion infraccion = Assert.Single(estado.Infracciones);
        Assert.Equal(TipoInfraccion.DemasiadosDescansosReducidos, infraccion.Tipo);
        Assert.Equal(Lunes + 4 * H(13) - H(9), infraccion.Momento); // inicio del cuarto descanso
        Assert.Equal(0, estado.DescansosReducidosRestantes);
    }

    [Fact]
    public void Un_descanso_dividido_3h_mas_9h_cuenta_como_normal()
    {
        EstadoTacografo estado = new ConstructorRegistro(Lunes)
            .Conducir(H(3)).Descansar(H(3))
            .Conducir(H(3)).Descansar(H(9))
            .Conducir(H(1))
            .Analizar();

        Assert.Equal(3, estado.DescansosReducidosRestantes);
        Assert.Equal(H(1), estado.ConduccionDiaria);
        Assert.Empty(estado.Infracciones);
    }

    [Fact]
    public void Con_la_primera_parte_de_3h_hecha_solo_faltan_9h()
    {
        EstadoTacografo estado = new ConstructorRegistro(Lunes)
            .Conducir(H(3)).Descansar(H(3)).Conducir(H(1))
            .Analizar();

        Assert.True(estado.PrimeraParteDescansoDivididoHecha);
        Assert.Equal(H(9), estado.DescansoDiarioRestante);
    }

    [Fact]
    public void Un_descanso_de_11h_que_no_cabe_en_las_24h_es_reducido()
    {
        // 14 h de jornada: el descanso empieza a las 14 h y solo caben 10 h antes del plazo.
        EstadoTacografo estado = new ConstructorRegistro(Lunes)
            .ConducirConPausas(H(9)).OtrosTrabajos(H(4.25))
            .Descansar(H(11)).Conducir(H(1))
            .Analizar();

        Assert.Equal(2, estado.DescansosReducidosRestantes);
        Assert.Empty(estado.Infracciones);
    }

    [Fact]
    public void Trabajar_despues_del_limite_para_empezar_el_descanso_es_infraccion()
    {
        // El descanso de 9 h tenía que empezar como tarde a las 15 h de jornada.
        EstadoTacografo estado = new ConstructorRegistro(Lunes)
            .ConducirConPausas(H(8.5)).OtrosTrabajos(H(7))
            .Analizar();

        Infraccion infraccion = Assert.Single(estado.Infracciones);
        Assert.Equal(TipoInfraccion.DescansoDiarioFueraDePlazo, infraccion.Tipo);
        Assert.Equal(Lunes + H(15), infraccion.Momento);
        Assert.Equal(Lunes + H(24), estado.PlazoDescansoDiario);
        Assert.Equal(Lunes + H(15), estado.LimiteInicioDescansoDiario);
    }

    [Fact]
    public void Sin_reducidos_disponibles_el_descanso_debe_empezar_antes_para_que_quepan_11h()
    {
        var registro = new ConstructorRegistro(Lunes);

        for (int i = 0; i < 3; i++)
        {
            registro.Conducir(H(4)).Descansar(H(9));
        }

        DateTime inicioJornada = registro.Hora;
        EstadoTacografo estado = registro.Conducir(H(1)).Analizar();

        Assert.Equal(inicioJornada + H(13), estado.LimiteInicioDescansoDiario);
    }

    [Fact]
    public void Durante_el_descanso_se_ve_lo_que_falta()
    {
        EstadoTacografo estado = new ConstructorRegistro(Lunes)
            .ConducirConPausas(H(8)).Descansar(H(5))
            .Analizar();

        Assert.Equal(H(5), estado.DescansoActual);
        Assert.Equal(H(6), estado.DescansoDiarioRestante);
        Assert.Equal(H(8), estado.ConduccionDiaria);
    }

    [Fact]
    public void Al_cumplir_11h_de_descanso_la_jornada_ya_se_ve_nueva()
    {
        EstadoTacografo estado = new ConstructorRegistro(Lunes)
            .ConducirConPausas(H(8)).Descansar(H(11))
            .Analizar();

        Assert.Equal(TimeSpan.Zero, estado.ConduccionDiaria);
        Assert.Equal(TimeSpan.Zero, estado.DescansoDiarioRestante);
        Assert.Equal(3, estado.DescansosReducidosRestantes);
    }

    [Fact]
    public void Un_descanso_en_curso_de_10h_aun_no_cuenta_como_reducido()
    {
        EstadoTacografo estado = new ConstructorRegistro(Lunes)
            .ConducirConPausas(H(8)).Descansar(H(10))
            .Analizar();

        Assert.Equal(3, estado.DescansosReducidosRestantes);
        Assert.Equal(H(1), estado.DescansoDiarioRestante);
    }
}
