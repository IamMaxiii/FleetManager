using FleetManager.Core;
using FleetManager.Storage;
using FleetManager.Telemetry;
using FleetManager.Tests.Utilidades;
using FleetManager.ViewModels;

namespace FleetManager.Tests.ViewModels;

/// <summary>
/// Panel "Saltar tiempo" con un juego falso que obedece (o no) al comando.
/// </summary>
public sealed class SaltoTiempoViewModelTests : IDisposable
{
    private static readonly DateTime Inicio = new(2026, 3, 2, 23, 0, 0);

    private readonly CarpetaTemporal carpeta = new();
    private readonly ServicioTacografo servicio;
    private readonly SimuladorJuego juego;
    private readonly ControlJuegoFalso control = new();
    private readonly DialogosFalsos dialogos = new();

    public SaltoTiempoViewModelTests()
    {
        var reloj = new RelojFalso();
        var rutas = new RutasDatos(carpeta.Ruta);
        var almacen = new AlmacenDatos(rutas, new Registro(rutas.Registro), reloj);
        almacen.Cargar();
        servicio = new ServicioTacografo(almacen, reloj);
        juego = new SimuladorJuego(servicio, reloj, Inicio);
        juego.Leer();

        // El juego falso pone la hora que le pidan con g_set_time (avanzando, como el de verdad).
        control.AlRecibirComando = comando =>
        {
            string[] partes = comando.Split(' ');
            DateTime objetivo = SaltoTiempo.ObjetivoHasta(juego.Hora, new TimeSpan(int.Parse(partes[1]), int.Parse(partes[2]), 0));
            juego.Saltar(objetivo - juego.Hora);
        };
    }

    public void Dispose() => carpeta.Dispose();

    private SaltoTiempoViewModel Crear() => new(servicio, control, dialogos, TimeSpan.FromMilliseconds(300));

    [Fact]
    public async Task Saltar_9_horas_envia_el_comando_y_confirma_la_hora_nueva()
    {
        SaltoTiempoViewModel salto = Crear();

        await salto.SaltarDuracion(TimeSpan.FromHours(9));

        Assert.Equal(["g_set_time 8 0"], control.Comandos);
        Assert.Equal(Inicio.AddHours(9), servicio.Datos.HoraJuego);
        Assert.StartsWith("Hecho", salto.Mensaje);
        Assert.False(salto.Ocupado);
    }

    [Fact]
    public async Task Saltar_hasta_una_hora_usa_la_hora_escrita()
    {
        SaltoTiempoViewModel salto = Crear();
        salto.HoraDestino = "06:30";

        await salto.SaltarHasta();

        Assert.Equal(["g_set_time 6 30"], control.Comandos);
        Assert.Equal(new DateTime(2026, 3, 3, 6, 30, 0), servicio.Datos.HoraJuego);
    }

    [Fact]
    public async Task Una_hora_mal_escrita_no_envia_nada()
    {
        SaltoTiempoViewModel salto = Crear();
        salto.HoraDestino = "6 y media";

        await salto.SaltarHasta();

        Assert.Empty(control.Comandos);
        Assert.Contains("HH:mm", salto.Mensaje);
    }

    [Fact]
    public async Task Si_el_juego_no_cambia_de_hora_se_avisa()
    {
        control.AlRecibirComando = _ => { }; // la consola no hace nada
        SaltoTiempoViewModel salto = Crear();

        await salto.SaltarDuracion(TimeSpan.FromHours(11));

        Assert.StartsWith("La hora del juego no ha cambiado", salto.Mensaje);
    }

    [Fact]
    public void Sin_consola_activada_no_se_puede_saltar()
    {
        control.Consola = false;
        SaltoTiempoViewModel salto = Crear();

        Assert.False(salto.ConsolaActivada);
        Assert.False(salto.ComandoSaltar9.CanExecute(null));
        Assert.True(salto.ComandoActivarConsola.CanExecute(null));
    }

    [Fact]
    public void Con_el_juego_abierto_no_se_activa_la_consola()
    {
        control.Consola = false;
        control.Abierto = true;
        SaltoTiempoViewModel salto = Crear();

        salto.ComandoActivarConsola.Execute(null);

        Assert.False(salto.ConsolaActivada);
        Assert.StartsWith("Cierra el juego", salto.Mensaje);
    }

    [Fact]
    public void Con_el_juego_cerrado_se_activa_la_consola()
    {
        control.Consola = false;
        SaltoTiempoViewModel salto = Crear();

        salto.ComandoActivarConsola.Execute(null);

        Assert.True(salto.ConsolaActivada);
        Assert.StartsWith("Consola activada", salto.Mensaje);
    }

    [Fact]
    public async Task El_salto_cuenta_como_descanso_en_el_tacografo()
    {
        servicio.AbrirJornada();
        juego.Conducir(30);
        SaltoTiempoViewModel salto = Crear();

        await salto.SaltarDuracion(TimeSpan.FromHours(11));

        Assert.True(servicio.Estado.DescansoActual >= TimeSpan.FromHours(10));
        Assert.Equal(TimeSpan.Zero, servicio.Estado.ConduccionDiaria); // descanso diario cumplido
    }

    /// <summary>Juego falso: apunta los comandos y hace lo que se le diga al recibirlos.</summary>
    private sealed class ControlJuegoFalso : IControlJuego
    {
        public bool Consola { get; set; } = true;

        public bool Abierto { get; set; }

        public List<string> Comandos { get; } = [];

        public Action<string> AlRecibirComando { get; set; } = _ => { };

        public bool JuegoAbierto() => Abierto;

        public bool ConsolaActivada() => Consola;

        public ResultadoActivacion ActivarConsola()
        {
            if (Abierto)
            {
                return ResultadoActivacion.JuegoAbierto;
            }

            Consola = true;
            return ResultadoActivacion.Hecho;
        }

        public Task<bool> EnviarComando(string comando)
        {
            Comandos.Add(comando);
            AlRecibirComando(comando);
            return Task.FromResult(true);
        }
    }
}
