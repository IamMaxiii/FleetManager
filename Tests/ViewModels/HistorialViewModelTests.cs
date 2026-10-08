using FleetManager.MapaJuego;
using FleetManager.Models;
using FleetManager.Storage;
using FleetManager.Tests.Utilidades;
using FleetManager.ViewModels;

namespace FleetManager.Tests.ViewModels;

/// <summary>
/// Sección "Historial": árbol, tabla, edición, borrado con copia y exportación.
/// </summary>
public sealed class HistorialViewModelTests : IDisposable
{
    private readonly CarpetaTemporal carpeta = new();
    private readonly RutasDatos rutas;
    private readonly AlmacenDatos almacen;
    private readonly DialogosFalsos dialogos = new();
    private readonly HistorialViewModel historial;

    public HistorialViewModelTests()
    {
        rutas = new RutasDatos(carpeta.Ruta);
        almacen = new AlmacenDatos(rutas, new Registro(rutas.Registro), new RelojFalso());
        almacen.Cargar();
        almacen.ReemplazarHistorial(EjemplosHistorial.DosJornadas());
        almacen.GuardarCambiosPendientes();
        var mapaJuego = new MapaJuegoViewModel(new ServicioMapaJuego(carpeta.Archivo("mapa-juego"), new Registro(rutas.Registro)), dialogos);
        historial = new HistorialViewModel(almacen, dialogos, mapaJuego);
    }

    public void Dispose()
    {
        historial.Dispose();
        carpeta.Dispose();
    }

    private NodoHistorial NodoJornada(int numero) =>
        historial.Arbol.Single(n => n.Tipo == TipoNodo.Jornada && n.Jornada!.Numero == numero);

    [Fact]
    public void El_arbol_tiene_todo_el_historial_y_las_jornadas_de_la_mas_reciente_a_la_mas_antigua()
    {
        Assert.Equal(["Todo el historial", "Jornada 2 (abierta)", "Jornada 1"], historial.Arbol.Select(n => n.Titulo));

        NodoHistorial jornada1 = NodoJornada(1);
        Assert.Equal(["Día 176 (lun)", "Día 177 (mar)"], jornada1.Hijos.Select(n => n.Titulo));
        Assert.Equal(["Madrid → Zaragoza", "Zaragoza → Barcelona"], jornada1.Hijos[0].Hijos.Select(n => n.Titulo));
    }

    [Fact]
    public void Al_principio_la_tabla_muestra_todos_los_trayectos()
    {
        Assert.Equal(TipoNodo.Todo, historial.NodoSeleccionado?.Tipo);
        Assert.Equal(4, historial.Filas.Count);
        Assert.StartsWith("4 trayectos", historial.Resumen);
    }

    [Fact]
    public void Elegir_una_jornada_muestra_sus_trayectos_y_su_panel_de_edicion()
    {
        historial.NodoSeleccionado = NodoJornada(1);

        Assert.Equal(3, historial.Filas.Count);
        Assert.IsType<EdicionJornadaViewModel>(historial.Edicion);
    }

    [Fact]
    public void Elegir_una_fila_abre_la_edicion_de_ese_trayecto()
    {
        historial.FilasSeleccionadas = [historial.Filas[0]];

        var edicion = Assert.IsType<EdicionTrayectoViewModel>(historial.Edicion);
        Assert.Equal("Madrid", edicion.Origen);
    }

    [Fact]
    public void Guardar_una_edicion_la_escribe_en_disco_y_rehace_el_arbol()
    {
        historial.NodoSeleccionado = NodoJornada(1);
        historial.FilasSeleccionadas = [historial.Filas[0]];
        var edicion = Assert.IsType<EdicionTrayectoViewModel>(historial.Edicion);

        edicion.Destino = "Huesca";
        Assert.True(edicion.Guardar());

        Assert.Contains("Huesca", File.ReadAllText(rutas.Historial));
        Assert.Equal("Madrid → Huesca", NodoJornada(1).Hijos[0].Hijos[0].Titulo);
        Assert.Equal(TipoNodo.Jornada, historial.NodoSeleccionado?.Tipo); // sigue elegida la misma jornada
    }

    [Fact]
    public void Borrar_trayectos_guarda_antes_una_copia()
    {
        historial.FilasSeleccionadas = [historial.Filas[0], historial.Filas[1]];

        historial.ComandoBorrarSeleccionados.Execute(null);

        Assert.Equal(2, historial.Filas.Count);
        Assert.Single(almacen.Historial.Jornadas[0].Trayectos);
        string copia = Assert.Single(Directory.GetFiles(rutas.CarpetaCopias, "historial-antes-de-borrar-*.json"));
        Assert.Contains("Zaragoza", File.ReadAllText(copia));
        Assert.StartsWith("Borrado.", historial.Mensaje);
    }

