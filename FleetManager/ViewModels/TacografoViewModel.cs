using System.Windows.Input;
using FleetManager.Core;
using FleetManager.Models;

namespace FleetManager.ViewModels;

/// <summary>
/// Todo lo que se muestra del tacógrafo. Hay <b>una sola instancia</b>, compartida
/// por la ventana principal y el mini tacógrafo: los textos y colores se calculan
/// una vez por lectura y las dos ventanas muestran siempre lo mismo.
/// </summary>
public sealed class TacografoViewModel : ObjetoObservable, IDisposable
{
    private readonly ServicioTacografo servicio;
    private readonly IDialogos dialogos;

    // ---------- Juego ----------
    private EstadoJuego estadoJuego;
    private bool juegoConectado;
    private string textoEstadoJuego = "";
    private string detalleJuego = "";
    private string horaJuego = "";
    private string velocidad = "";
    private string limiteVelocidad = "";
    private NivelVelocidad nivelVelocidad;

    // ---------- Jornada y actividad ----------
    private bool jornadaAbierta;
    private string textoJornada = "";
    private Actividad actividad;
    private string nombreActividad = "";
    private string tiempoActividad = "";
    private string actividadElegida = "";
    private string subtituloActividad = "";
    private string siguienteParada = "";

    // ---------- Pausa, plazos y faltas ----------
    private bool marcaPausa15;
    private bool marcaPausa30;
    private string textoPausa = "";
    private string plazoDescansoDiario = "";
    private string plazoDescansoSemanal = "";
    private string amplitud = "";
    private string ampliaciones = "";
    private string reducidos = "";
    private int faltasJornada;
    private IReadOnlyList<Aviso> avisos = [];
    private Aviso? avisoPrincipal;
    private IReadOnlyList<string> infracciones = [];
    private string trayecto = "";
    private string datosTrayecto = "";
    private bool hayTrayecto;

    public TacografoViewModel(ServicioTacografo servicio, IDialogos dialogos)
    {
        this.servicio = servicio;
        this.dialogos = dialogos;

        ComandoAbrirJornada = new Comando(() => servicio.AbrirJornada(), () => servicio.PuedeAbrirJornada);
        ComandoCerrarJornada = new Comando(CerrarJornada, () => servicio.PuedeCerrarJornada);
        ComandoDescanso = ComandoActividad(Actividad.Descanso);
        ComandoDisponibilidad = ComandoActividad(Actividad.Disponibilidad);
        ComandoOtrosTrabajos = ComandoActividad(Actividad.OtrosTrabajos);
        ComandoConduccion = ComandoActividad(Actividad.Conduccion);

        servicio.Actualizado += ServicioActualizado;
        Actualizar();
    }

    // ---------- Juego ----------

    public EstadoJuego EstadoJuego { get => estadoJuego; private set => Asignar(ref estadoJuego, value); }

    public bool JuegoConectado { get => juegoConectado; private set => Asignar(ref juegoConectado, value); }

    public string TextoEstadoJuego { get => textoEstadoJuego; private set => Asignar(ref textoEstadoJuego, value); }

    public string DetalleJuego { get => detalleJuego; private set => Asignar(ref detalleJuego, value); }

    public string HoraJuego { get => horaJuego; private set => Asignar(ref horaJuego, value); }

    /// <summary>Solo el número ("83"), para mostrarlo grande.</summary>
    public string Velocidad { get => velocidad; private set => Asignar(ref velocidad, value); }

    /// <summary>"límite 80", o vacío si no hay límite.</summary>
    public string LimiteVelocidad { get => limiteVelocidad; private set => Asignar(ref limiteVelocidad, value); }

    public NivelVelocidad NivelVelocidad { get => nivelVelocidad; private set => Asignar(ref nivelVelocidad, value); }

    // ---------- Jornada y actividad ----------

    public bool JornadaAbierta { get => jornadaAbierta; private set => Asignar(ref jornadaAbierta, value); }

    public string TextoJornada { get => textoJornada; private set => Asignar(ref textoJornada, value); }

    public Actividad Actividad { get => actividad; private set => Asignar(ref actividad, value); }

    public string NombreActividad { get => nombreActividad; private set => Asignar(ref nombreActividad, value); }

    public string TiempoActividad { get => tiempoActividad; private set => Asignar(ref tiempoActividad, value); }

    /// <summary>"Botón: Descanso" si hay una actividad elegida a mano, o vacío.</summary>
    public string ActividadElegida { get => actividadElegida; private set => Asignar(ref actividadElegida, value); }

    /// <summary>Segunda línea de la franja: la jornada y el botón elegido, o qué hacer para empezar.</summary>
    public string SubtituloActividad { get => subtituloActividad; private set => Asignar(ref subtituloActividad, value); }

    public string SiguienteParada { get => siguienteParada; private set => Asignar(ref siguienteParada, value); }

    // ---------- Barras ----------

    public IndicadorTiempo Continua { get; } = new("Conducción continua");

    public IndicadorTiempo Diaria { get; } = new("Conducción diaria");

    public IndicadorTiempo Semanal { get; } = new("Conducción semanal");

