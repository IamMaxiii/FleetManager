using System.Windows.Input;
using FleetManager.Core;
using FleetManager.Models;
using FleetManager.Telemetry;
using FleetManager.Idiomas;

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
            Mensaje = Textos.T("Salto.HoraMal");
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
            Mensaje = Textos.T("Salto.Saltando", FechaJuego.TextoDiaYHora(objetivo));

            if (!await control.EnviarComando(SaltoTiempo.Comando(objetivo)))
            {
                Mensaje = Textos.T("Salto.SinVentana");
                return;
            }

            Mensaje = await EsperarHora(objetivo)
                ? Textos.T("Salto.Hecho", FechaJuego.TextoDiaYHora(servicio.Datos.HoraJuego))
                : Textos.T("Salto.NoCambio");
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
                Textos.T("Salto.ConfirmarConsola")))
        {
            return;
        }

        Mensaje = control.ActivarConsola() switch
        {
            ResultadoActivacion.Hecho => Textos.T("Salto.ConsolaActivada"),
            ResultadoActivacion.YaEstaba => Textos.T("Salto.ConsolaYaEstaba"),
            ResultadoActivacion.JuegoAbierto => Textos.T("Salto.CierraJuego"),
            ResultadoActivacion.ConfiguracionNoEncontrada => Textos.T("Salto.SinConfig"),
            _ => Textos.T("Salto.ConfigError")
        };

        ConsolaActivada = control.ConsolaActivada();
        CommandManager.InvalidateRequerySuggested();
    }

    private void ServicioActualizado(object? sender, EventArgs e) => JuegoConectado = servicio.Datos.Conectado;
}
