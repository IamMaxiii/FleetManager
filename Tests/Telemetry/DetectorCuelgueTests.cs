using FleetManager.Telemetry;
using FleetManager.Tests.Utilidades;

namespace FleetManager.Tests.Telemetry;

/// <summary>
/// El juego se da por colgado si su reloj interno no avanza durante 5 s sin estar en pausa.
/// </summary>
public class DetectorCuelgueTests
{
    private readonly RelojFalso reloj = new();
    private readonly DetectorCuelgue detector;

    public DetectorCuelgueTests()
    {
        detector = new DetectorCuelgue(reloj, DetectorCuelgue.EsperaPorDefecto);
    }

    [Fact]
    public void Si_el_reloj_del_juego_avanza_no_esta_colgado()
    {
        for (ulong marca = 0; marca < 100; marca++)
        {
            reloj.Avanzar(TimeSpan.FromSeconds(1));
            Assert.False(detector.EstaColgado(marca, pausado: false));
        }
    }

    [Fact]
    public void Si_el_reloj_del_juego_se_para_5_s_esta_colgado()
    {
        detector.EstaColgado(500, pausado: false);
        reloj.Avanzar(TimeSpan.FromSeconds(4));
        Assert.False(detector.EstaColgado(500, pausado: false));

        reloj.Avanzar(TimeSpan.FromSeconds(1));
        Assert.True(detector.EstaColgado(500, pausado: false));
    }

    [Fact]
    public void En_pausa_no_esta_colgado()
    {
        detector.EstaColgado(500, pausado: true);
        reloj.Avanzar(TimeSpan.FromMinutes(5));

        Assert.False(detector.EstaColgado(500, pausado: true));
    }

    [Fact]
    public void Si_el_reloj_vuelve_a_avanzar_deja_de_estar_colgado()
    {
        detector.EstaColgado(500, pausado: false);
        reloj.Avanzar(TimeSpan.FromSeconds(10));
        Assert.True(detector.EstaColgado(500, pausado: false));

        Assert.False(detector.EstaColgado(501, pausado: false));
    }
}
