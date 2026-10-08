using System.Windows;
using FleetManager.ViewModels;

namespace FleetManager.Tests.ViewModels;

/// <summary>
/// El mini tacógrafo siempre queda entero dentro de la pantalla.
/// </summary>
public class PosicionVentanaTests
{
    private static readonly Rect Pantalla = new(0, 0, 1920, 1080);
    private static readonly Size Mini = new(270, 140);

    [Fact]
    public void Sin_posicion_guardada_va_arriba_a_la_derecha()
    {
        Assert.Equal(new Point(1920 - 270 - 16, 16), PosicionVentana.Ajustar(null, null, Mini, Pantalla));
    }

    [Fact]
    public void Una_posicion_dentro_de_la_pantalla_se_respeta()
    {
        Assert.Equal(new Point(500, 300), PosicionVentana.Ajustar(500, 300, Mini, Pantalla));
    }

    [Fact]
    public void Si_se_sale_por_la_derecha_o_por_abajo_se_mete_dentro()
    {
        Assert.Equal(new Point(1920 - 270, 1080 - 140), PosicionVentana.Ajustar(1900, 1070, Mini, Pantalla));
    }

    [Fact]
    public void Si_estaba_en_un_monitor_que_ya_no_esta_vuelve_a_la_pantalla()
    {
        Assert.Equal(new Point(0, 0), PosicionVentana.Ajustar(-2500, -300, Mini, Pantalla));
    }

    [Fact]
    public void Con_dos_monitores_puede_estar_en_el_segundo()
    {
        var dosMonitores = new Rect(0, 0, 3840, 1080);

        Assert.Equal(new Point(2500, 100), PosicionVentana.Ajustar(2500, 100, Mini, dosMonitores));
    }
}
