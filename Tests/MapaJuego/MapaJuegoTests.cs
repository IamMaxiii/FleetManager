using FleetManager.MapaJuego;

namespace FleetManager.Tests.MapaJuego;

/// <summary>
/// Cuentas de las teselas del mapa del juego, bibliotecas de Steam y mensajes del generador.
/// </summary>
public class MapaJuegoTests
{
    // Como el mapa real: un cuadrado de unos 200 km de lado, zoom 3 a 9, teselas de 256 px.
    private static readonly InfoMapaJuego Info = new(1, -94_000, -122_000, 200_000, 3, 9, 256, "prueba");

    [Fact]
    public void Cada_zoom_tiene_el_doble_de_detalle_que_el_anterior()
    {
        Assert.Equal(200_000 / 8.0, TeselasMapa.UnidadesPorTesela(Info, 3));
        Assert.Equal(TeselasMapa.Escala(Info, 4) * 2, TeselasMapa.Escala(Info, 5), 9);
    }

    [Fact]
    public void Se_elige_el_zoom_que_no_se_ve_borroso()
    {
        double escalaZoom6 = TeselasMapa.Escala(Info, 6);

        Assert.Equal(6, TeselasMapa.ZoomPara(Info, escalaZoom6));
        Assert.Equal(7, TeselasMapa.ZoomPara(Info, escalaZoom6 * 1.5));
        Assert.Equal(3, TeselasMapa.ZoomPara(Info, 0.000001)); // muy alejado: el mínimo
        Assert.Equal(9, TeselasMapa.ZoomPara(Info, 100));      // muy cerca: el máximo
    }

    [Fact]
    public void La_tesela_de_un_punto_y_la_zona_de_una_tesela_cuadran()
    {
        var (x, y) = TeselasMapa.TeselaDe(Info, 5, 23_977, -51_731);
        ZonaMundo zona = TeselasMapa.ZonaDe(Info, 5, x, y);

        Assert.InRange(23_977, zona.X, zona.Derecha);
        Assert.InRange(-51_731, zona.Z, zona.Abajo);
    }

    [Fact]
    public void Las_teselas_visibles_no_se_salen_del_mapa()
    {
        var todoYMas = new ZonaMundo(-1_000_000, -1_000_000, 3_000_000, 3_000_000);

        var teselas = TeselasMapa.TeselasEn(Info, 3, todoYMas).ToList();

        Assert.Equal(64, teselas.Count); // 8 × 8
        Assert.All(teselas, t => Assert.InRange(t.X, 0, 7));
    }

    [Fact]
    public void Las_bibliotecas_de_steam_se_leen_de_libraryfolders()
    {
        const string vdf = """
            "libraryfolders"
            {
                "0" { "path"  "C:\\Program Files (x86)\\Steam" }
                "1" { "path"  "D:\\Juegos\\SteamLibrary" }
            }
            """;

        Assert.Equal([@"C:\Program Files (x86)\Steam", @"D:\Juegos\SteamLibrary"], BuscadorJuego.LeerBibliotecas(vdf));
    }

    [Fact]
    public void Las_lineas_del_generador_se_entienden()
    {
        // El generador manda la clave del texto y sus números; FleetManager lo traduce.
        LineaGenerador? progreso = LineaGenerador.Interpretar("PROGRESO|0.2500|Generador.Dibujando|1000|4000");
        Assert.Equal(new ProgresoMapa("Dibujando el mapa: 1.000 de 4.000 teselas", 0.25), progreso?.Progreso);

        Assert.True(LineaGenerador.Interpretar("HECHO")?.EsFin);
        Assert.Equal("No hay juego", LineaGenerador.Interpretar("ERROR|No hay juego")?.Error);
        Assert.Null(LineaGenerador.Interpretar("[23:07:30|  Info] cualquier otra cosa"));
    }
}
