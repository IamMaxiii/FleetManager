using FleetManager.Core;
using FleetManager.Models;
using FleetManager.Tests.Utilidades;
using FleetManager.ViewModels;

namespace FleetManager.Tests.ViewModels;

/// <summary>
/// Paneles de edición del historial: lectura de los campos, comprobación y guardado.
/// </summary>
public class EdicionHistorialTests
{
    [Theory]
    [InlineData("12,5", 12.5)]
    [InlineData("12.5", 12.5)]
    [InlineData("1.234,5", 1234.5)]
    [InlineData("125.000", 125000)]
    [InlineData(" 300 ", 300)]
    public void Los_numeros_se_leen_con_coma_o_con_punto(string texto, double esperado)
    {
        Assert.True(LecturaCampos.Decimal(texto, out double valor));
        Assert.Equal(esperado, valor, 6);
    }

    [Theory]
    [InlineData("08:15", true)]
    [InlineData("8:15", true)]
    [InlineData("23:59", true)]
    [InlineData("24:00", false)]
    [InlineData("8.15", false)]
    public void Las_horas_se_leen_como_HH_mm(string texto, bool valida)
    {
        Assert.Equal(valida, LecturaCampos.Hora(texto, out _));
    }

    [Fact]
    public void Editar_un_trayecto_cambia_el_original_y_avisa()
    {
        Historial historial = EjemplosHistorial.DosJornadas();
        Jornada jornada = historial.Jornadas[0];
        Trayecto trayecto = jornada.Trayectos[0];
        bool guardado = false;
        var edicion = new EdicionTrayectoViewModel(jornada, trayecto, () => guardado = true);

        edicion.Destino = "Huesca";
        edicion.Kilometros = "250,5";
        edicion.HoraFin = "10:30";
        edicion.FaltaConduccion = true;

        Assert.True(edicion.Guardar());
        Assert.True(guardado);
        Assert.Equal("Huesca", trayecto.Destino);
        Assert.Equal(250.5, trayecto.Kilometros);
        Assert.Equal(FechaJuego.Componer(176, new TimeSpan(10, 30, 0)), trayecto.Fin);
        Assert.True(trayecto.FaltaConduccion);
        Assert.Equal("", edicion.Errores);
    }

    [Fact]
    public void Con_datos_incorrectos_no_se_cambia_nada_y_se_explica_por_que()
    {
        Historial historial = EjemplosHistorial.DosJornadas();
        Jornada jornada = historial.Jornadas[0];
        Trayecto trayecto = jornada.Trayectos[0];
        DateTime finOriginal = trayecto.Fin;
        var edicion = new EdicionTrayectoViewModel(jornada, trayecto, () => { });

        edicion.HoraFin = "06:00"; // antes del inicio (07:00)
        edicion.Kilometros = "mucho";

        Assert.False(edicion.Guardar());
        Assert.Contains("Los kilómetros no son un número.", edicion.Errores);
        Assert.Equal(finOriginal, trayecto.Fin);

        edicion.Kilometros = "300";
        Assert.False(edicion.Guardar());
        Assert.Contains("La hora de fin no puede ser anterior a la de inicio.", edicion.Errores);
        Assert.Equal(finOriginal, trayecto.Fin);
    }

    [Fact]
    public void Descartar_vuelve_a_los_valores_guardados()
    {
        Historial historial = EjemplosHistorial.DosJornadas();
        var edicion = new EdicionTrayectoViewModel(historial.Jornadas[0], historial.Jornadas[0].Trayectos[0], () => { });

        edicion.Origen = "Otro sitio";
        edicion.ComandoDescartar.Execute(null);

        Assert.Equal("Madrid", edicion.Origen);
    }

    [Fact]
    public void La_jornada_abierta_no_se_puede_editar()
    {
        Historial historial = EjemplosHistorial.DosJornadas();
        var edicion = new EdicionJornadaViewModel(historial.Jornadas[1], () => { });

        Assert.False(edicion.Editable);
        Assert.False(edicion.Guardar());
    }

    [Fact]
    public void Editar_una_jornada_cerrada_cambia_sus_horas()
    {
        Historial historial = EjemplosHistorial.DosJornadas();
        Jornada jornada = historial.Jornadas[0];
        var edicion = new EdicionJornadaViewModel(jornada, () => { });

        edicion.HoraInicio = "05:30";

        Assert.True(edicion.Guardar());
        Assert.Equal(FechaJuego.Componer(176, new TimeSpan(5, 30, 0)), jornada.Inicio);
    }
}
