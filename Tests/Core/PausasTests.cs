using FleetManager.Core;
using FleetManager.Tests.Utilidades;
using static FleetManager.Tests.Utilidades.ConstructorRegistro;

namespace FleetManager.Tests.Core;

/// <summary>
/// Conducción continua (4 h 30) y pausa (45 min o 15 + 30).
/// </summary>
public class PausasTests
{
    private static readonly DateTime Lunes = new(2026, 3, 2, 6, 0, 0);

    private static ConstructorRegistro Registro() => new(Lunes);

    [Fact]
    public void Sin_registro_todos_los_limites_estan_completos()
    {
        EstadoTacografo estado = AnalizadorTacografo.Analizar(new());

        Assert.Equal(H(4.5), estado.ConduccionContinuaRestante);
        Assert.Equal(M(45), estado.PausaRestante);
        Assert.Equal(H(10), estado.ConduccionDiariaRestante);
        Assert.Equal(H(56), estado.ConduccionSemanalRestante);
        Assert.Equal(H(90), estado.ConduccionBisemanalRestante);
        Assert.Empty(estado.Infracciones);
    }

    [Fact]
    public void Tras_4h_de_conduccion_quedan_30_min()
    {
        EstadoTacografo estado = Registro().Conducir(H(4)).Analizar();

        Assert.Equal(H(4), estado.ConduccionContinua);
        Assert.Equal(M(30), estado.ConduccionContinuaRestante);
        Assert.Equal(M(30), estado.SiguienteParada);
        Assert.Empty(estado.Infracciones);
    }

    [Fact]
    public void Pasar_de_4h30_sin_pausa_es_infraccion_en_el_momento_exacto()
    {
        EstadoTacografo estado = Registro().Conducir(H(4.5) + M(1)).Analizar();

        Infraccion infraccion = Assert.Single(estado.Infracciones);
        Assert.Equal(TipoInfraccion.ConduccionContinua, infraccion.Tipo);
        Assert.Equal(Lunes + H(4.5), infraccion.Momento);
    }

    [Fact]
    public void Justo_4h30_no_es_infraccion()
    {
        EstadoTacografo estado = Registro().Conducir(H(4.5)).Analizar();

        Assert.Empty(estado.Infracciones);
        Assert.Equal(TimeSpan.Zero, estado.ConduccionContinuaRestante);
    }

    [Fact]
    public void Una_pausa_de_45_min_reinicia_la_conduccion_continua()
    {
        EstadoTacografo estado = Registro().Conducir(H(4.5)).Descansar(M(45)).Conducir(H(1)).Analizar();

        Assert.Equal(H(1), estado.ConduccionContinua);
        Assert.Empty(estado.Infracciones);
    }

    [Fact]
    public void Una_pausa_dividida_15_mas_30_reinicia_la_conduccion_continua()
    {
        EstadoTacografo estado = Registro()
            .Conducir(H(2)).Descansar(M(15))
            .Conducir(H(2)).Descansar(M(30))
            .Conducir(H(1))
            .Analizar();

        Assert.Equal(H(1), estado.ConduccionContinua);
        Assert.Empty(estado.Infracciones);
    }

    [Fact]
    public void Una_pausa_30_mas_15_no_vale_porque_el_orden_es_15_y_luego_30()
    {
        EstadoTacografo estado = Registro()
            .Conducir(H(2)).Descansar(M(30))
            .Conducir(H(2)).Descansar(M(15))
            .Conducir(H(1))
            .Analizar();

        Assert.Equal(H(5), estado.ConduccionContinua);
        Assert.Equal(TipoInfraccion.ConduccionContinua, Assert.Single(estado.Infracciones).Tipo);
    }

    [Fact]
    public void Una_pausa_de_menos_de_15_min_no_cuenta()
    {
        EstadoTacografo estado = Registro()
            .Conducir(H(2)).Descansar(M(14))
            .Conducir(H(2)).Descansar(M(30))
            .Conducir(H(1))
            .Analizar();

        Assert.Equal(H(5), estado.ConduccionContinua);
    }

    [Fact]
    public void Otros_trabajos_no_cuentan_como_pausa()
    {
        EstadoTacografo estado = Registro().Conducir(H(4)).OtrosTrabajos(M(45)).Conducir(H(1)).Analizar();

        Assert.Equal(H(5), estado.ConduccionContinua);
        Assert.Equal(TipoInfraccion.ConduccionContinua, Assert.Single(estado.Infracciones).Tipo);
    }

    [Fact]
    public void La_disponibilidad_no_cuenta_como_pausa()
    {
        EstadoTacografo estado = Registro().Conducir(H(4)).Disponibilidad(M(45)).Conducir(H(1)).Analizar();

        Assert.Equal(H(5), estado.ConduccionContinua);
        Assert.Equal(TipoInfraccion.ConduccionContinua, Assert.Single(estado.Infracciones).Tipo);
    }

    [Fact]
    public void Durante_la_pausa_se_ve_lo_que_falta()
    {
        EstadoTacografo estado = Registro().Conducir(H(4)).Descansar(M(20)).Analizar();

        Assert.Equal(M(25), estado.PausaRestante);
        Assert.Equal(M(20), estado.DescansoActual);
    }

    [Fact]
    public void Con_la_primera_parte_hecha_faltan_30_min()
    {
        EstadoTacografo conduciendo = Registro().Conducir(H(2)).Descansar(M(15)).Conducir(H(1)).Analizar();
        EstadoTacografo descansando = Registro().Conducir(H(2)).Descansar(M(15)).Conducir(H(1)).Descansar(M(10)).Analizar();

        Assert.True(conduciendo.PrimeraPartePausaHecha);
        Assert.Equal(M(30), conduciendo.PausaRestante);
        Assert.Equal(M(20), descansando.PausaRestante);
    }

    [Fact]
    public void La_siguiente_parada_es_el_limite_que_llegue_antes()
    {
        // Conducción diaria 8 h 30 (quedan 1 h 30 con ampliación), continua 4 h (quedan 30 min).
        EstadoTacografo estado = Registro().Conducir(H(4.5)).Descansar(M(45)).Conducir(H(4)).Analizar();

        Assert.Equal(M(30), estado.SiguienteParada);
    }
}
