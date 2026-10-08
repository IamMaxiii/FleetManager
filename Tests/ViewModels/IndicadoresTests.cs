using FleetManager.ViewModels;
using static FleetManager.Tests.Utilidades.ConstructorRegistro;

namespace FleetManager.Tests.ViewModels;

/// <summary>
/// Barras de tiempo y de porcentaje: cuánto se llenan y de qué color se pintan.
/// </summary>
public class IndicadoresTests
{
    [Fact]
    public void Una_barra_de_limite_muestra_lo_usado_el_limite_y_lo_que_queda()
    {
        var barra = new IndicadorTiempo("Conducción continua");

        barra.ActualizarLimite(H(2), H(4.5), H(2.5));

        Assert.Equal("02:00", barra.Usado);
        Assert.Equal("04:30", barra.Limite);
        Assert.Equal("quedan 02:30", barra.Restante);
        Assert.Equal(2 / 4.5, barra.Fraccion, 3);
        Assert.Equal(NivelIndicador.Normal, barra.Nivel);
    }

    [Fact]
    public void Con_poco_margen_la_barra_pasa_a_atencion()
    {
        var barra = new IndicadorTiempo("Conducción continua");

        barra.ActualizarLimite(H(4), H(4.5), M(30)); // 89 % gastado

        Assert.Equal(NivelIndicador.Atencion, barra.Nivel);
    }

    [Fact]
    public void Sin_margen_la_barra_esta_agotada_y_no_se_sale()
    {
        var barra = new IndicadorTiempo("Conducción diaria");

        barra.ActualizarLimite(H(11), H(10), TimeSpan.Zero);

        Assert.Equal(NivelIndicador.Agotado, barra.Nivel);
        Assert.Equal(1, barra.Fraccion);
    }

    [Fact]
    public void Una_barra_de_objetivo_dice_lo_que_falta_o_que_esta_cumplido()
    {
        var barra = new IndicadorTiempo("Descanso diario");

        barra.ActualizarObjetivo(H(3), H(11), H(8));
        Assert.Equal("faltan 08:00", barra.Restante);
        Assert.Equal(NivelIndicador.Objetivo, barra.Nivel);

        barra.ActualizarObjetivo(H(11), H(11), TimeSpan.Zero);
        Assert.Equal("cumplido", barra.Restante);
        Assert.Equal(1, barra.Fraccion);
    }

    [Fact]
    public void Solo_se_avisa_a_la_pantalla_si_algo_cambia()
    {
        var barra = new IndicadorTiempo("Conducción continua");
        barra.ActualizarLimite(H(1), H(4.5), H(3.5));
        int avisos = 0;
        barra.PropertyChanged += (_, _) => avisos++;

        barra.ActualizarLimite(H(1), H(4.5), H(3.5));
        Assert.Equal(0, avisos);

        barra.ActualizarLimite(H(1) + M(1), H(4.5), H(3.5) - M(1));
        Assert.True(avisos > 0);
    }

    [Theory]
    [InlineData(400, NivelIndicador.Normal)]
    [InlineData(150, NivelIndicador.Atencion)]
    [InlineData(50, NivelIndicador.Agotado)]
    public void Un_deposito_avisa_cuando_queda_poco(double litros, NivelIndicador esperado)
    {
        var combustible = new IndicadorPorcentaje("Combustible");

        combustible.ActualizarDeposito(litros, 800, "L");

        Assert.Equal(esperado, combustible.Nivel);
    }

    [Fact]
    public void Un_deposito_muestra_cantidad_capacidad_y_porcentaje()
    {
        var combustible = new IndicadorPorcentaje("Combustible");

        combustible.ActualizarDeposito(320, 800, "L");

        Assert.Equal("320 / 800 L (40 %)", combustible.Texto);
        Assert.Equal(0.4, combustible.Fraccion, 3);
    }

    [Theory]
    [InlineData(0.05, NivelIndicador.Normal, "5 %")]
    [InlineData(0.12, NivelIndicador.Atencion, "12 %")]
    [InlineData(0.30, NivelIndicador.Agotado, "30 %")]
    public void Un_desgaste_alto_se_marca(double desgaste, NivelIndicador esperado, string texto)
    {
        var motor = new IndicadorPorcentaje("Motor");

        motor.ActualizarDesgaste(desgaste);

        Assert.Equal(esperado, motor.Nivel);
        Assert.Equal(texto, motor.Texto);
    }
}
