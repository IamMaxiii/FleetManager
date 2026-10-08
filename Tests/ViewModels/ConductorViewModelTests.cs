using FleetManager.Core;
using FleetManager.Storage;
using FleetManager.Tests.Utilidades;
using FleetManager.ViewModels;

namespace FleetManager.Tests.ViewModels;

/// <summary>
/// Perfil del conductor: números de documento y edición de los datos.
/// </summary>
public sealed class ConductorViewModelTests : IDisposable
{
    private readonly CarpetaTemporal carpeta = new();
    private readonly AlmacenDatos almacen;
    private readonly ServicioTacografo servicio;

    public ConductorViewModelTests()
    {
        var reloj = new RelojFalso();
        var rutas = new RutasDatos(carpeta.Ruta);
        almacen = new AlmacenDatos(rutas, new Registro(rutas.Registro), reloj);
        almacen.Cargar();
        servicio = new ServicioTacografo(almacen, reloj);
    }

    public void Dispose() => carpeta.Dispose();

    [Fact]
    public void La_primera_vez_se_inventan_el_permiso_y_el_ADR()
    {
        var conductor = new ConductorViewModel(almacen, servicio, new DialogosFalsos(), new Random(7));

        Assert.StartsWith("ES-", conductor.NumeroPermiso);
        Assert.StartsWith("ADR-", conductor.NumeroAdr);
        Assert.True(almacen.HayCambiosPendientes);
    }

    [Fact]
    public void Si_ya_hay_numeros_no_se_cambian()
    {
        almacen.Perfil.NumeroPermiso = "ES-123456";
        almacen.Perfil.NumeroAdr = "ADR-654321";

        var conductor = new ConductorViewModel(almacen, servicio, new DialogosFalsos(), new Random(7));

        Assert.Equal("ES-123456", conductor.NumeroPermiso);
        Assert.Equal("ADR-654321", conductor.NumeroAdr);
        Assert.False(almacen.HayCambiosPendientes);
    }

    [Fact]
    public void Cambiar_el_nombre_lo_guarda_en_el_perfil_sin_espacios_sobrantes()
    {
        var conductor = new ConductorViewModel(almacen, servicio, new DialogosFalsos(), new Random(7));
        almacen.GuardarCambiosPendientes();
        var cambiadas = new List<string?>();
        conductor.PropertyChanged += (_, e) => cambiadas.Add(e.PropertyName);

        conductor.Nombre = "  Ana  ";

        Assert.Equal("Ana", almacen.Perfil.Nombre);
        Assert.Equal("Ana", conductor.Nombre);
        Assert.Contains(nameof(ConductorViewModel.Nombre), cambiadas);
        Assert.True(almacen.HayCambiosPendientes);
    }

    [Theory]
    [InlineData("125000", 125000)]
    [InlineData("125.000", 125000)]
    [InlineData("125000,5", 125000.5)]
    [InlineData("0", 0)]
    public void Los_kilometros_de_la_tarjeta_se_pueden_cambiar_a_mano(string texto, double esperado)
    {
        var conductor = new ConductorViewModel(almacen, servicio, new DialogosFalsos(), new Random(7));

        conductor.ComandoCambiarKilometros.Execute(null);
        Assert.True(conductor.EditandoKilometros);
        conductor.KilometrosEditados = texto;

        Assert.True(conductor.GuardarKilometros());
        Assert.Equal(esperado, almacen.Perfil.KilometrosTarjeta);
        Assert.False(conductor.EditandoKilometros);
    }

    [Theory]
    [InlineData("-5")]
    [InlineData("muchos")]
    [InlineData("")]
    public void Unos_kilometros_no_validos_no_se_guardan(string texto)
    {
        almacen.Perfil.KilometrosTarjeta = 500;
        var conductor = new ConductorViewModel(almacen, servicio, new DialogosFalsos(), new Random(7));

        conductor.ComandoCambiarKilometros.Execute(null);
        conductor.KilometrosEditados = texto;

        Assert.False(conductor.GuardarKilometros());
        Assert.Equal(500, almacen.Perfil.KilometrosTarjeta);
        Assert.True(conductor.EditandoKilometros);
        Assert.NotEqual("", conductor.ErrorKilometros);
    }

    [Fact]
    public void Despues_de_cambiarlos_se_siguen_sumando_al_conducir()
    {
        var reloj = new RelojFalso();
        var conductor = new ConductorViewModel(almacen, servicio, new DialogosFalsos(), new Random(7));
        conductor.ComandoCambiarKilometros.Execute(null);
        conductor.KilometrosEditados = "1000";
        conductor.GuardarKilometros();

        var juego = new SimuladorJuego(servicio, reloj, new DateTime(2026, 3, 2, 8, 0, 0));
        juego.Leer(motor: false);
        servicio.AbrirJornada();
        juego.Conducir(30, velocidad: 60);

        Assert.InRange(almacen.Perfil.KilometrosTarjeta, 1029, 1031);
    }
}
