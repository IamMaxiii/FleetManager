using FleetManager.Core;
using FleetManager.Models;
using FleetManager.Storage;
using FleetManager.Tests.Utilidades;

namespace FleetManager.Tests.Core;

/// <summary>
/// La pieza central con lecturas simuladas del juego: registro del tacógrafo,
/// trayectos automáticos, faltas, kilómetros de la tarjeta y guardado.
/// </summary>
public sealed class ServicioTacografoTests : IDisposable
{
    private static readonly DateTime Lunes = new(2026, 3, 2, 8, 0, 0);

    private readonly CarpetaTemporal carpeta = new();
    private readonly RelojFalso reloj = new();
    private readonly RutasDatos rutas;
    private readonly AlmacenDatos almacen;
    private readonly ServicioTacografo servicio;
    private readonly SimuladorJuego juego;

    public ServicioTacografoTests()
    {
        rutas = new RutasDatos(carpeta.Ruta);
        almacen = new AlmacenDatos(rutas, new Registro(rutas.Registro), reloj);
        almacen.Cargar();
        servicio = new ServicioTacografo(almacen, reloj);
        juego = new SimuladorJuego(servicio, reloj, Lunes);
    }

    public void Dispose() => carpeta.Dispose();

    private List<PeriodoActividad> Periodos => almacen.Tacografo.Periodos;

    /// <summary>Abre la jornada con el camión aparcado y el motor apagado, como al empezar el día.</summary>
    private void AbrirJornada()
    {
        juego.Leer(motor: false);
        Assert.True(servicio.AbrirJornada());
    }

    [Fact]
    public void Sin_juego_no_se_puede_abrir_jornada()
    {
        Assert.False(servicio.AbrirJornada());
    }

    [Fact]
    public void Sin_jornada_abierta_no_se_registra_nada()
    {
        juego.Conducir(30);

        Assert.Empty(Periodos);
        Assert.Null(servicio.TrayectoEnCurso);
    }

    [Fact]
    public void Abrir_jornada_la_guarda_al_momento()
    {
        AbrirJornada();

        Jornada jornada = Assert.Single(almacen.Historial.Jornadas);
        Assert.Equal(1, jornada.Numero);
        Assert.Equal(Lunes, jornada.Inicio);
        Assert.True(File.Exists(rutas.Historial));
    }

    [Fact]
    public void Al_conducir_se_registra_conduccion_y_se_crea_el_trayecto()
    {
        AbrirJornada();
        juego.Conducir(60, velocidad: 80);

        // El minuto de la apertura (08:00-08:01) fue descanso: se conduce de 08:01 a 09:00.
        Assert.Equal(Actividad.Conduccion, servicio.Estado.ActividadActual);
        Assert.Equal(TimeSpan.FromMinutes(59), servicio.Estado.ConduccionContinua);

        Trayecto trayecto = Assert.IsType<Trayecto>(servicio.TrayectoEnCurso);
        Assert.Equal("Madrid", trayecto.Origen);
        Assert.Equal("Zaragoza", trayecto.Destino);
        Assert.Equal("Maquinaria", trayecto.Carga);
        Assert.InRange(trayecto.Kilometros, 79, 80);
        Assert.InRange(trayecto.VelocidadMedia, 79, 81);
        Assert.Equal(80, trayecto.VelocidadMaxima);
    }

    [Fact]
    public void Un_semaforo_sigue_contando_como_conduccion()
    {
        AbrirJornada();
        juego.Conducir(20).Parar(30).Conducir(20); // 30 min de juego = 90 s reales parado

        Assert.Single(Periodos, p => p.Actividad == Actividad.Conduccion);
        Assert.DoesNotContain(Periodos, p => p.Actividad == Actividad.OtrosTrabajos);
        Assert.NotNull(servicio.TrayectoEnCurso);
    }

    [Fact]
    public void Maniobrar_despacio_cuenta_como_conduccion()
    {
        AbrirJornada();
        juego.Conducir(11, velocidad: 3); // maniobrando a 3 km/h de 08:01 a 08:11

        Assert.Equal(Actividad.Conduccion, servicio.Estado.ActividadActual);
        Assert.Equal(TimeSpan.FromMinutes(10), servicio.Estado.ConduccionContinua);
        Assert.NotNull(servicio.TrayectoEnCurso);
    }

    [Fact]
    public void Pasar_una_espera_a_otros_trabajos_no_cierra_el_trayecto()
    {
        AbrirJornada();
        juego.Conducir(10).Parar(46, motor: false); // pausa válida: termina el primer trayecto
        juego.Conducir(1).Parar(60);                // nuevo trayecto y espera larga con el motor encendido

        Assert.NotNull(servicio.TrayectoEnCurso);
        Assert.Single(almacen.Historial.Jornadas[0].Trayectos);
    }

