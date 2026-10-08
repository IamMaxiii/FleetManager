using System.Collections.ObjectModel;
using FleetManager.Core;
using FleetManager.Idiomas;
using FleetManager.Models;

namespace FleetManager.ViewModels;

public enum TipoNodo
{
    Todo,
    Jornada,
    Dia,
    Trayecto
}

/// <summary>
/// Un elemento del árbol del historial: "Todo el historial", una jornada, un día o un trayecto.
/// </summary>
public sealed class NodoHistorial : ObjetoObservable
{
    private bool expandido;
    private bool seleccionado;

    public NodoHistorial(
        TipoNodo tipo,
        string clave,
        string titulo,
        string detalle,
        IReadOnlyList<(Jornada Jornada, Trayecto Trayecto)> filas,
        Jornada? jornada = null,
        Trayecto? trayecto = null)
    {
        Tipo = tipo;
        Clave = clave;
        Titulo = titulo;
        Detalle = detalle;
        Filas = filas;
        Jornada = jornada;
        Trayecto = trayecto;
    }

    public TipoNodo Tipo { get; }

    /// <summary>Identifica el nodo para volver a seleccionarlo al rehacer el árbol.</summary>
    public string Clave { get; }

    public string Titulo { get; }

    public string Detalle { get; }

    /// <summary>Trayectos que se muestran en la tabla al elegir este nodo.</summary>
    public IReadOnlyList<(Jornada Jornada, Trayecto Trayecto)> Filas { get; }

    public Jornada? Jornada { get; }

    public Trayecto? Trayecto { get; }

    public NodoHistorial? Padre { get; private set; }

    public ObservableCollection<NodoHistorial> Hijos { get; } = [];

    public bool Expandido { get => expandido; set => Asignar(ref expandido, value); }

    public bool Seleccionado { get => seleccionado; set => Asignar(ref seleccionado, value); }

    public void AnadirHijo(NodoHistorial hijo)
    {
        hijo.Padre = this;
        Hijos.Add(hijo);
    }
}

/// <summary>
/// Una fila de la tabla de trayectos. Guarda los números como números para que la
/// tabla ordene bien por kilómetros o velocidades.
/// </summary>
public sealed class FilaTrayecto
{
    public FilaTrayecto(Jornada jornada, Trayecto trayecto)
    {
        Jornada = jornada;
        Trayecto = trayecto;
    }

    public Jornada Jornada { get; }

    public Trayecto Trayecto { get; }

    public int NumeroJornada => Jornada.Numero;

    public string Dia => FechaJuego.TextoDia(Trayecto.Inicio);

    /// <summary>Para ordenar por fecha y hora.</summary>
    public DateTime Inicio => Trayecto.Inicio;

    public string HoraInicio => FechaJuego.TextoHora(Trayecto.Inicio);

    /// <summary>La hora de fin; con el día delante si no es el mismo que el de inicio.</summary>
    public string HoraFin => Trayecto.Fin.Date == Trayecto.Inicio.Date
        ? FechaJuego.TextoHora(Trayecto.Fin)
        : FechaJuego.TextoDiaYHora(Trayecto.Fin);

    public string Origen => Trayecto.Origen;

    public string Destino => Trayecto.Destino;

    public string Carga => Trayecto.Carga;

    public double Kilometros => Trayecto.Kilometros;

    public double VelocidadMedia => Trayecto.VelocidadMedia;

    public double VelocidadMaxima => Trayecto.VelocidadMaxima;

    public string Faltas => (Trayecto.FaltaVelocidad, Trayecto.FaltaConduccion) switch
    {
        (true, true) => Textos.T("Faltas.Ambas"),
        (true, false) => Textos.T("Faltas.Velocidad"),
        (false, true) => Textos.T("Faltas.Conduccion"),
        _ => ""
    };
}
