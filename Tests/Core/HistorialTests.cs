using FleetManager.Core;
using FleetManager.Models;
using FleetManager.Tests.Utilidades;

namespace FleetManager.Tests.Core;

/// <summary>
/// Agrupación del historial por días, resúmenes y comprobación de ediciones.
/// </summary>
public class HistorialTests
{
    [Fact]
    public void Los_trayectos_se_agrupan_por_el_dia_en_que_empiezan()
    {
        Jornada jornada = EjemplosHistorial.DosJornadas().Jornadas[0];

        IReadOnlyList<DiaHistorial> dias = HistorialConsultas.Dias(jornada);

        Assert.Equal(2, dias.Count);
        Assert.Equal(176, FechaJuego.NumeroDia(dias[0].Fecha));
        Assert.Equal(2, dias[0].Trayectos.Count);
        Assert.Equal(608.4, dias[0].Kilometros, 3);
        Assert.Equal(1, dias[0].Faltas);
        Assert.Equal(177, FechaJuego.NumeroDia(dias[1].Fecha));
        Assert.Single(dias[1].Trayectos);
    }

    [Fact]
    public void El_resumen_suma_trayectos_kilometros_conduccion_y_faltas()
    {
        Jornada jornada = EjemplosHistorial.DosJornadas().Jornadas[0];

        ResumenTrayectos resumen = HistorialConsultas.Resumir(jornada.Trayectos);

        Assert.Equal(3, resumen.Trayectos);
        Assert.Equal(707.9, resumen.Kilometros, 3);
        Assert.Equal(TimeSpan.FromHours(7), resumen.TiempoConduccion);
        Assert.Equal(1, resumen.Faltas);
    }

    [Fact]
    public void Un_trayecto_correcto_no_tiene_problemas()
    {
        Assert.Empty(ValidadorHistorial.ValidarTrayecto(EjemplosHistorial.Trayecto(176, 7, 3, "A", "B", 200)));
    }

    [Fact]
    public void Un_trayecto_que_acaba_antes_de_empezar_no_vale()
    {
        Trayecto trayecto = EjemplosHistorial.Trayecto(176, 7, 3, "A", "B", 200);
        trayecto.Fin = trayecto.Inicio.AddHours(-1);

        Assert.Contains("La hora de fin no puede ser anterior a la de inicio.", ValidadorHistorial.ValidarTrayecto(trayecto));
    }

    [Fact]
    public void Kilometros_negativos_o_media_mayor_que_maxima_no_valen()
    {
        Trayecto trayecto = EjemplosHistorial.Trayecto(176, 7, 3, "A", "B", 200);
        trayecto.Kilometros = -5;
        trayecto.VelocidadMedia = 95;

        IReadOnlyList<string> errores = ValidadorHistorial.ValidarTrayecto(trayecto);

        Assert.Contains("Los kilómetros no pueden ser negativos.", errores);
        Assert.Contains("La velocidad media no puede ser mayor que la máxima.", errores);
    }

    [Fact]
    public void Una_jornada_no_puede_dejar_trayectos_fuera_de_su_horario()
    {
        Jornada jornada = EjemplosHistorial.DosJornadas().Jornadas[0];
        jornada.Fin = FechaJuego.Componer(176, TimeSpan.FromHours(12)); // antes del último trayecto

        Assert.Contains("Hay trayectos fuera del horario de la jornada.", ValidadorHistorial.ValidarJornada(jornada));
    }
}
