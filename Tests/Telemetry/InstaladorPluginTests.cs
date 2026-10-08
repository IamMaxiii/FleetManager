using FleetManager.Storage;
using FleetManager.Telemetry;
using FleetManager.Tests.Utilidades;

namespace FleetManager.Tests.Telemetry;

/// <summary>
/// Comprobar e instalar el plugin de telemetría en la carpeta del juego.
/// </summary>
public sealed class InstaladorPluginTests : IDisposable
{
    private readonly CarpetaTemporal carpeta = new();
    private readonly string pluginIncluido;
    private readonly string juego;
    private readonly string pluginJuego;
    private string? carpetaJuegoEncontrada;
    private readonly List<string> copiasComoAdministrador = [];
    private int? respuestaAdministrador = 0;

    public InstaladorPluginTests()
    {
        pluginIncluido = carpeta.Archivo("scs-telemetry.dll");
        File.WriteAllBytes(pluginIncluido, [1, 2, 3]);

        juego = carpeta.Archivo("Euro Truck Simulator 2");
        pluginJuego = Path.Combine(InstaladorPlugin.CarpetaPlugins(juego), InstaladorPlugin.NombreArchivo);
        carpetaJuegoEncontrada = juego;
    }

    public void Dispose()
    {
        if (File.Exists(pluginJuego))
        {
            File.SetAttributes(pluginJuego, FileAttributes.Normal);
        }

        carpeta.Dispose();
    }

    private InstaladorPlugin Crear() => new(
        pluginIncluido,
        () => carpetaJuegoEncontrada,
        destino =>
        {
            copiasComoAdministrador.Add(destino);
            return respuestaAdministrador;
        },
        new Registro(carpeta.Archivo("registro.log")));

    private void PonerPluginEnJuego(byte[] contenido)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(pluginJuego)!);
        File.WriteAllBytes(pluginJuego, contenido);
    }

    [Fact]
    public void Sin_juego_no_se_puede_comprobar()
    {
        carpetaJuegoEncontrada = null;

        Assert.Equal(EstadoPlugin.JuegoNoEncontrado, Crear().Comprobar());
    }

    [Fact]
    public void Sin_el_archivo_en_el_juego_falta()
    {
        Assert.Equal(EstadoPlugin.Falta, Crear().Comprobar());
    }

    [Fact]
    public void El_mismo_archivo_esta_instalado_y_otro_distinto_es_otra_version()
    {
        PonerPluginEnJuego([1, 2, 3]);
        Assert.Equal(EstadoPlugin.Instalado, Crear().Comprobar());

        PonerPluginEnJuego([9, 9]);
        Assert.Equal(EstadoPlugin.OtraVersion, Crear().Comprobar());
    }

    [Fact]
    public void Instalar_crea_la_carpeta_de_plugins_y_copia_el_archivo()
    {
        InstaladorPlugin instalador = Crear();
        instalador.Comprobar();

        Assert.Equal(ResultadoPlugin.Hecho, instalador.Instalar());

        Assert.Equal([1, 2, 3], File.ReadAllBytes(pluginJuego));
        Assert.Equal(EstadoPlugin.Instalado, instalador.Comprobar());
        Assert.Empty(copiasComoAdministrador);
    }

    [Fact]
    public void Si_la_carpeta_esta_protegida_se_pide_permiso_de_administrador()
    {
        PonerPluginEnJuego([9, 9]);
        File.SetAttributes(pluginJuego, FileAttributes.ReadOnly); // sin permiso para escribir
        InstaladorPlugin instalador = Crear();
        instalador.Comprobar();

        Assert.Equal(ResultadoPlugin.Hecho, instalador.Instalar());

        Assert.Equal([InstaladorPlugin.CarpetaPlugins(juego)], copiasComoAdministrador);
    }

    [Fact]
    public void Si_el_usuario_no_da_permiso_queda_cancelado()
    {
        PonerPluginEnJuego([9, 9]);
        File.SetAttributes(pluginJuego, FileAttributes.ReadOnly);
        respuestaAdministrador = null;
        InstaladorPlugin instalador = Crear();
        instalador.Comprobar();

        Assert.Equal(ResultadoPlugin.Cancelado, instalador.Instalar());
    }

    [Fact]
    public void El_modo_administrador_solo_copia_a_una_carpeta_de_plugins_del_juego()
    {
        var registro = new Registro(carpeta.Archivo("registro.log"));

        Assert.Equal(1, InstaladorPlugin.CopiarComoProcesoAparte(@"C:\Windows\System32", registro));
    }
}
