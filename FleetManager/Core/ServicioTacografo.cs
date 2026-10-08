using FleetManager.Models;
using FleetManager.Storage;

namespace FleetManager.Core;

/// <summary>
/// Pieza central de la aplicación: una sola instancia, compartida por todas las
/// ventanas. Recibe cada lectura del juego (unas 4 por segundo) y:
///
/// - Decide la actividad (<see cref="SelectorActividad"/>) y la apunta en el registro
///   del tacógrafo, solo con la jornada abierta. El tiempo con la jornada cerrada
///   queda como hueco y, al volver a abrirla, cuenta como descanso.
/// - Recalcula el estado del tacógrafo (<see cref="AnalizadorTacografo"/>).
/// - Lleva el trayecto en curso: empieza al echar a rodar y termina al completar una
///   pausa válida o al cerrar la jornada. Marca las faltas de velocidad y de conducción.
/// - Suma los kilómetros de la tarjeta del conductor.
/// - Avisa a las ventanas con <see cref="Actualizado"/> una vez por lectura.
///
/// Se usa desde el hilo de la interfaz, igual que <see cref="AlmacenDatos"/>.
/// </summary>
public sealed class ServicioTacografo
{
    /// <summary>Trayectos más cortos que esto (maniobras) no se guardan.</summary>
    public const double KilometrosMinimosTrayecto = 0.1;

    /// <summary>Saltos del odómetro mayores que esto entre dos lecturas (teletransporte, cambio de partida) no suman.</summary>
    private const double SaltoMaximoOdometro = 1.0;

    private readonly AlmacenDatos almacen;
    private readonly SelectorActividad selector;
    private readonly VigilanteVelocidad vigilante;
    private readonly RegistradorActividad registrador;
    private RegistradorMapa registradorMapa;

    private DateTime? ultimaHoraJuego;
    private double? ultimoOdometro;

    public ServicioTacografo(AlmacenDatos almacen, TimeProvider reloj)
    {
        this.almacen = almacen;
        selector = new SelectorActividad(reloj);
        vigilante = new VigilanteVelocidad(reloj);
        registrador = new RegistradorActividad(almacen.Tacografo);
        Estado = AnalizadorTacografo.Analizar(almacen.Tacografo);

        // El mapa muestra el recorrido de la jornada abierta o, si no hay, el de la última.
        Jornada? jornada = JornadaAbierta ?? almacen.Historial.Jornadas.MaxBy(j => j.Numero);

        if (jornada is not null)
        {
            almacen.UsarRecorrido(jornada.Id);
        }

        registradorMapa = new RegistradorMapa(almacen.Recorrido);
    }

    /// <summary>Se lanza después de procesar cada lectura o de cualquier cambio (abrir jornada, botones...).</summary>
    public event EventHandler? Actualizado;

    /// <summary>Última lectura del juego.</summary>
    public DatosJuego Datos { get; private set; } = DatosJuego.SinConexion;

    public EstadoTacografo Estado { get; private set; }

    /// <summary>Actividad que se registró en la última lectura con la jornada abierta.</summary>
    public Actividad ActividadRegistrada { get; private set; } = Actividad.Descanso;

    public Actividad? ActividadManual => selector.ActividadManual;

    public NivelVelocidad NivelVelocidad => vigilante.Nivel;

    public Jornada? JornadaAbierta => almacen.Historial.Jornadas.LastOrDefault(j => j.Abierta);

    public Trayecto? TrayectoEnCurso => JornadaAbierta?.TrayectoEnCurso;

    public PerfilConductor Perfil => almacen.Perfil;

    /// <summary>Recorrido de la jornada abierta (o de la última, si no hay ninguna abierta).</summary>
    public MapaRecorrido Recorrido => almacen.Recorrido;

    /// <summary>Aumenta cada vez que cambia el recorrido (para saber cuándo redibujarlo).</summary>
    public int VersionRecorrido { get; private set; }

    public bool PuedeAbrirJornada => JornadaAbierta is null && Datos.Conectado;

    public bool PuedeCerrarJornada => JornadaAbierta is not null;

    public void ProcesarLectura(DatosJuego datos)
    {
        Datos = datos;

        if (datos.Conectado && !datos.Pausado && JornadaAbierta is { } jornada)
        {
            ProcesarJornada(jornada, datos);
            RegistrarRecorrido(jornada, datos);
        }
        else if (!datos.Conectado || datos.Pausado)
        {
            vigilante.Reiniciar();
        }

        Actualizado?.Invoke(this, EventArgs.Empty);
    }

