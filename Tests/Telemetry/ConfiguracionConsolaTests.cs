using FleetManager.Telemetry;

namespace FleetManager.Tests.Telemetry;

/// <summary>
/// Activar la consola en config.cfg cambiando solo las dos opciones necesarias.
/// </summary>
public class ConfiguracionConsolaTests
{
    private const string ConfigDesactivada =
        "uset g_lang_init \"es_es\"\r\nuset g_developer \"0\"\r\nuset g_console_state \"0\"\r\nuset g_console \"0\"\r\nuset g_fps \"0\"\r\n";

    [Fact]
    public void Con_las_opciones_a_cero_la_consola_esta_desactivada()
    {
        Assert.False(ConfiguracionConsola.EstaActivada(ConfigDesactivada));
    }

    [Fact]
    public void Activar_pone_las_dos_opciones_a_uno_y_no_toca_nada_mas()
    {
        string activada = ConfiguracionConsola.Activar(ConfigDesactivada);

        Assert.True(ConfiguracionConsola.EstaActivada(activada));
        Assert.Equal(
            "uset g_lang_init \"es_es\"\r\nuset g_developer \"1\"\r\nuset g_console_state \"0\"\r\nuset g_console \"1\"\r\nuset g_fps \"0\"\r\n",
            activada);
    }

    [Fact]
    public void Si_faltan_las_opciones_se_anaden_al_final()
    {
        string activada = ConfiguracionConsola.Activar("uset g_fps \"0\"\r\n");

        Assert.True(ConfiguracionConsola.EstaActivada(activada));
        Assert.StartsWith("uset g_fps \"0\"\r\n", activada);
    }

    [Fact]
    public void Solo_una_activada_no_basta()
    {
        Assert.False(ConfiguracionConsola.EstaActivada("uset g_developer \"1\"\nuset g_console \"0\"\n"));
    }
}
