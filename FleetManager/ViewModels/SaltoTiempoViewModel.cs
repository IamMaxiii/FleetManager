using System.Windows.Input;
using FleetManager.Core;
using FleetManager.Models;
using FleetManager.Telemetry;

namespace FleetManager.ViewModels;

/// <summary>
/// Panel "Saltar tiempo" de la página de inicio: escribe <c>g_set_time</c> en la consola
/// del juego y después comprueba que la hora del juego ha cambiado de verdad. El salto
/// queda en el tacógrafo como descanso (como dormir), igual que cualquier salto adelante.
/// </summary>
public sealed class SaltoTiempoViewModel : ObjetoObservable, IDisposable
{
    public static readonly TimeSpan EsperaPorDefecto = TimeSpan.FromSeconds(8);

    private readonly ServicioTacografo servicio;
    private readonly IControlJuego control;
    private readonly IDialogos dialogos;
    private readonly TimeSpan esperaComprobacion;

    private bool consolaActivada;
    private bool juegoConectado;
    private bool ocupado;
    private string horaDestino = "06:00";
    private string mensaje = "";

    public SaltoTiempoViewModel(ServicioTacografo servicio, IControlJuego control, IDialogos dialogos, TimeSpan esperaComprobacion)
    {
        this.servicio = servicio;
        this.control = control;
        this.dialogos = dialogos;
        this.esperaComprobacion = esperaComprobacion;

        ComandoSaltar9 = new Comando(() => _ = SaltarDuracion(TimeSpan.FromHours(9)), PuedeSaltar);
        ComandoSaltar11 = new Comando(() => _ = SaltarDuracion(TimeSpan.FromHours(11)), PuedeSaltar);
        ComandoSaltarHasta = new Comando(() => _ = SaltarHasta(), PuedeSaltar);
        ComandoActivarConsola = new Comando(ActivarConsola, () => !ConsolaActivada);

        ConsolaActivada = control.ConsolaActivada();
        servicio.Actualizado += ServicioActualizado;
        JuegoConectado = servicio.Datos.Conectado;
    }

    public bool ConsolaActivada { get => consolaActivada; private set => Asignar(ref consolaActivada, value); }

    public bool JuegoConectado
    {
        get => juegoConectado;
        private set
        {
            if (Asignar(ref juegoConectado, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    /// <summary>Hay un salto en marcha (mientras tanto los botones se desactivan).</summary>
    public bool Ocupado { get => ocupado; private set => Asignar(ref ocupado, value); }

    /// <summary>Hora del día a la que saltar con "Saltar hasta" (HH:mm).</summary>
    public string HoraDestino { get => horaDestino; set => Asignar(ref horaDestino, value); }

    public string Mensaje { get => mensaje; private set => Asignar(ref mensaje, value); }

    public ICommand ComandoSaltar9 { get; }

    public ICommand ComandoSaltar11 { get; }

    public ICommand ComandoSaltarHasta { get; }

    public ICommand ComandoActivarConsola { get; }

    public void Dispose() => servicio.Actualizado -= ServicioActualizado;

    public Task SaltarDuracion(TimeSpan duracion) =>
        Saltar(SaltoTiempo.Objetivo(servicio.Datos.HoraJuego, duracion));

    public Task SaltarHasta()
    {
        if (!LecturaCampos.Hora(HoraDestino, out TimeSpan hora))
        {
            Mensaje = "Escribe la hora con el formato HH:mm (por ejemplo, 06:00).";
            return Task.CompletedTask;
        }

        return Saltar(SaltoTiempo.ObjetivoHasta(servicio.Datos.HoraJuego, hora));
    }

    private bool PuedeSaltar() => ConsolaActivada && JuegoConectado && !Ocupado;

    private async Task Saltar(DateTime objetivo)
    {
        if (!PuedeSaltar())
        {
            return;
        }

        Ocupado = true;
        CommandManager.InvalidateRequerySuggested();

        try
        {
            Mensaje = $"Saltando a {FechaJuego.TextoDiaYHora(objetivo)}… no toques el teclado.";

            if (!await control.EnviarComando(SaltoTiempo.Comando(objetivo)))
            {
                Mensaje = "No se encuentra la ventana del juego.";
                return;
            }

            Mensaje = await EsperarHora(objetivo)
                ? $"Hecho: ahora es {FechaJuego.TextoDiaYHora(servicio.Datos.HoraJuego)}. El salto cuenta como descanso."
                : "La hora del juego no ha cambiado. Comprueba que la consola se abre con la tecla º y vuelve a intentarlo.";
        }
        finally
        {
            Ocupado = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>Espera a que la hora del juego llegue al objetivo (con 2 min de margen).</summary>
    private async Task<bool> EsperarHora(DateTime objetivo)
    {
        DateTime limite = DateTime.UtcNow + esperaComprobacion;

        while (true)
        {
            DatosJuego datos = servicio.Datos;

            if (datos.Conectado && datos.HoraJuego >= objetivo.AddMinutes(-2))
            {
                return true;
            }

            if (DateTime.UtcNow >= limite)
            {
                return false;
            }

            await Task.Delay(250);
        }
    }

    private void ActivarConsola()
    {
        if (!dialogos.Confirmar(
                "Para saltar el tiempo, FleetManager tiene que activar la consola del juego en su archivo " +
                "config.cfg (se guarda antes una copia del original). ¿Activarla?"))
        {
            return;
        }

        Mensaje = control.ActivarConsola() switch
        {
            ResultadoActivacion.Hecho => "Consola activada. Ya puedes abrir el juego.",
            ResultadoActivacion.YaEstaba => "La consola ya estaba activada.",
            ResultadoActivacion.JuegoAbierto => "Cierra el juego y vuelve a pulsar: si no, al cerrarse borraría el cambio.",
            ResultadoActivacion.ConfiguracionNoEncontrada => "No se encuentra config.cfg del juego en Documentos\\Euro Truck Simulator 2.",
            _ => "No se pudo cambiar config.cfg. Mira el registro de FleetManager."
        };

        ConsolaActivada = control.ConsolaActivada();
        CommandManager.InvalidateRequerySuggested();
    }

    private void ServicioActualizado(object? sender, EventArgs e) => JuegoConectado = servicio.Datos.Conectado;
}