    [Fact]
    public void Una_espera_larga_pasa_a_otros_trabajos_desde_que_se_paro()
    {
        AbrirJornada();
        juego.Conducir(20);
        DateTime parada = juego.Hora.AddMinutes(1); // hora de la primera lectura ya parado
        juego.Parar(60); // 60 min de juego = 3 min reales

        PeriodoActividad otros = Assert.Single(Periodos, p => p.Actividad == Actividad.OtrosTrabajos);
        Assert.Equal(parada, otros.Inicio);
        Assert.Equal(Actividad.OtrosTrabajos, servicio.Estado.ActividadActual);

        // Otros trabajos no es pausa: el trayecto sigue, pero termina donde se paró.
        Trayecto trayecto = Assert.IsType<Trayecto>(servicio.TrayectoEnCurso);
        Assert.Equal(parada, trayecto.Fin);
        Assert.InRange(trayecto.TiempoConduccion.TotalMinutes, 18, 20);
    }

    [Fact]
    public void Una_pausa_de_45_min_con_el_motor_apagado_cierra_el_trayecto_y_lo_guarda()
    {
        AbrirJornada();
        juego.Conducir(90).Parar(46, motor: false);

        Assert.Null(servicio.TrayectoEnCurso);
        Trayecto trayecto = Assert.Single(almacen.Historial.Jornadas[0].Trayectos);
        Assert.InRange(trayecto.Kilometros, 119, 120);
        Assert.Equal(TimeSpan.Zero, servicio.Estado.ConduccionContinua);
        Assert.Contains("Zaragoza", File.ReadAllText(rutas.Historial));
    }

    [Fact]
    public void Una_pausa_de_menos_de_45_min_no_cierra_el_trayecto()
    {
        AbrirJornada();
        juego.Conducir(60).Parar(30, motor: false).Conducir(10);

        Assert.NotNull(servicio.TrayectoEnCurso);
        Assert.Empty(almacen.Historial.Jornadas[0].Trayectos);
    }

    [Fact]
    public void Dormir_en_el_juego_cuenta_como_descanso()
    {
        AbrirJornada();
        juego.Conducir(60).Parar(1, motor: false).Saltar(TimeSpan.FromHours(9));

        Assert.Equal(Actividad.Descanso, servicio.Estado.ActividadActual);
        Assert.True(servicio.Estado.DescansoActual >= TimeSpan.FromHours(9));
    }

    [Fact]
    public void Ir_60_s_por_encima_del_limite_marca_falta_de_velocidad()
    {
        AbrirJornada();
        juego.Conducir(25, velocidad: 100, limite: 80); // 25 min de juego = 75 s reales

        Assert.Equal(NivelVelocidad.Falta, servicio.NivelVelocidad);
        Assert.True(servicio.TrayectoEnCurso!.FaltaVelocidad);
    }

    [Fact]
    public void Ir_poco_tiempo_por_encima_del_limite_no_es_falta()
    {
        AbrirJornada();
        juego.Conducir(10, velocidad: 100, limite: 80).Conducir(10, velocidad: 70, limite: 80);

        Assert.False(servicio.TrayectoEnCurso!.FaltaVelocidad);
    }

    [Fact]
    public void Conducir_mas_de_4h30_sin_pausa_marca_falta_de_conduccion()
    {
        AbrirJornada();
        juego.Conducir(4 * 60 + 35);

        Assert.True(servicio.TrayectoEnCurso!.FaltaConduccion);
        Assert.Contains(servicio.Estado.Infracciones, i => i.Tipo == TipoInfraccion.ConduccionContinua);
    }

    [Fact]
    public void Un_boton_cambia_la_actividad_con_el_camion_parado()
    {
        AbrirJornada();
        juego.Conducir(10).Parar(1);
        servicio.ElegirActividad(Actividad.Disponibilidad);
        juego.Parar(5);

        Assert.Equal(Actividad.Disponibilidad, servicio.Estado.ActividadActual);

        juego.Conducir(5);

        Assert.Equal(Actividad.Conduccion, servicio.Estado.ActividadActual);
        Assert.Null(servicio.ActividadManual);
    }

    [Fact]
    public void Con_el_juego_en_pausa_no_se_registra_nada()
    {
        AbrirJornada();
        juego.Conducir(10);
        int periodos = Periodos.Count;
        DateTime fin = Periodos[^1].Fin;

        juego.Leer(80, pausado: true);

        Assert.Equal(periodos, Periodos.Count);
        Assert.Equal(fin, Periodos[^1].Fin);
    }

    [Fact]
    public void Los_kilometros_de_la_tarjeta_suman_el_odometro_pero_no_los_saltos()
    {
        AbrirJornada();
        juego.Conducir(60, velocidad: 60);
        double tras60 = almacen.Perfil.KilometrosTarjeta;

        juego.Odometro += 500; // cambio de partida o teletransporte
        juego.Conducir(1, velocidad: 60);

        Assert.InRange(tras60, 59.5, 60.5);
        Assert.InRange(almacen.Perfil.KilometrosTarjeta - tras60, 0, 1.1);
    }

