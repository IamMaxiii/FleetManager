using FleetManager.Core;
using FleetManager.Models;
using FleetManager.Tests.Utilidades;
using static FleetManager.Tests.Utilidades.ConstructorRegistro;

namespace FleetManager.Tests.Core;

/// <summary>
/// Conducción semanal (56 h), bisemanal (90 h) y descanso semanal (45 h / 24 h, plazo de 6 × 24 h).
/// </summary>
public class SemanalTests
{
    private static readonly DateTime Lunes = new(2026, 3, 2, 6, 0, 0);

    /// <summary>Un día de trabajo: empieza a las 06:00 del día indicado y descansa hasta las 06:00 del siguiente.</summary>
    private static ConstructorRegistro Dia(ConstructorRegistro registro, DateTime dia, TimeSpan conduccion) =>
        registro.ConducirConPausas(conduccion).DescansarHasta(dia.AddDays(1));

    [Fact]
    public void La_semana_empieza_el_lunes_a_las_0()
    {
        // Domingo 22:00 a lunes 02:00: 2 h en cada semana.
        EstadoTacografo estado = new ConstructorRegistro(new DateTime(2026, 3, 8, 22, 0, 0)).Conducir(H(4)).Analizar();

        Assert.Equal(H(2), estado.ConduccionSemanal);
        Assert.Equal(H(2), estado.ConduccionSemanaAnterior);
        Assert.Equal(H(4), estado.ConduccionBisemanal);
    }

    [Fact]
    public void Pasar_de_56h_en_la_semana_es_infraccion()
    {
        var registro = new ConstructorRegistro(Lunes);

        for (int dia = 0; dia < 6; dia++)
        {
            Dia(registro, Lunes.AddDays(dia), H(9)); // 54 h de lunes a sábado
        }

        // El descanso de sábado a domingo (14 h 15) no es semanal: el domingo a las 06:00 se cumple
        // el plazo de 6 × 24 h, así que también hay infracción por no hacer el descanso semanal.
        EstadoTacografo estado = registro.ConducirConPausas(H(3)).Analizar();

        Infraccion semanal = Assert.Single(estado.Infracciones, i => i.Tipo == TipoInfraccion.ConduccionSemanal);
        Assert.Equal(Lunes.AddDays(6) + H(2), semanal.Momento);
        Assert.Contains(estado.Infracciones, i => i.Tipo == TipoInfraccion.DescansoSemanalFueraDePlazo);
        Assert.Equal(TimeSpan.Zero, estado.ConduccionSemanalRestante);
    }

    [Fact]
    public void Pasar_de_90h_en_dos_semanas_es_infraccion()
    {
        var registro = new ConstructorRegistro(Lunes);

        // Semana 1: 5 días de 9 h y el sábado 5 h = 50 h; descanso semanal hasta el lunes.
        for (int dia = 0; dia < 5; dia++)
        {
            Dia(registro, Lunes.AddDays(dia), H(9));
        }

        registro.ConducirConPausas(H(5)).DescansarHasta(Lunes.AddDays(7));

        // Semana 2: 4 días de 9 h = 36 h; el viernes, 4 h 30 → 90 h 30 en total.
        DateTime lunes2 = Lunes.AddDays(7);

        for (int dia = 0; dia < 4; dia++)
        {
            Dia(registro, lunes2.AddDays(dia), H(9));
        }

        EstadoTacografo estado = registro.ConducirConPausas(H(4.5)).Analizar();

        Infraccion bisemanal = Assert.Single(estado.Infracciones);
        Assert.Equal(TipoInfraccion.ConduccionBisemanal, bisemanal.Tipo);
        Assert.Equal(lunes2.AddDays(4) + H(4), bisemanal.Momento);
        Assert.Equal(H(50), estado.ConduccionSemanaAnterior);
        Assert.Equal(H(40.5), estado.ConduccionSemanal);
    }

