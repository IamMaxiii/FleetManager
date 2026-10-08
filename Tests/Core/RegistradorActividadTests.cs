using FleetManager.Core;
using FleetManager.Models;
using static FleetManager.Tests.Utilidades.ConstructorRegistro;

namespace FleetManager.Tests.Core;

/// <summary>
/// Escritura del registro a partir de lecturas "hora del juego + actividad",
/// incluidos los saltos del reloj del juego.
/// </summary>
public class RegistradorActividadTests
{
    private static readonly DateTime Inicio = new(2026, 3, 2, 8, 0, 0);

    private readonly RegistroTacografo registro = new();
    private readonly RegistradorActividad registrador;

    public RegistradorActividadTests()
    {
        registrador = new RegistradorActividad(registro);
    }

    private List<PeriodoActividad> Periodos => registro.Periodos;

    /// <summary>Una lectura por minuto entre dos horas (ambas incluidas), como hará el juego.</summary>
    private void Lecturas(DateTime desde, DateTime hasta, Actividad actividad)
    {
        for (DateTime hora = desde; hora <= hasta; hora += M(1))
        {
            registrador.Registrar(hora, actividad);
        }
    }

    [Fact]
    public void La_primera_lectura_crea_un_periodo()
    {
        Assert.Equal(ResultadoRegistro.Normal, registrador.Registrar(Inicio, Actividad.Conduccion));

        PeriodoActividad periodo = Assert.Single(Periodos);
        Assert.Equal(Actividad.Conduccion, periodo.Actividad);
        Assert.Equal(Inicio, periodo.Inicio);
        Assert.Equal(Inicio, periodo.Fin);
    }

    [Fact]
    public void La_misma_actividad_alarga_el_periodo()
    {
        registrador.Registrar(Inicio, Actividad.Conduccion);
        registrador.Registrar(Inicio + M(1), Actividad.Conduccion);
        registrador.Registrar(Inicio + M(2), Actividad.Conduccion);

        PeriodoActividad periodo = Assert.Single(Periodos);
        Assert.Equal(M(2), periodo.Duracion);
    }

    [Fact]
    public void Un_cambio_de_actividad_cierra_el_periodo_anterior_en_la_hora_del_cambio()
    {
        Lecturas(Inicio, Inicio + M(30), Actividad.Conduccion);
        Lecturas(Inicio + M(31), Inicio + M(40), Actividad.Descanso);

        Assert.Equal(2, Periodos.Count);
        Assert.Equal(Inicio + M(31), Periodos[0].Fin);
        Assert.Equal(Actividad.Descanso, Periodos[1].Actividad);
        Assert.Equal(Inicio + M(31), Periodos[1].Inicio);
        Assert.Equal(Inicio + M(40), Periodos[1].Fin);
    }

    [Fact]
    public void Un_salto_hacia_delante_se_apunta_como_descanso()
    {
        Lecturas(Inicio, Inicio + H(1), Actividad.Conduccion);

        // Duerme: el reloj salta 9 h y al despertar sigue conduciendo.
        var resultado = registrador.Registrar(Inicio + H(10), Actividad.Conduccion);

        Assert.Equal(ResultadoRegistro.SaltoAdelante, resultado);
        Assert.Equal(3, Periodos.Count);
        Assert.Equal(Actividad.Descanso, Periodos[1].Actividad);
        Assert.Equal(Inicio + H(1), Periodos[1].Inicio);
        Assert.Equal(Inicio + H(10), Periodos[1].Fin);
        Assert.Equal(Actividad.Conduccion, Periodos[2].Actividad);
        Assert.Equal(Inicio + H(10), Periodos[2].Inicio);
    }

    [Fact]
    public void Un_salto_hacia_delante_estando_en_descanso_alarga_el_mismo_descanso()
    {
        registrador.Registrar(Inicio, Actividad.Descanso);
        registrador.Registrar(Inicio + H(9), Actividad.Descanso);

        PeriodoActividad periodo = Assert.Single(Periodos);
        Assert.Equal(H(9), periodo.Duracion);
    }

    [Fact]
    public void Un_salto_pequeno_se_atribuye_a_la_actividad_anterior()
    {
        registrador.Registrar(Inicio, Actividad.Conduccion);
        var resultado = registrador.Registrar(Inicio + M(10), Actividad.Conduccion);

        Assert.Equal(ResultadoRegistro.Normal, resultado);
        Assert.Equal(M(10), Assert.Single(Periodos).Duracion);
    }

