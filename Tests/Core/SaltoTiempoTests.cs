using FleetManager.Core;

namespace FleetManager.Tests.Core;

/// <summary>
/// Cuentas del salto de tiempo y el comando g_set_time.
/// </summary>
public class SaltoTiempoTests
{
    private static readonly DateTime Ahora = new(2026, 3, 2, 23, 40, 30);

    [Fact]
    public void Saltar_9_horas_lleva_a_la_misma_hora_mas_9_sin_segundos()
    {
        Assert.Equal(new DateTime(2026, 3, 3, 8, 40, 0), SaltoTiempo.Objetivo(Ahora, TimeSpan.FromHours(9)));
    }

    [Fact]
    public void El_comando_lleva_hora_y_minutos()
    {
        Assert.Equal("g_set_time 8 40", SaltoTiempo.Comando(new DateTime(2026, 3, 3, 8, 40, 0)));
        Assert.Equal("g_set_time 0 5", SaltoTiempo.Comando(new DateTime(2026, 3, 3, 0, 5, 0)));
    }

    [Fact]
    public void Saltar_hasta_una_hora_que_ya_ha_pasado_hoy_es_manana()
    {
        Assert.Equal(new DateTime(2026, 3, 3, 6, 0, 0), SaltoTiempo.ObjetivoHasta(Ahora, new TimeSpan(6, 0, 0)));
    }

    [Fact]
    public void Saltar_hasta_una_hora_que_aun_no_ha_llegado_es_hoy()
    {
        var mañana = new DateTime(2026, 3, 2, 5, 0, 0);

        Assert.Equal(new DateTime(2026, 3, 2, 22, 0, 0), SaltoTiempo.ObjetivoHasta(mañana, new TimeSpan(22, 0, 0)));
    }

    [Fact]
    public void Saltar_hasta_la_hora_actual_es_dentro_de_24_horas()
    {
        var enPunto = new DateTime(2026, 3, 2, 6, 0, 0);

        Assert.Equal(enPunto.AddDays(1), SaltoTiempo.ObjetivoHasta(enPunto, new TimeSpan(6, 0, 0)));
    }

    [Fact]
    public void No_se_puede_saltar_24_horas_o_mas()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SaltoTiempo.Objetivo(Ahora, TimeSpan.FromHours(24)));
    }
}