    [Fact]
    public void Cerrar_la_jornada_cierra_el_trayecto_y_lo_guarda()
    {
        AbrirJornada();
        juego.Conducir(30);

        Assert.True(servicio.CerrarJornada());

        Jornada jornada = Assert.Single(almacen.Historial.Jornadas);
        Assert.False(jornada.Abierta);
        Assert.Equal(juego.Hora, jornada.Fin);
        Assert.Single(jornada.Trayectos);
        Assert.Null(jornada.TrayectoEnCurso);
        Assert.False(almacen.HayCambiosPendientes);
    }

    [Fact]
    public void El_tiempo_con_la_jornada_cerrada_cuenta_como_descanso()
    {
        AbrirJornada();
        juego.Conducir(60);
        servicio.CerrarJornada();

        juego.Parar(60, motor: true); // jornada cerrada: no se registra
        Assert.True(servicio.AbrirJornada());
        juego.Conducir(1);

        Assert.Contains(Periodos, p => p.Actividad == Actividad.Descanso && p.Duracion >= TimeSpan.FromMinutes(60));
    }

    [Fact]
    public void Con_la_jornada_abierta_el_recorrido_se_apunta_con_el_minuto_de_la_jornada()
    {
        AbrirJornada();
        juego.Conducir(10, velocidad: 60); // 10 km hacia el este

        List<int[]> tramo = Assert.Single(servicio.Recorrido.Tramos);
        Assert.InRange(tramo.Count, 15, 30);
        Assert.Equal(1000, tramo[0][0]);
        Assert.Equal(0, tramo[0][2]);       // minuto 0 de la jornada
        Assert.Equal(10, tramo[^1][2]);     // el último punto, en el minuto 10
        Assert.True(servicio.VersionRecorrido > 0);
    }

    [Fact]
    public void Sin_jornada_abierta_no_se_apunta_recorrido()
    {
        juego.Leer();
        juego.Conducir(10, velocidad: 60);

        Assert.Empty(servicio.Recorrido.Tramos);
    }

    [Fact]
    public void Al_abrir_una_jornada_nueva_el_recorrido_empieza_vacio_y_el_anterior_se_conserva()
    {
        AbrirJornada();
        juego.Conducir(10, velocidad: 60);
        servicio.CerrarJornada();
        Guid primera = almacen.Historial.Jornadas[0].Id;
        int puntosPrimera = almacen.LeerRecorrido(primera).Tramos[0].Count;

        Assert.True(servicio.AbrirJornada());

        Assert.Equal(almacen.Historial.Jornadas[1].Id, almacen.RecorridoDe);
        Assert.True(servicio.Recorrido.Tramos.Count <= 1 && servicio.Recorrido.Tramos.Sum(t => t.Count) <= 1);
        Assert.Equal(puntosPrimera, almacen.LeerRecorrido(primera).Tramos[0].Count);
    }

    [Fact]
    public void El_recorrido_se_guarda_compacto_y_al_volver_a_abrir_se_ve_el_de_la_ultima_jornada()
    {
        AbrirJornada();
        juego.Conducir(5, velocidad: 60);
        servicio.CerrarJornada();
        Guid jornada = almacen.Historial.Jornadas[0].Id;

        string texto = File.ReadAllText(rutas.Recorrido(jornada));
        Assert.DoesNotContain("\n", texto); // sin saltos de línea: ocupa menos

        var almacenNuevo = new AlmacenDatos(rutas, new Registro(rutas.Registro), reloj);
        almacenNuevo.Cargar();
        var servicioNuevo = new ServicioTacografo(almacenNuevo, reloj);

        Assert.Equal(jornada, almacenNuevo.RecorridoDe);
        Assert.Equal(almacen.Recorrido.Tramos[0].Count, servicioNuevo.Recorrido.Tramos[0].Count);
    }

    [Fact]
    public void Al_volver_a_abrir_la_aplicacion_todo_sigue_donde_estaba()
    {
        AbrirJornada();
        juego.Conducir(46); // conducción de 08:01 a 08:46
        almacen.GuardarCambiosPendientes();

        var almacenNuevo = new AlmacenDatos(rutas, new Registro(rutas.Registro), reloj);
        almacenNuevo.Cargar();
        var servicioNuevo = new ServicioTacografo(almacenNuevo, reloj);

        Assert.NotNull(servicioNuevo.JornadaAbierta);
        Assert.NotNull(servicioNuevo.TrayectoEnCurso);
        Assert.Equal(TimeSpan.FromMinutes(45), servicioNuevo.Estado.ConduccionContinua);
    }
}