    [Fact]
    public void Un_salto_hacia_atras_borra_lo_posterior_a_la_nueva_hora()
    {
        Lecturas(Inicio, Inicio + H(2) - M(1), Actividad.Conduccion);
        Lecturas(Inicio + H(2), Inicio + H(3) - M(1), Actividad.Descanso);
        Lecturas(Inicio + H(3), Inicio + H(4), Actividad.Conduccion);

        // Carga una partida guardada de una hora y media después del inicio.
        var resultado = registrador.Registrar(Inicio + H(1.5), Actividad.OtrosTrabajos);

        Assert.Equal(ResultadoRegistro.SaltoAtras, resultado);
        Assert.Equal(2, Periodos.Count);
        Assert.Equal(Actividad.Conduccion, Periodos[0].Actividad);
        Assert.Equal(Inicio + H(1.5), Periodos[0].Fin);
        Assert.Equal(Actividad.OtrosTrabajos, Periodos[1].Actividad);
        Assert.Equal(Inicio + H(1.5), Periodos[1].Inicio);
    }

    [Fact]
    public void Un_salto_hacia_atras_a_antes_de_todo_empieza_de_nuevo()
    {
        registrador.Registrar(Inicio, Actividad.Conduccion);
        registrador.Registrar(Inicio + H(2), Actividad.Conduccion);

        registrador.Registrar(Inicio - H(5), Actividad.Descanso);

        PeriodoActividad periodo = Assert.Single(Periodos);
        Assert.Equal(Actividad.Descanso, periodo.Actividad);
        Assert.Equal(Inicio - H(5), periodo.Inicio);
    }

    [Fact]
    public void Varios_cambios_en_el_mismo_minuto_no_dejan_periodos_vacios()
    {
        registrador.Registrar(Inicio, Actividad.Descanso);
        registrador.Registrar(Inicio + M(5), Actividad.OtrosTrabajos);
        registrador.Registrar(Inicio + M(5), Actividad.Disponibilidad);
        registrador.Registrar(Inicio + M(5), Actividad.Conduccion);
        registrador.Registrar(Inicio + M(8), Actividad.Conduccion);

        Assert.Equal(2, Periodos.Count);
        Assert.Equal(Actividad.Descanso, Periodos[0].Actividad);
        Assert.Equal(Actividad.Conduccion, Periodos[1].Actividad);
        Assert.Equal(M(3), Periodos[1].Duracion);
    }

    [Fact]
    public void Volver_a_la_actividad_anterior_en_el_mismo_minuto_la_une()
    {
        registrador.Registrar(Inicio, Actividad.Conduccion);
        registrador.Registrar(Inicio + M(5), Actividad.Descanso);
        registrador.Registrar(Inicio + M(5), Actividad.Conduccion);
        registrador.Registrar(Inicio + M(9), Actividad.Conduccion);

        PeriodoActividad periodo = Assert.Single(Periodos);
        Assert.Equal(M(9), periodo.Duracion);
    }

    [Fact]
    public void Reclasificar_cambia_el_tramo_desde_la_hora_indicada()
    {
        Lecturas(Inicio, Inicio + M(30), Actividad.Conduccion);
        Lecturas(Inicio + M(31), Inicio + M(70), Actividad.Conduccion); // parado, aún contado como conducción

        registrador.Reclasificar(Inicio + M(30), Inicio + M(70), Actividad.OtrosTrabajos);

        Assert.Equal(2, Periodos.Count);
        Assert.Equal(Inicio + M(30), Periodos[0].Fin);
        Assert.Equal(Actividad.OtrosTrabajos, Periodos[1].Actividad);
        Assert.Equal(Inicio + M(30), Periodos[1].Inicio);
        Assert.Equal(Inicio + M(70), Periodos[1].Fin);
    }

    [Fact]
    public void Se_descartan_los_periodos_de_mas_de_8_semanas()
    {
        registrador.Registrar(Inicio, Actividad.Conduccion);
        registrador.Registrar(Inicio + H(1), Actividad.Descanso);
        registrador.Registrar(Inicio + H(2), Actividad.Conduccion);

        // Nueve semanas después (con el hueco apuntado como descanso).
        registrador.Registrar(Inicio + TimeSpan.FromDays(63), Actividad.Conduccion);

        Assert.All(Periodos, p => Assert.True(p.Fin >= Inicio + TimeSpan.FromDays(63) - ReglasUE.AntiguedadMaximaRegistro));
        Assert.Equal(Actividad.Conduccion, Periodos[^1].Actividad);
    }

    [Fact]
    public void Funciona_con_la_hora_del_juego_desde_el_ano_1()
    {
        Lecturas(DateTime.MinValue, DateTime.MinValue + M(30), Actividad.Conduccion);
        Lecturas(DateTime.MinValue + M(31), DateTime.MinValue + M(40), Actividad.Descanso);

        Assert.Equal(2, Periodos.Count);
    }
}
