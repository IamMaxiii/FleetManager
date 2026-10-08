using System.Windows;
using FleetManager.Models;
using FleetManager.ViewModels;

namespace FleetManager.Tests.ViewModels;

/// <summary>
/// Cuentas del mapa: de coordenadas del juego a píxeles, acercar, arrastrar y encuadrar.
/// </summary>
public class VistaMapaTests
{
    private static readonly Size Pantalla = new(800, 600);

    [Fact]
    public void El_centro_del_mapa_esta_en_el_centro_de_la_pantalla()
    {
        var vista = new VistaMapa { Centro = new Point(5000, -3000) };

        Assert.Equal(new Point(400, 300), vista.APantalla(new Point(5000, -3000), Pantalla));
    }

    [Fact]
    public void Un_km_al_este_son_50_pixeles_a_la_derecha_con_la_escala_inicial()
    {
        var vista = new VistaMapa { Centro = new Point(0, 0) };

        Assert.Equal(new Point(450, 300), vista.APantalla(new Point(1000, 0), Pantalla));
    }

    [Fact]
    public void Ir_y_volver_entre_pantalla_y_mundo_da_el_mismo_punto()
    {
        var vista = new VistaMapa { Centro = new Point(1234, 5678) };
        var mundo = new Point(2000, 6000);

        Point vuelta = vista.AMundo(vista.APantalla(mundo, Pantalla), Pantalla);

        Assert.Equal(mundo.X, vuelta.X, 6);
        Assert.Equal(mundo.Y, vuelta.Y, 6);
    }

    [Fact]
    public void Al_acercar_el_punto_bajo_el_raton_no_se_mueve()
    {
        var vista = new VistaMapa { Centro = new Point(0, 0) };
        var raton = new Point(650, 120);
        Point antes = vista.AMundo(raton, Pantalla);

        vista.Acercar(2, raton, Pantalla);

        Point despues = vista.AMundo(raton, Pantalla);
        Assert.Equal(antes.X, despues.X, 6);
        Assert.Equal(antes.Y, despues.Y, 6);
        Assert.Equal(VistaMapa.EscalaInicial * 2, vista.Escala, 9);
    }

    [Fact]
    public void La_escala_tiene_limites()
    {
        var vista = new VistaMapa();

        for (int i = 0; i < 100; i++)
        {
            vista.Acercar(2, new Point(400, 300), Pantalla);
        }

        Assert.Equal(VistaMapa.EscalaMaxima, vista.Escala);
    }

    [Fact]
    public void Arrastrar_a_la_derecha_mueve_el_mapa_hacia_el_oeste()
    {
        var vista = new VistaMapa { Centro = new Point(0, 0) };

        vista.Desplazar(new Vector(50, 0)); // 50 px = 1 km con la escala inicial

        Assert.Equal(new Point(-1000, 0), vista.Centro);
    }

    [Fact]
    public void Encuadrar_hace_que_quepa_todo_el_recorrido()
    {
        var mapa = new MapaRecorrido { Tramos = { new List<int[]> { new[] { 0, 0 }, new[] { 40_000, 10_000 } } } };
        var vista = new VistaMapa();

        vista.Encuadrar(VistaMapa.Limites(mapa), Pantalla);

        Assert.Equal(new Point(20_000, 5_000), vista.Centro);
        Point izquierda = vista.APantalla(new Point(0, 0), Pantalla);
        Point derecha = vista.APantalla(new Point(40_000, 10_000), Pantalla);
        Assert.InRange(izquierda.X, 0, 800);
        Assert.InRange(derecha.X, 0, 800);
    }

    [Fact]
    public void Sin_recorrido_los_limites_estan_vacios()
    {
        Assert.True(VistaMapa.Limites(new MapaRecorrido()).IsEmpty);
    }
}