    public bool AbrirJornada()
    {
        if (!PuedeAbrirJornada)
        {
            return false;
        }

        var jornada = new Jornada
        {
            Numero = almacen.Historial.SiguienteNumeroJornada(),
            Inicio = Datos.HoraJuego
        };

        almacen.Historial.Jornadas.Add(jornada);

        // Jornada nueva, recorrido nuevo: el mapa de inicio empieza en blanco.
        almacen.UsarRecorrido(jornada.Id);
        registradorMapa = new RegistradorMapa(almacen.Recorrido);
        VersionRecorrido++;

        selector.Reiniciar();
        vigilante.Reiniciar();
        ultimaHoraJuego = null;
        ultimoOdometro = null;

        // El registro empieza en el mismo momento de abrir la jornada, no en la lectura siguiente.
        if (!Datos.Pausado)
        {
            ProcesarJornada(jornada, Datos);
            RegistrarRecorrido(jornada, Datos);
        }

        almacen.MarcarCambios(ArchivosDatos.Historial);
        almacen.GuardarCambiosPendientes();
        Actualizado?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool CerrarJornada()
    {
        if (JornadaAbierta is not { } jornada)
        {
            return false;
        }

        // Sin conexión, la jornada se cierra a la hora de la última actividad registrada.
        DateTime hora = Datos.Conectado
            ? Datos.HoraJuego
            : almacen.Tacografo.Periodos.LastOrDefault()?.Fin ?? jornada.Inicio;

        if (jornada.TrayectoEnCurso is not null)
        {
            CerrarTrayecto(jornada);
        }

        jornada.Fin = hora < jornada.Inicio ? jornada.Inicio : hora;
        selector.Reiniciar();
        vigilante.Reiniciar();

        almacen.MarcarCambios(ArchivosDatos.Historial | ArchivosDatos.Tacografo | ArchivosDatos.Recorrido);
        almacen.GuardarCambiosPendientes();
        Actualizado?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>Botones de actividad. Solo tienen efecto con el camión parado.</summary>
    public void ElegirActividad(Actividad actividad)
    {
        selector.ElegirManual(actividad);
        Actualizado?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Apunta la posición en el recorrido de la jornada. Una posición exactamente en (0, 0)
    /// es que el juego aún no ha cargado el camión.
    /// </summary>
    private void RegistrarRecorrido(Jornada jornada, DatosJuego datos)
    {
        if (datos.PosicionX == 0 && datos.PosicionZ == 0)
        {
            return;
        }

        int minuto = (int)Math.Max(0, (datos.HoraJuego - jornada.Inicio).TotalMinutes);

        if (registradorMapa.Registrar(datos.PosicionX, datos.PosicionZ, minuto))
        {
            VersionRecorrido++;
            almacen.MarcarCambios(ArchivosDatos.Recorrido);
        }
    }

    private void ProcesarJornada(Jornada jornada, DatosJuego datos)
    {
        DateTime hora = datos.HoraJuego;
        DecisionActividad decision = selector.Decidir(datos.Velocidad, datos.MotorEncendido, hora);
        var resultado = ResultadoRegistro.Normal;

        if (decision.ReclasificarDesde is { } desde)
        {
            registrador.Reclasificar(desde, hora, decision.Actividad);
        }
        else
        {
            resultado = registrador.Registrar(hora, decision.Actividad);
        }

        ActividadRegistrada = decision.Actividad;
        almacen.MarcarCambios(ArchivosDatos.Tacografo);

        if (resultado == ResultadoRegistro.SaltoAtras)
        {
            DescartarTrayectoPosterior(jornada, hora);
        }

        EstadoTacografo anterior = Estado;
        Estado = AnalizadorTacografo.Analizar(almacen.Tacografo);

        bool faltaVelocidad = vigilante.Actualizar(datos.Velocidad, datos.LimiteVelocidad);
        ActualizarTrayecto(jornada, datos, decision, resultado, anterior, faltaVelocidad);
        SumarKilometrosTarjeta(datos.Odometro);

        ultimaHoraJuego = hora;
    }

    private void ActualizarTrayecto(
        Jornada jornada,
        DatosJuego datos,
        DecisionActividad decision,
        ResultadoRegistro resultado,
        EstadoTacografo anterior,
        bool faltaVelocidad)
    {
        DateTime hora = datos.HoraJuego;
        bool conduciendo = decision.Actividad == Actividad.Conduccion;
        Trayecto? trayecto = jornada.TrayectoEnCurso;

        // Al completar una pausa válida (descansando, la conducción continua vuelve a cero) termina
        // el trayecto. Hay que estar en descanso: al pasar una parada larga a otros trabajos la
        // conducción continua también puede bajar, y eso no es una pausa.
        if (trayecto is not null && decision.Actividad == Actividad.Descanso &&
            anterior.ConduccionContinua > TimeSpan.Zero && Estado.ConduccionContinua == TimeSpan.Zero)
        {
            CerrarTrayecto(jornada);
            trayecto = null;
        }

        if (trayecto is null)
        {
            if (!conduciendo || datos.Velocidad <= SelectorActividad.VelocidadMovimiento)
            {
                return;
            }

            trayecto = new Trayecto
            {
                Origen = datos.Origen,
                Destino = datos.Destino,
                Carga = datos.Carga,
                Inicio = hora,
                Fin = hora,
                OdometroInicial = datos.Odometro
            };

            jornada.TrayectoEnCurso = trayecto;
        }

        // El encargo puede conocerse o cambiar a mitad de trayecto.
        if (trayecto.Origen.Length == 0)
        {
            trayecto.Origen = datos.Origen;
        }

        if (datos.Destino.Length > 0)
        {
            trayecto.Destino = datos.Destino;
        }

        if (datos.Carga.Length > 0)
        {
            trayecto.Carga = datos.Carga;
        }

        if (decision.ReclasificarDesde is { } desde)
        {
            // La parada no era un semáforo: ese tramo deja de ser conducción.
            TimeSpan reclasificado = hora - desde;
            trayecto.TiempoConduccion = trayecto.TiempoConduccion > reclasificado
                ? trayecto.TiempoConduccion - reclasificado
                : TimeSpan.Zero;

            if (desde > trayecto.Inicio)
            {
                trayecto.Fin = desde;
            }
        }
        else if (conduciendo)
        {
            if (resultado == ResultadoRegistro.Normal && ultimaHoraJuego is { } antes && hora > antes)
            {
                trayecto.TiempoConduccion += hora - antes;
            }

            trayecto.Fin = hora;
        }

        trayecto.Kilometros = Math.Max(0, datos.Odometro - trayecto.OdometroInicial);
        trayecto.VelocidadMaxima = Math.Max(trayecto.VelocidadMaxima, datos.Velocidad);
        trayecto.VelocidadMedia = trayecto.TiempoConduccion > TimeSpan.Zero
            ? trayecto.Kilometros / trayecto.TiempoConduccion.TotalHours
            : 0;

        if (faltaVelocidad)
        {
            trayecto.FaltaVelocidad = true;
        }

        if (!trayecto.FaltaConduccion && HayInfraccionDeConduccion(trayecto.Inicio, hora))
        {
            trayecto.FaltaConduccion = true;
        }

        almacen.MarcarCambios(ArchivosDatos.Historial);
    }

    private bool HayInfraccionDeConduccion(DateTime desde, DateTime hasta) =>
        Estado.Infracciones.Any(i =>
            (i.Tipo is TipoInfraccion.ConduccionContinua or TipoInfraccion.ConduccionDiaria
                or TipoInfraccion.ConduccionSemanal or TipoInfraccion.ConduccionBisemanal)
            && i.Momento >= desde && i.Momento <= hasta);

    private void CerrarTrayecto(Jornada jornada)
    {
        Trayecto? trayecto = jornada.TrayectoEnCurso;
        jornada.TrayectoEnCurso = null;

        if (trayecto is not null && trayecto.Kilometros >= KilometrosMinimosTrayecto)
        {
            jornada.Trayectos.Add(trayecto);
        }

        almacen.MarcarCambios(ArchivosDatos.Historial);
        almacen.GuardarCambiosPendientes();
    }

    /// <summary>Tras cargar una partida anterior, un trayecto que aún no había empezado deja de existir.</summary>
    private static void DescartarTrayectoPosterior(Jornada jornada, DateTime hora)
    {
        if (jornada.TrayectoEnCurso is { } trayecto && trayecto.Inicio > hora)
        {
            jornada.TrayectoEnCurso = null;
        }
    }

    private void SumarKilometrosTarjeta(double odometro)
    {
        if (ultimoOdometro is { } anterior)
        {
            double recorrido = odometro - anterior;

            if (recorrido > 0 && recorrido <= SaltoMaximoOdometro)
            {
                almacen.Perfil.KilometrosTarjeta += recorrido;
                almacen.MarcarCambios(ArchivosDatos.Perfil);
            }
        }

        ultimoOdometro = odometro;
    }
}
