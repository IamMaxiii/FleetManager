using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows.Input;
using FleetManager.Core;
using FleetManager.Idiomas;
using FleetManager.Models;
using FleetManager.Storage;

namespace FleetManager.ViewModels;

/// <summary>
/// Sección "Historial": árbol de jornadas → días → trayectos, tabla de trayectos,
/// edición, borrado (siempre con copia de seguridad antes) y exportación a CSV.
///
/// Solo muestra trayectos terminados: el trayecto en curso no aparece hasta que
/// termina, y la jornada abierta no se puede editar ni borrar.
/// </summary>
public sealed class HistorialViewModel : ObjetoObservable, IDisposable
{
    private const string ClaveTodo = "todo";

    private readonly AlmacenDatos almacen;
    private readonly IDialogos dialogos;

    private NodoHistorial? nodoSeleccionado;
    private IReadOnlyList<FilaTrayecto> filasSeleccionadas = [];
    private ObjetoObservable? edicion;
    private string resumen = "";
    private string mensaje = "";
    private string firma = "";
    private bool verMapa;
    private MapaRecorrido? recorrido;
    private int resaltadoDesde = -1;
    private int resaltadoHasta = -1;
    private string textoMapa = "";

    public HistorialViewModel(AlmacenDatos almacen, IDialogos dialogos, MapaJuegoViewModel mapaJuego)
    {
        this.almacen = almacen;
        this.dialogos = dialogos;
        MapaJuego = mapaJuego;

        ComandoExportar = new Comando(ExportarVista);
        ComandoExportarTodo = new Comando(() => Exportar(TodasLasFilas(), "FleetManager-historial.csv"));
        ComandoBorrarSeleccionados = new Comando(BorrarSeleccionados, () => FilasSeleccionadas.Count > 0);
        ComandoBorrarJornada = new Comando(BorrarJornada, () => NodoSeleccionado is { Tipo: TipoNodo.Jornada, Jornada.Abierta: false });
        ComandoBorrarTodo = new Comando(BorrarTodo, () => TodasLasFilas().Count > 0 || almacen.Historial.Jornadas.Any(j => !j.Abierta));

        almacen.EstadoCambiado += AlmacenEstadoCambiado;
        Refrescar();
    }

    public ObservableCollection<NodoHistorial> Arbol { get; } = [];

    public ObservableCollection<FilaTrayecto> Filas { get; } = [];

    /// <summary>Lo elegido en el árbol (lo pone la vista).</summary>
    public NodoHistorial? NodoSeleccionado
    {
        get => nodoSeleccionado;
        set
        {
            if (Asignar(ref nodoSeleccionado, value))
            {
                MostrarFilas();
                ActualizarEdicion();
                ActualizarMapa();
            }
        }
    }