    [Fact]
    public void Si_el_usuario_dice_que_no_no_se_borra_nada()
    {
        dialogos.Respuesta = false;
        historial.FilasSeleccionadas = [historial.Filas[0]];

        historial.ComandoBorrarSeleccionados.Execute(null);

        Assert.Equal(4, historial.Filas.Count);
        Assert.False(Directory.Exists(rutas.CarpetaCopias) &&
                     Directory.GetFiles(rutas.CarpetaCopias, "historial-antes-de-borrar-*.json").Length > 0);
    }

    [Fact]
    public void La_jornada_abierta_no_se_puede_borrar()
    {
        historial.NodoSeleccionado = NodoJornada(2);

        Assert.False(historial.ComandoBorrarJornada.CanExecute(null));
    }

    [Fact]
    public void Borrar_una_jornada_cerrada_la_quita_del_historial()
    {
        historial.NodoSeleccionado = NodoJornada(1);

        historial.ComandoBorrarJornada.Execute(null);

        Assert.Equal([2], almacen.Historial.Jornadas.Select(j => j.Numero));
    }

    [Fact]
    public void Borrar_todo_conserva_la_jornada_abierta_sin_trayectos_terminados()
    {
        historial.ComandoBorrarTodo.Execute(null);

        Jornada abierta = Assert.Single(almacen.Historial.Jornadas);
        Assert.True(abierta.Abierta);
        Assert.Empty(abierta.Trayectos);
        Assert.Empty(historial.Filas);
    }

    [Fact]
    public void Exportar_sin_elegir_filas_exporta_lo_que_se_ve()
    {
        dialogos.RutaCsv = carpeta.Archivo("jornada1.csv");
        historial.NodoSeleccionado = NodoJornada(1);

        historial.ComandoExportar.Execute(null);

        string[] lineas = File.ReadAllLines(dialogos.RutaCsv);
        Assert.Equal(4, lineas.Length); // cabecera + 3 trayectos
        Assert.StartsWith("Exportados 3 trayectos", historial.Mensaje);
    }

    [Fact]
    public void Exportar_con_filas_elegidas_exporta_solo_esas()
    {
        dialogos.RutaCsv = carpeta.Archivo("elegidos.csv");
        historial.FilasSeleccionadas = [historial.Filas[3]];

        historial.ComandoExportar.Execute(null);

        string[] lineas = File.ReadAllLines(dialogos.RutaCsv);
        Assert.Equal(2, lineas.Length);
        Assert.Contains("Perpiñán", lineas[1]);
    }

    [Fact]
    public void Si_se_cancela_la_exportacion_no_se_escribe_nada()
    {
        dialogos.RutaCsv = null;

        historial.ComandoExportarTodo.Execute(null);

        Assert.Equal("", historial.Mensaje);
    }

    [Fact]
    public void Elegir_una_jornada_carga_su_recorrido_sin_resaltar_nada()
    {
        GuardarRecorrido(almacen.Historial.Jornadas[0].Id);

        historial.NodoSeleccionado = NodoJornada(1);

        Assert.NotNull(historial.Recorrido);
        Assert.Equal(2, historial.Recorrido!.Tramos[0].Count);
        Assert.Equal(-1, historial.ResaltadoDesde);
        Assert.Equal("", historial.TextoMapa);
    }

    [Fact]
    public void Elegir_un_trayecto_resalta_sus_minutos_de_la_jornada()
    {
        historial.NodoSeleccionado = NodoJornada(1);
        historial.FilasSeleccionadas = [historial.Filas[1]]; // Zaragoza → Barcelona: de 11:00 a 14:00, la jornada empieza a las 06:00

        Assert.Equal(5 * 60, historial.ResaltadoDesde);
        Assert.Equal(8 * 60, historial.ResaltadoHasta);
    }

    [Fact]
    public void Una_jornada_sin_recorrido_lo_explica()
    {
        historial.NodoSeleccionado = NodoJornada(1);

        Assert.StartsWith("Esta jornada no tiene recorrido", historial.TextoMapa);
    }

    [Fact]
    public void Sin_jornada_elegida_el_mapa_pide_elegir_una()
    {
        historial.NodoSeleccionado = historial.Arbol[0]; // Todo el historial

        Assert.Null(historial.Recorrido);
        Assert.StartsWith("Elige una jornada", historial.TextoMapa);
    }

    /// <summary>Guarda un recorrido de dos puntos para una jornada.</summary>
    private void GuardarRecorrido(Guid jornada)
    {
        Directory.CreateDirectory(rutas.CarpetaRecorridos);
        File.WriteAllText(rutas.Recorrido(jornada),
            "{\"Version\":1,\"Datos\":{\"Tramos\":[[[0,0,0],[1000,0,60]]]}}");
    }

    [Fact]
    public void Un_trayecto_nuevo_en_el_historial_aparece_solo()
    {
        almacen.Historial.Jornadas[1].Trayectos.Add(EjemplosHistorial.Trayecto(178, 10, 1, "Perpiñán", "Narbona", 65));
        almacen.MarcarCambios(ArchivosDatos.Historial);

        Assert.Equal(5, historial.Filas.Count);
    }
}
