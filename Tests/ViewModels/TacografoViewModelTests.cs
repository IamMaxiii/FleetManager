using FleetManager.Core;
using FleetManager.Models;
using FleetManager.Storage;
using FleetManager.Tests.Utilidades;
using FleetManager.ViewModels;

namespace FleetManager.Tests.ViewModels;

/// <summary>
/// Lo que muestran la ventana principal y el mini tacógrafo, con el juego simulado.
/// </summary>
public sealed class TacografoViewModelTests : IDisposable
{
    private static readonly DateTime Lunes = new(2026, 3, 2, 8, 0, 0);

    private readonly CarpetaTemporal carpeta = new();
    private readonly ServicioTacografo servicio;
    private readonly SimuladorJuego juego;
    private readonly TacografoViewModel tacografo;

    public TacografoViewModelTests()
    {
        var reloj = new RelojFalso();
        var rutas = new RutasDatos(carpeta.Ruta);
        var almacen = new AlmacenDatos(rutas, new Registro(rutas.Registro), reloj);
        almacen.Cargar();
        servicio = new ServicioTacografo(almacen, reloj);
        juego = new SimuladorJuego(servicio, reloj, Lunes);
        tacografo = new TacografoViewModel(servicio, new DialogosFalsos());
    }

    public void Dispose()
    {
        tacografo.Dispose();
        carpeta.Dispose();
    }

    private void AbrirJornada()
    {
        juego.Leer();
        servicio.AbrirJornada();
    }

    [Fact]
    public void Sin_jornada_indica_como_empezar()
    {
        juego.Leer();

        Assert.Equal("SIN JORNADA", tacografo.NombreActividad);
        Assert.Equal("Pulsa «Abrir jornada» para empezar", tacografo.SubtituloActividad);
        Assert.False(tacografo.JornadaAbierta);
        Assert.True(tacografo.JuegoConectado);
    }

    [Fact]
    public void Conduciendo_muestra_la_actividad_las_barras_y_el_trayecto()
    {
        AbrirJornada();
        juego.Conducir(61, velocidad: 83, limite: 90); // el minuto de la apertura es otros trabajos: 1 h de conducción

        Assert.Equal(Actividad.Conduccion, tacografo.Actividad);
        Assert.Equal("CONDUCCIÓN", tacografo.NombreActividad);
        Assert.Equal("01:00", tacografo.TiempoActividad);
        Assert.Equal("83", tacografo.Velocidad);
        Assert.Equal("límite 90", tacografo.LimiteVelocidad);
        Assert.Equal("01:00", tacografo.Continua.Usado);
        Assert.Equal("03:30", tacografo.SiguienteParada);
        Assert.True(tacografo.HayTrayecto);
        Assert.Equal("Madrid → Zaragoza · Maquinaria", tacografo.Trayecto);
        Assert.Null(tacografo.AvisoPrincipal);
    }

    [Fact]
    public void Cerca_de_las_4h30_avisa_y_la_barra_pasa_a_atencion()
    {
        AbrirJornada();
        juego.Conducir(4 * 60 + 21); // 4 h 20 de conducción tras el minuto de la apertura

        Assert.Equal(NivelIndicador.Atencion, tacografo.Continua.Nivel);
        Assert.Equal("Parada obligatoria en 10 min.", tacografo.AvisoPrincipal?.Texto);
    }

    [Fact]
    public void Las_marcas_de_la_pausa_siguen_la_pausa_dividida()
    {
        AbrirJornada();
        juego.Conducir(60).Parar(16, motor: false);
        Assert.True(tacografo.MarcaPausa15);
        Assert.False(tacografo.MarcaPausa30);

        juego.Conducir(30).Parar(31, motor: false);
        Assert.True(tacografo.MarcaPausa30);
        Assert.Equal("Pausa cumplida", tacografo.TextoPausa);
    }

    [Fact]
    public void Las_faltas_de_la_jornada_se_cuentan()
    {
        AbrirJornada();
        juego.Conducir(25, velocidad: 100, limite: 80);

        Assert.Equal(1, tacografo.FaltasJornada);
    }
}
