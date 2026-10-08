using FleetManager.Core;

namespace FleetManager.Tests.Core;

/// <summary>
/// Fechas del juego como "Día N (día de la semana)".
/// </summary>
public class FechaJuegoTests
{
    [Fact]
    public void El_primer_dia_del_juego_es_el_dia_1_lunes()
    {
        Assert.Equal(1, FechaJuego.NumeroDia(DateTime.MinValue));
        Assert.Equal("Día 1 (lun)", FechaJuego.TextoDia(DateTime.MinValue));
    }

    [Fact]
    public void Los_dias_se_cuentan_desde_el_principio()
    {
        DateTime fecha = DateTime.MinValue.AddDays(178).AddHours(14.5); // día 179: 25 semanas y 3 días después de un lunes

        Assert.Equal(179, FechaJuego.NumeroDia(fecha));
        Assert.Equal("Día 179 (jue) 14:30", FechaJuego.TextoDiaYHora(fecha));
    }

    [Fact]
    public void Componer_y_leer_dan_la_misma_fecha()
    {
        DateTime fecha = FechaJuego.Componer(176, new TimeSpan(8, 15, 0));

        Assert.Equal(176, FechaJuego.NumeroDia(fecha));
        Assert.Equal("08:15", FechaJuego.TextoHora(fecha));
    }
}
