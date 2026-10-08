using System.Windows.Input;
using FleetManager.Idiomas;
using FleetManager.Telemetry;

namespace FleetManager.ViewModels;

/// <summary>
/// Aviso del menú lateral cuando falta el plugin de telemetría en el juego (o es de
/// otra versión), con el botón para instalarlo.
/// </summary>
public sealed class PluginViewModel : ObjetoObservable
{
    private readonly InstaladorPlugin instalador;
    private readonly IDialogos dialogos;

    private EstadoPlugin estado = EstadoPlugin.Instalado;
    private string aviso = "";
    private string textoBoton = "";

    public PluginViewModel(InstaladorPlugin instalador, IDialogos dialogos)
    {
        this.instalador = instalador;
        this.dialogos = dialogos;
        ComandoInstalar = new Comando(Instalar);
    }

    public EstadoPlugin Estado { get => estado; private set => Asignar(ref estado, value); }

    public string Aviso
    {
        get => aviso;
        private set
        {
            if (Asignar(ref aviso, value))
            {
                Avisar(nameof(HayAviso));
            }
        }
    }

    public bool HayAviso => Aviso.Length > 0;

    public string TextoBoton
    {
        get => textoBoton;
        private set
        {
            if (Asignar(ref textoBoton, value))
            {
                Avisar(nameof(HayBoton));
            }
        }
    }

    public bool HayBoton => TextoBoton.Length > 0;

    public ICommand ComandoInstalar { get; }

    public void Comprobar()
    {
        Estado = instalador.Comprobar();

        (Aviso, TextoBoton) = Estado switch
        {
            EstadoPlugin.Falta => (Textos.T("Plugin.Falta"), Textos.T("Plugin.Instalar")),
            EstadoPlugin.OtraVersion => (Textos.T("Plugin.OtraVersion"), Textos.T("Plugin.Actualizar")),
            EstadoPlugin.JuegoNoEncontrado => (Textos.T("Plugin.SinJuego"), ""),
            _ => ("", "")
        };
    }

    private void Instalar()
    {
        if (Estado == EstadoPlugin.OtraVersion &&
            !dialogos.Confirmar(Textos.T("Plugin.ConfirmarSustituir")))
        {
            return;
        }

        switch (instalador.Instalar())
        {
            case ResultadoPlugin.Hecho:
                dialogos.Informar(
                    Textos.T("Plugin.Instalado"));
                break;

            case ResultadoPlugin.JuegoAbierto:
                dialogos.Informar(Textos.T("Plugin.EnUso"));
                break;

            case ResultadoPlugin.Error:
                dialogos.Informar(Textos.T("Plugin.Error"));
                break;

            case ResultadoPlugin.Cancelado:
                break;
        }

        Comprobar();
    }
}
