using FleetManager.Core;
using FleetManager.Models;
using FleetManager.Tests.Utilidades;

namespace FleetManager.Tests.Core;

/// <summary>
/// Opción A2: conducción al moverse; parado con motor encendido después de conducir sigue
/// en conducción 2 min reales y luego pasa a otros trabajos desde que se paró; motor
/// apagado = descanso; arrancar el motor sin moverse = otros trabajos.
/// </summary>
public class SelectorActividadTests
{
    private static readonly DateTime Inicio = new(2026, 3, 2, 8, 0, 0);
    private static readonly TimeSpan Lectura = TimeSpan.FromMilliseconds(250);

    private readonly RelojFalso reloj = new();
    private readonly SelectorActividad selector;

    public SelectorActividadTests()
    {
        selector = new SelectorActividad(reloj);
    }

    /// <summary>Lecturas cada 250 ms durante <paramref name="tiempoReal"/>; devuelve la última decisión.</summary>
    private DecisionActividad Lecturas(TimeSpan tiempoReal, double velocidad, bool motor, DateTime hora)
    {
        reloj.Avanzar(Lectura);
        DecisionActividad decision = selector.Decidir(velocidad, motor, hora);

        for (TimeSpan t = Lectura; t < tiempoReal; t += Lectura)
        {
            reloj.Avanzar(Lectura);
            decision = selector.Decidir(velocidad, motor, hora);
        }

        return decision;
    }

    /// <summary>Conduce un momento a velocidad normal.</summary>
    private void Conducir(DateTime hora) => Lecturas(TimeSpan.FromSeconds(2), 60, motor: true, hora);

    [Fact]
    public void A_velocidad_normal_es_conduccion_al_instante()
    {
        Assert.Equal(new DecisionActividad(Actividad.Conduccion, null), selector.Decidir(80, true, Inicio));
    }

    [Fact]
    public void Maniobrar_despacio_es_conduccion_tras_un_segundo()
    {
        Assert.Equal(Actividad.OtrosTrabajos, Lecturas(Lectura * 3, 2, motor: true, Inicio).Actividad);
        Assert.Equal(Actividad.Conduccion, Lecturas(Lectura, 2, motor: true, Inicio).Actividad);

        // Seguir maniobrando un buen rato nunca cuenta como espera.
        Assert.Equal(new DecisionActividad(Actividad.Conduccion, null), Lecturas(TimeSpan.FromMinutes(5), 3, motor: true, Inicio));
    }

    [Fact]
    public void Una_vibracion_suelta_al_ralenti_no_es_conduccion()
    {
        for (int i = 0; i < 20; i++)
        {
            reloj.Avanzar(Lectura);
            double velocidad = i % 3 == 0 ? 1.5 : 0; // picos sueltos de 1,5 km/h
            Assert.Equal(Actividad.OtrosTrabajos, selector.Decidir(velocidad, true, Inicio).Actividad);
        }
    }

    [Fact]
    public void Arrancar_el_motor_tras_un_descanso_es_otros_trabajos_no_conduccion()
    {
        Assert.Equal(Actividad.Descanso, Lecturas(TimeSpan.FromSeconds(5), 0, motor: false, Inicio).Actividad);

        DecisionActividad decision = Lecturas(TimeSpan.FromSeconds(10), 0, motor: true, Inicio.AddMinutes(1));

        Assert.Equal(new DecisionActividad(Actividad.OtrosTrabajos, null), decision);
    }

    [Fact]
    public void Al_empezar_con_el_motor_encendido_y_parado_es_otros_trabajos()
    {
        Assert.Equal(new DecisionActividad(Actividad.OtrosTrabajos, null), selector.Decidir(0, true, Inicio));
    }

    [Fact]
    public void Una_parada_corta_despues_de_conducir_sigue_siendo_conduccion()
    {
        Conducir(Inicio);

        DecisionActividad decision = Lecturas(TimeSpan.FromSeconds(110), 0, motor: true, Inicio.AddMinutes(1));

        Assert.Equal(new DecisionActividad(Actividad.Conduccion, null), decision);
    }

    [Fact]
    public void Una_parada_de_mas_de_2_minutos_pasa_a_otros_trabajos_desde_que_se_paro()
    {
        Conducir(Inicio);
        selector.Decidir(0, true, Inicio.AddMinutes(1)); // se para a las 08:01 de juego

        DecisionActividad decision = Lecturas(TimeSpan.FromMinutes(2), 0, motor: true, Inicio.AddMinutes(40));

        Assert.Equal(Actividad.OtrosTrabajos, decision.Actividad);
        Assert.Equal(Inicio.AddMinutes(1), decision.ReclasificarDesde);

        // Las lecturas siguientes ya son otros trabajos sin volver a reclasificar.
        reloj.Avanzar(Lectura);
        Assert.Equal(new DecisionActividad(Actividad.OtrosTrabajos, null), selector.Decidir(0, true, Inicio.AddMinutes(41)));
    }

    [Fact]
    public void Con_el_motor_apagado_es_descanso()
    {
        Conducir(Inicio);

        Assert.Equal(new DecisionActividad(Actividad.Descanso, null), selector.Decidir(0, false, Inicio));
    }

    [Fact]
    public void Al_moverse_vuelve_a_conduccion_y_reinicia_la_espera()
    {
        Conducir(Inicio);
        Lecturas(TimeSpan.FromSeconds(100), 0, motor: true, Inicio.AddMinutes(1));
        Conducir(Inicio.AddMinutes(30));

        // Nueva parada: la espera empieza de cero.
        DecisionActividad decision = Lecturas(TimeSpan.FromSeconds(100), 0, motor: true, Inicio.AddMinutes(31));

        Assert.Equal(Actividad.Conduccion, decision.Actividad);
    }

    [Fact]
    public void Un_boton_manda_con_el_camion_parado()
    {
        Conducir(Inicio);
        selector.Decidir(0, true, Inicio);
        selector.ElegirManual(Actividad.Disponibilidad);

        Assert.Equal(Actividad.Disponibilidad, selector.Decidir(0, true, Inicio).Actividad);
        Assert.Equal(Actividad.Disponibilidad, selector.Decidir(0, false, Inicio).Actividad);
    }

    [Fact]
    public void Al_moverse_se_olvida_el_boton()
    {
        selector.ElegirManual(Actividad.Descanso);

        Assert.Equal(Actividad.Conduccion, selector.Decidir(60, true, Inicio).Actividad);
        Assert.Null(selector.ActividadManual);
        Assert.Equal(Actividad.Descanso, selector.Decidir(0, false, Inicio).Actividad);
    }

    [Fact]
    public void Una_pausa_del_juego_no_cuenta_para_la_espera()
    {
        Conducir(Inicio);
        selector.Decidir(0, true, Inicio);
        reloj.Avanzar(TimeSpan.FromMinutes(10)); // juego en pausa: no hay lecturas

        Assert.Equal(Actividad.Conduccion, selector.Decidir(0, true, Inicio).Actividad);
    }
}