    /// <summary>Filas elegidas en la tabla (lo pone la vista).</summary>
    public IReadOnlyList<FilaTrayecto> FilasSeleccionadas
    {
        get => filasSeleccionadas;
        set
        {
            filasSeleccionadas = value;
            Avisar(nameof(FilasSeleccionadas));
            ActualizarEdicion();
            ActualizarMapa();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>Panel de edición: de un trayecto, de una jornada, o nada.</summary>
    public ObjetoObservable? Edicion { get => edicion; private set => Asignar(ref edicion, value); }

    /// <summary>Totales de lo que se ve en la tabla.</summary>
    public string Resumen { get => resumen; private set => Asignar(ref resumen, value); }

    /// <summary>Resultado de la última acción (exportar, borrar, guardar...).</summary>
    public string Mensaje { get => mensaje; private set => Asignar(ref mensaje, value); }

    // ---------- Mapa ----------

    /// <summary>Mapa del juego (teselas y ciudades), compartido con la página de inicio.</summary>
    public MapaJuegoViewModel MapaJuego { get; }

    /// <summary>Se ve el mapa en lugar de la tabla.</summary>
    public bool VerMapa { get => verMapa; set => Asignar(ref verMapa, value); }

    /// <summary>Recorrido de la jornada elegida; vacío si no hay jornada elegida.</summary>
    public MapaRecorrido? Recorrido { get => recorrido; private set => Asignar(ref recorrido, value); }

    /// <summary>Minutos de la jornada a resaltar (día o trayecto elegido); -1 = toda la jornada.</summary>
    public int ResaltadoDesde { get => resaltadoDesde; private set => Asignar(ref resaltadoDesde, value); }

    public int ResaltadoHasta { get => resaltadoHasta; private set => Asignar(ref resaltadoHasta, value); }

    /// <summary>Explicación encima del mapa cuando no hay nada que enseñar.</summary>
    public string TextoMapa { get => textoMapa; private set => Asignar(ref textoMapa, value); }

    public ICommand ComandoExportar { get; }

    public ICommand ComandoExportarTodo { get; }

    public ICommand ComandoBorrarSeleccionados { get; }

    public ICommand ComandoBorrarJornada { get; }

    public ICommand ComandoBorrarTodo { get; }

    private Historial Historial => almacen.Historial;

    public void Dispose() => almacen.EstadoCambiado -= AlmacenEstadoCambiado;

    /// <summary>Rehace el árbol, conservando lo que estaba elegido si sigue existiendo.</summary>
    public void Refrescar()
    {
        firma = CalcularFirma();
        string claveElegida = NodoSeleccionado?.Clave ?? ClaveTodo;

        Arbol.Clear();
        var todo = new NodoHistorial(TipoNodo.Todo, ClaveTodo, Textos.T("Historial.Todo"), TextoResumenCorto(TodasLasFilas()), TodasLasFilas());
        Arbol.Add(todo);

        foreach (Jornada jornada in Historial.Jornadas.OrderByDescending(j => j.Numero))
        {
            Arbol.Add(CrearNodoJornada(jornada));
        }

        NodoHistorial elegido = Buscar(Arbol, claveElegida) ?? todo;

        for (NodoHistorial? padre = elegido.Padre; padre is not null; padre = padre.Padre)
        {
            padre.Expandido = true;
        }

        elegido.Seleccionado = true;
        nodoSeleccionado = null; // fuerza a recalcular la tabla aunque sea "el mismo" nodo
        NodoSeleccionado = elegido;
    }

    private NodoHistorial CrearNodoJornada(Jornada jornada)
    {
        var filas = jornada.Trayectos.OrderBy(t => t.Inicio).Select(t => (jornada, t)).ToList();
        var nodo = new NodoHistorial(
            TipoNodo.Jornada,
            $"j:{jornada.Id}",
            jornada.Abierta ? Textos.T("Historial.JornadaEnCurso", jornada.Numero) : Textos.T("Tarjeta.Jornada", jornada.Numero),
            $"{FechaJuego.TextoDia(jornada.Inicio)} · {TextoResumenCorto(filas)}",
            filas,
            jornada);

        foreach (DiaHistorial dia in HistorialConsultas.Dias(jornada))
        {
            var filasDia = dia.Trayectos.Select(t => (jornada, t)).ToList();
            var nodoDia = new NodoHistorial(
                TipoNodo.Dia,
                $"d:{jornada.Id}:{dia.Fecha.Ticks}",
                FechaJuego.TextoDia(dia.Fecha),
                TextoResumenCorto(filasDia),
                filasDia,
                jornada);

            foreach (Trayecto trayecto in dia.Trayectos)
            {
                nodoDia.AnadirHijo(new NodoHistorial(
                    TipoNodo.Trayecto,
                    $"t:{trayecto.Id}",
                    $"{Texto(trayecto.Origen)} → {Texto(trayecto.Destino)}",
                    $"{FechaJuego.TextoHora(trayecto.Inicio)} · {Formato.Kilometros(trayecto.Kilometros)}",
                    [(jornada, trayecto)],
                    jornada,
                    trayecto));
            }

            nodo.AnadirHijo(nodoDia);
        }

        return nodo;
    }

    private void MostrarFilas()
    {
        IReadOnlyList<(Jornada Jornada, Trayecto Trayecto)> filas = NodoSeleccionado?.Filas ?? TodasLasFilas();

        Filas.Clear();

        foreach (var (jornada, trayecto) in filas)
        {
            Filas.Add(new FilaTrayecto(jornada, trayecto));
        }

        filasSeleccionadas = [];
        Resumen = TextoResumen(filas);
        CommandManager.InvalidateRequerySuggested();
    }

    private void ActualizarEdicion()
    {
        if (FilasSeleccionadas.Count == 1)
        {
            FilaTrayecto fila = FilasSeleccionadas[0];
            Edicion = new EdicionTrayectoViewModel(fila.Jornada, fila.Trayecto, AlGuardarEdicion);
        }
        else if (FilasSeleccionadas.Count == 0 && NodoSeleccionado is { Tipo: TipoNodo.Trayecto, Jornada: { } j, Trayecto: { } t })
        {
            Edicion = new EdicionTrayectoViewModel(j, t, AlGuardarEdicion);
        }
        else if (FilasSeleccionadas.Count == 0 && NodoSeleccionado is { Tipo: TipoNodo.Jornada, Jornada: { } jornada })
        {
            Edicion = new EdicionJornadaViewModel(jornada, AlGuardarEdicion);
        }
        else
        {
            Edicion = null;
        }
    }

    /// <summary>
    /// Recorrido de la jornada elegida y qué parte resaltar: una fila de la tabla (su
    /// trayecto), un trayecto o un día del árbol; con la jornada entera, nada resaltado.
    /// </summary>
    private void ActualizarMapa()
    {
        IReadOnlyList<(Jornada Jornada, Trayecto Trayecto)> elegidos = FilasSeleccionadas.Count > 0
            ? FilasSeleccionadas.Select(f => (f.Jornada, f.Trayecto)).ToList()
            : NodoSeleccionado is { Tipo: TipoNodo.Dia or TipoNodo.Trayecto } nodo ? nodo.Filas : [];

        Jornada? jornada = elegidos.Count > 0 ? elegidos[0].Jornada : NodoSeleccionado?.Jornada;

        if (jornada is null)
        {
            Recorrido = null;
            ResaltadoDesde = ResaltadoHasta = -1;
            TextoMapa = Textos.T("Historial.MapaElige");
            return;
        }

        Recorrido = almacen.LeerRecorrido(jornada.Id);
        TextoMapa = Recorrido.Tramos.Count == 0
            ? Textos.T("Historial.MapaSinRecorrido")
            : "";

        var mismaJornada = elegidos.Where(e => e.Jornada == jornada).Select(e => e.Trayecto).ToList();

        if (mismaJornada.Count == 0)
        {
            ResaltadoDesde = ResaltadoHasta = -1;
            return;
        }

        ResaltadoDesde = MinutoDeJornada(jornada, mismaJornada.Min(t => t.Inicio));
        ResaltadoHasta = MinutoDeJornada(jornada, mismaJornada.Max(t => t.Fin));
    }

    private static int MinutoDeJornada(Jornada jornada, DateTime momento) =>
        (int)Math.Max(0, (momento - jornada.Inicio).TotalMinutes);

    private void AlGuardarEdicion()
    {
        GuardarAhora();
        Mensaje = Textos.T("Historial.CambiosGuardados");
        Refrescar();
    }

    // ---------- Exportar ----------

    private void ExportarVista()
    {
        IReadOnlyList<(Jornada, Trayecto)> filas = FilasSeleccionadas.Count > 0
            ? FilasSeleccionadas.Select(f => (f.Jornada, f.Trayecto)).ToList()
            : Filas.Select(f => (f.Jornada, f.Trayecto)).ToList();

        string nombre = NodoSeleccionado is { Tipo: TipoNodo.Jornada or TipoNodo.Dia, Jornada: { } jornada } && FilasSeleccionadas.Count == 0
            ? Textos.T("Csv.NombreJornada", jornada.Numero)
            : Textos.T("Csv.NombreTrayectos");

        Exportar(filas, nombre);
    }

    private void Exportar(IReadOnlyList<(Jornada, Trayecto)> filas, string nombreSugerido)
    {
        if (filas.Count == 0)
        {
            Mensaje = Textos.T("Historial.NadaQueExportar");
            return;
        }

        if (dialogos.ElegirArchivoCsv(nombreSugerido) is not { } ruta)
        {
            return;
        }

        try
        {
            ExportadorCsv.Guardar(ruta, ExportadorCsv.Generar(filas.OrderBy(f => f.Item2.Inicio)));
            Mensaje = Textos.T("Historial.Exportados", Textos.Plural("Plural.Trayectos", filas.Count), ruta);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Mensaje = Textos.T("Historial.NoGuardado", ex.Message);
        }
    }

    // ---------- Borrar ----------

    private void BorrarSeleccionados()
    {
        var elegidos = FilasSeleccionadas.ToList();

        if (elegidos.Count == 0 ||
            !dialogos.Confirmar(Textos.T("Historial.ConfirmarBorrarTrayectos", Textos.Plural("Plural.Trayectos", elegidos.Count))))
        {
            return;
        }

        Borrar(() =>
        {
            foreach (FilaTrayecto fila in elegidos)
            {
                fila.Jornada.Trayectos.Remove(fila.Trayecto);
            }
        });
    }

    private void BorrarJornada()
    {
        if (NodoSeleccionado is not { Tipo: TipoNodo.Jornada, Jornada: { Abierta: false } jornada } ||
            !dialogos.Confirmar(Textos.T("Historial.ConfirmarBorrarJornada", jornada.Numero)))
        {
            return;
        }

        Borrar(() => Historial.Jornadas.Remove(jornada));
    }

    private void BorrarTodo()
    {
        if (!dialogos.Confirmar(Textos.T("Historial.ConfirmarBorrarTodo")))
        {
            return;
        }

        Borrar(() =>
        {
            Historial.Jornadas.RemoveAll(j => !j.Abierta);

            foreach (Jornada abierta in Historial.Jornadas)
            {
                abierta.Trayectos.Clear();
            }
        });
    }

    /// <summary>Hace la copia de seguridad y, solo si ha salido bien, borra y guarda.</summary>
    private void Borrar(Action borrado)
    {
        if (almacen.CopiarHistorial("antes-de-borrar") is not { } copia)
        {
            Mensaje = Textos.T("Historial.SinCopia");
            return;
        }

        borrado();
        GuardarAhora();
        Mensaje = Textos.T("Historial.Borrado", copia);
        Refrescar();
    }

    // ---------- Utilidades ----------

    private void GuardarAhora()
    {
        almacen.MarcarCambios(ArchivosDatos.Historial);
        almacen.GuardarCambiosPendientes();
    }

    private IReadOnlyList<(Jornada Jornada, Trayecto Trayecto)> TodasLasFilas() =>
        Historial.Jornadas
            .SelectMany(j => j.Trayectos.Select(t => (j, t)))
            .OrderBy(f => f.t.Inicio)
            .ToList();

    /// <summary>
    /// Cambia cuando cambia algo que se ve en el historial (jornadas, sus trayectos
    /// terminados o su cierre), pero no con cada lectura del juego.
    /// </summary>
    private string CalcularFirma()
    {
        var texto = new StringBuilder();

        foreach (Jornada jornada in Historial.Jornadas)
        {
            texto.Append(jornada.Id).Append(jornada.Fin?.Ticks).Append(jornada.Trayectos.Count).Append('|');
        }

        return texto.ToString();
    }

    private void AlmacenEstadoCambiado(object? sender, EventArgs e)
    {
        if (CalcularFirma() != firma)
        {
            Refrescar();
        }
    }

    private static NodoHistorial? Buscar(IEnumerable<NodoHistorial> nodos, string clave)
    {
        foreach (NodoHistorial nodo in nodos)
        {
            if (nodo.Clave == clave)
            {
                return nodo;
            }

            if (Buscar(nodo.Hijos, clave) is { } encontrado)
            {
                return encontrado;
            }
        }

        return null;
    }

    private static string TextoResumen(IEnumerable<(Jornada Jornada, Trayecto Trayecto)> filas)
    {
        ResumenTrayectos resumen = HistorialConsultas.Resumir(filas.Select(f => f.Trayecto));

        return Textos.T("Historial.Resumen", Textos.Plural("Plural.Trayectos", resumen.Trayectos), Formato.Kilometros(resumen.Kilometros), Formato.Duracion(resumen.TiempoConduccion)) +
               (resumen.Faltas > 0 ? $" · {Textos.Plural("Plural.Faltas", resumen.Faltas)}" : "");
    }

    /// <summary>Versión corta para el árbol: trayectos, kilómetros y faltas.</summary>
    private static string TextoResumenCorto(IEnumerable<(Jornada Jornada, Trayecto Trayecto)> filas)
    {
        ResumenTrayectos resumen = HistorialConsultas.Resumir(filas.Select(f => f.Trayecto));

        return $"{Textos.Plural("Plural.Trayectos", resumen.Trayectos)} · {Formato.Kilometros(resumen.Kilometros)}" +
               (resumen.Faltas > 0 ? $" · {Textos.Plural("Plural.Faltas", resumen.Faltas)}" : "");
    }

    private static string Texto(string valor) => valor.Length > 0 ? valor : "?";

}