    [Fact]
    public void La_conduccion_bisemanal_se_renueva_cada_semana()
    {
        var registro = new ConstructorRegistro(Lunes);

        // Semana 1: 45 h. Semana 2: 30 h. Semana 3: 10 h. Descanso semanal cada fin de semana.
        foreach (var (semana, horasPorDia) in new[] { (0, 9.0), (1, 6.0), (2, 2.0) })
        {
            DateTime lunesSemana = Lunes.AddDays(7 * semana);

            for (int dia = 0; dia < 5; dia++)
            {
                Dia(registro, lunesSemana.AddDays(dia), H(horasPorDia));
            }

            // La última semana termina el domingo, para que "ahora" siga siendo la semana 3.
            registro.DescansarHasta(lunesSemana.AddDays(semana < 2 ? 7 : 6));
        }

        EstadoTacografo estado = registro.Analizar();

        Assert.Equal(H(10), estado.ConduccionSemanal);
        Assert.Equal(H(30), estado.ConduccionSemanaAnterior);
        Assert.Equal(H(40), estado.ConduccionBisemanal);
        Assert.Equal(H(50), estado.ConduccionBisemanalRestante);
    }

    [Fact]
    public void Un_descanso_semanal_renueva_los_reducidos_y_el_plazo_semanal()
    {
        var registro = new ConstructorRegistro(Lunes)
            .Conducir(H(4)).Descansar(H(9))
            .Conducir(H(4)).Descansar(H(9))
            .Conducir(H(4)).Descansar(H(45));
        DateTime finSemanal = registro.Hora;

        EstadoTacografo estado = registro.Conducir(H(1)).Analizar();

        Assert.Equal(3, estado.DescansosReducidosRestantes);
        Assert.Equal(finSemanal + H(144), estado.PlazoDescansoSemanal);
        Assert.Empty(estado.Infracciones);
    }

    [Fact]
    public void Trabajar_6_dias_seguidos_sin_descanso_semanal_es_infraccion()
    {
        var registro = new ConstructorRegistro(Lunes);

        for (int dia = 0; dia < 6; dia++)
        {
            Dia(registro, Lunes.AddDays(dia), H(4));
        }

        EstadoTacografo estado = registro.Conducir(H(1)).Analizar(); // domingo a las 06:00

        Infraccion infraccion = Assert.Single(estado.Infracciones);
        Assert.Equal(TipoInfraccion.DescansoSemanalFueraDePlazo, infraccion.Tipo);
        Assert.Equal(Lunes + H(144), infraccion.Momento);
    }

    [Fact]
    public void Dos_descansos_semanales_reducidos_seguidos_es_infraccion()
    {
        EstadoTacografo estado = new ConstructorRegistro(Lunes)
            .Conducir(H(4)).Descansar(H(30))
            .Conducir(H(4)).Descansar(H(30))
            .Conducir(H(1))
            .Analizar();

        Infraccion infraccion = Assert.Single(estado.Infracciones);
        Assert.Equal(TipoInfraccion.DescansosSemanalesReducidosSeguidos, infraccion.Tipo);
    }

    [Fact]
    public void Un_descanso_de_mas_de_24h_en_curso_se_ve_como_semanal()
    {
        EstadoTacografo estado = new ConstructorRegistro(Lunes).Conducir(H(4)).Descansar(H(30)).Analizar();

        Assert.True(estado.DescansoSemanalEnCurso);
        Assert.Equal(TimeSpan.Zero, estado.ConduccionDiaria);
        Assert.Equal(Actividad.Descanso, estado.ActividadActual);
        Assert.Equal(H(30), estado.TiempoActividadActual);
    }

    [Fact]
    public void Funciona_con_la_hora_del_juego_desde_el_ano_1()
    {
        // El SDK cuenta la hora del juego desde el lunes 1 de enero del año 1.
        EstadoTacografo estado = new ConstructorRegistro(DateTime.MinValue)
            .Conducir(H(2)).Descansar(M(45)).Conducir(H(1))
            .Analizar();

        Assert.Equal(H(3), estado.ConduccionSemanal);
        Assert.Equal(TimeSpan.Zero, estado.ConduccionSemanaAnterior);
        Assert.Empty(estado.Infracciones);
    }
}
