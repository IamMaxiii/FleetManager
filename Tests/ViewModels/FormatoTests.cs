using FleetManager.Models;
using FleetManager.ViewModels;

namespace FleetManager.Tests.ViewModels;

/// <summary>
/// Cómo se escriben en pantalla horas, duraciones, distancias y porcentajes.
/// </summary>
public class FormatoTests
{
    [Theory]
    [InlineData(330, "05:30")]
    [InlineData(56 * 60, "56:00")]
    [InlineData(0, "00:00")]
    [InlineData(-15, "00:00")]
    public void Las_duraciones_se_escriben_en_horas_y_minutos(int minutos, string esperado)
    {
        Assert.Equal(esperado, Formato.Duracion(TimeSpan.FromMinutes(minutos)));
    }

    [Fact]
    public void La_hora_del_juego_lleva_el_dia_de_la_semana()
    {
        Assert.Equal("mié 14:32", Formato.HoraJuego(new DateTime(2026, 10, 7, 14, 32, 0)));
        Assert.Equal("lun 00:00", Formato.HoraJuego(DateTime.MinValue)); // la hora del SDK empieza el lunes 1/1/0001
    }

    [Fact]
    public void Los_numeros_usan_el_formato_espanol()
    {
        Assert.Equal("12.345,6 km", Formato.Kilometros(12345.64));
        Assert.Equal("83 km/h", Formato.Velocidad(83.4));
        Assert.Equal("40 %", Formato.Porcentaje(0.4));
        Assert.Equal("18,5 t", Formato.Toneladas(18_500));
    }

    [Fact]
    public void Las_actividades_tienen_su_nombre_en_espanol()
    {
        Assert.Equal("Conducción", Formato.Actividad(Actividad.Conduccion));
        Assert.Equal("Otros trabajos", Formato.Actividad(Actividad.OtrosTrabajos));
        Assert.Equal("Disponibilidad", Formato.Actividad(Actividad.Disponibilidad));
        Assert.Equal("Descanso", Formato.Actividad(Actividad.Descanso));
    }
}