    public IndicadorTiempo Bisemanal { get; } = new("Conducción bisemanal");

    public IndicadorTiempo DescansoDiario { get; } = new("Descanso diario");

    // ---------- Pausa, plazos y faltas ----------

    /// <summary>Hecha la primera parte de la pausa (15 min).</summary>
    public bool MarcaPausa15 { get => marcaPausa15; private set => Asignar(ref marcaPausa15, value); }

    /// <summary>Hecha la pausa completa (45 min, o la segunda parte de 30 min).</summary>
    public bool MarcaPausa30 { get => marcaPausa30; private set => Asignar(ref marcaPausa30, value); }

    public string TextoPausa { get => textoPausa; private set => Asignar(ref textoPausa, value); }

    public string PlazoDescansoDiario { get => plazoDescansoDiario; private set => Asignar(ref plazoDescansoDiario, value); }

    public string PlazoDescansoSemanal { get => plazoDescansoSemanal; private set => Asignar(ref plazoDescansoSemanal, value); }

    public string Amplitud { get => amplitud; private set => Asignar(ref amplitud, value); }

    public string Ampliaciones { get => ampliaciones; private set => Asignar(ref ampliaciones, value); }

    public string Reducidos { get => reducidos; private set => Asignar(ref reducidos, value); }

    /// <summary>Faltas (velocidad y conducción) en los trayectos de la jornada abierta.</summary>
    public int FaltasJornada { get => faltasJornada; private set => Asignar(ref faltasJornada, value); }

    public IReadOnlyList<Aviso> Avisos { get => avisos; private set => Asignar(ref avisos, value); }

    /// <summary>El aviso más urgente (para el mini tacógrafo), o vacío.</summary>
    public Aviso? AvisoPrincipal { get => avisoPrincipal; private set => Asignar(ref avisoPrincipal, value); }

    /// <summary>Infracciones del registro, de la más reciente a la más antigua.</summary>
    public IReadOnlyList<string> Infracciones { get => infracciones; private set => Asignar(ref infracciones, value); }

    // ---------- Trayecto ----------

    public bool HayTrayecto { get => hayTrayecto; private set => Asignar(ref hayTrayecto, value); }

    /// <summary>"Madrid → Zaragoza · Maquinaria".</summary>
    public string Trayecto { get => trayecto; private set => Asignar(ref trayecto, value); }

    /// <summary>Kilómetros, velocidades y faltas del trayecto.</summary>
    public string DatosTrayecto { get => datosTrayecto; private set => Asignar(ref datosTrayecto, value); }

    // ---------- Botones ----------

    public ICommand ComandoAbrirJornada { get; }

    public ICommand ComandoCerrarJornada { get; }

    public ICommand ComandoDescanso { get; }

    public ICommand ComandoDisponibilidad { get; }

    public ICommand ComandoOtrosTrabajos { get; }

    public ICommand ComandoConduccion { get; }

    public void Dispose() => servicio.Actualizado -= ServicioActualizado;

    private Comando ComandoActividad(Actividad elegida) =>
        new(() => servicio.ElegirActividad(elegida), () => servicio.JornadaAbierta is not null);

    private void CerrarJornada()
    {
        if (dialogos.Confirmar("¿Cerrar la jornada? El trayecto en curso pasará al historial."))
        {
            servicio.CerrarJornada();
        }
    }

    private void ServicioActualizado(object? sender, EventArgs e) => Actualizar();

    private void Actualizar()
    {
        DatosJuego datos = servicio.Datos;
        EstadoTacografo estado = servicio.Estado;
        Jornada? jornada = servicio.JornadaAbierta;
        bool hayRegistro = estado.Ahora != default;

        bool botonesCambiados = false;

        // Juego
        EstadoJuego = datos.Estado;
        botonesCambiados |= Asignar(ref juegoConectado, datos.Conectado, nameof(JuegoConectado));
        TextoEstadoJuego = datos.Estado switch
        {
            EstadoJuego.Detectado => datos.Pausado ? "Juego en pausa" : "Juego detectado",
            EstadoJuego.JuegoNoCompatible => "Juego no compatible",
            EstadoJuego.NoResponde => "El juego no responde",
            EstadoJuego.Error => "Error del plugin",
            _ => "Juego no detectado"
        };
        DetalleJuego = datos.Estado switch
        {
            EstadoJuego.Detectado => $"Plugin {datos.VersionPlugin}",
            EstadoJuego.JuegoNoCompatible => "El juego no es ETS2",
            EstadoJuego.NoResponde => "¿Colgado o cerrado de golpe?",
            EstadoJuego.Error => datos.Error ?? "",
            _ => "Abre ETS2 con el plugin"
        };
        HoraJuego = datos.Conectado ? Formato.HoraJuego(datos.HoraJuego) : "—";
        Velocidad = datos.Conectado ? datos.Velocidad.ToString("0") : "—";
        LimiteVelocidad = datos.Conectado && datos.LimiteVelocidad > 0 ? $"límite {datos.LimiteVelocidad:0}" : "";
        NivelVelocidad = servicio.NivelVelocidad;

        // Jornada y actividad
        botonesCambiados |= Asignar(ref jornadaAbierta, jornada is not null, nameof(JornadaAbierta));
        TextoJornada = jornada is null ? "Sin jornada" : $"Jornada {jornada.Numero}";
        Actividad = jornada is null ? Actividad.Descanso : estado.ActividadActual;
        NombreActividad = jornada is null ? "SIN JORNADA" : Formato.Actividad(estado.ActividadActual).ToUpperInvariant();
        TiempoActividad = jornada is null || !hayRegistro ? "" : Formato.Duracion(estado.TiempoActividadActual);
        ActividadElegida = servicio.ActividadManual is { } manual ? $"Botón: {Formato.Actividad(manual)}" : "";
        SubtituloActividad = jornada is null
            ? "Pulsa «Abrir jornada» para empezar"
            : ActividadElegida.Length > 0 ? $"{TextoJornada}  ·  {ActividadElegida}" : TextoJornada;
        SiguienteParada = Formato.Duracion(estado.SiguienteParada);

        // Barras
        Continua.ActualizarLimite(estado.ConduccionContinua, ReglasUE.ConduccionContinuaMaxima, estado.ConduccionContinuaRestante);
        Diaria.ActualizarLimite(estado.ConduccionDiaria, estado.LimiteConduccionDiaria, estado.ConduccionDiariaRestante);
        Semanal.ActualizarLimite(estado.ConduccionSemanal, ReglasUE.ConduccionSemanalMaxima, estado.ConduccionSemanalRestante);
        Bisemanal.ActualizarLimite(estado.ConduccionBisemanal, ReglasUE.ConduccionBisemanalMaxima, estado.ConduccionBisemanalRestante);
        DescansoDiario.ActualizarObjetivo(
            estado.DescansoActual,
            estado.DescansoActual + estado.DescansoDiarioRestante,
            estado.DescansoDiarioRestante);

        // Pausa
        bool descansando = estado.ActividadActual == Actividad.Descanso && estado.DescansoActual > TimeSpan.Zero;
        MarcaPausa15 = estado.PrimeraPartePausaHecha || (descansando && estado.DescansoActual >= ReglasUE.PausaPrimeraParte);
        MarcaPausa30 = descansando && estado.PausaRestante == TimeSpan.Zero;
        TextoPausa = descansando
            ? estado.PausaRestante > TimeSpan.Zero ? $"En pausa · faltan {Formato.Duracion(estado.PausaRestante)}" : "Pausa cumplida"
            : estado.PrimeraPartePausaHecha ? "Hecha la parte de 15 min · faltan 30" : "Pendiente: 45 min (o 15 + 30)";

        // Plazos y contadores
        PlazoDescansoDiario = hayRegistro ? Formato.HoraJuego(estado.LimiteInicioDescansoDiario) : "—";
        PlazoDescansoSemanal = !hayRegistro ? "—"
            : estado.DescansoSemanalEnCurso ? "en curso"
            : Formato.HoraJuego(estado.PlazoDescansoSemanal);
        Amplitud = Formato.Duracion(estado.AmplitudJornada);
        Ampliaciones = $"{estado.AmpliacionesRestantes} de {ReglasUE.AmpliacionesPorSemana}";
        Reducidos = $"{estado.DescansosReducidosRestantes} de {ReglasUE.DescansosReducidosMaximos}";

        // Avisos e infracciones (las listas solo se sustituyen si cambian, para no redibujarlas)
        IReadOnlyList<Aviso> nuevosAvisos = GeneradorAvisos.Calcular(estado, jornada is not null);

        if (!nuevosAvisos.SequenceEqual(Avisos))
        {
            Avisos = nuevosAvisos;
            AvisoPrincipal = nuevosAvisos.Count > 0 ? nuevosAvisos[0] : null;
        }

        if (estado.Infracciones.Count != Infracciones.Count)
        {
            Infracciones = estado.Infracciones
                .Reverse()
                .Select(i => $"{Formato.HoraJuego(i.Momento)} · {i.Descripcion}")
                .ToList();
        }

        // Trayecto y faltas
        Trayecto? enCurso = jornada?.TrayectoEnCurso;
        HayTrayecto = enCurso is not null;
        Trayecto = enCurso is null ? "Sin trayecto en curso" : $"{Texto(enCurso.Origen)} → {Texto(enCurso.Destino)} · {Texto(enCurso.Carga)}";
        DatosTrayecto = enCurso is null
            ? ""
            : $"{Formato.Kilometros(enCurso.Kilometros)} · media {Formato.Velocidad(enCurso.VelocidadMedia)} · máx. {Formato.Velocidad(enCurso.VelocidadMaxima)}" +
              (enCurso.FaltaVelocidad ? " · falta de velocidad" : "") +
              (enCurso.FaltaConduccion ? " · falta de conducción" : "");
        FaltasJornada = jornada is null
            ? 0
            : jornada.Trayectos.Append(enCurso).OfType<Trayecto>()
                .Sum(t => (t.FaltaVelocidad ? 1 : 0) + (t.FaltaConduccion ? 1 : 0));

        if (botonesCambiados)
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private static string Texto(string valor) => valor.Length > 0 ? valor : "?";
}
