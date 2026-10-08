using System.Windows.Input;
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
            EstadoPlugin.Falta => ("Falta el plugin de telemetría en el juego: sin él FleetManager no puede leer ETS2.", "Instalar plugin"),
            EstadoPlugin.OtraVersion => ("El plugin de telemetría del juego es de otra versión y puede no funcionar con FleetManager.", "Actualizar plugin"),
            EstadoPlugin.JuegoNoEncontrado => ("No se encuentra ETS2 en Steam: no se puede comprobar el plugin de telemetría.", ""),
            _ => ("", "")
        };
    }

    private void Instalar()
    {
        if (Estado == EstadoPlugin.OtraVersion &&
            !dialogos.Confirmar("Se sustituirá el plugin de telemetría del juego (scs-telemetry.dll) por el que trae FleetManager. ¿Continuar?"))
        {
            return;
        }

        switch (instalador.Instalar())
        {
            case ResultadoPlugin.Hecho:
                dialogos.Informar(
                    "Plugin instalado.\n\n" +
                    "Si el juego está abierto, ciérralo y vuelve a abrirlo. Al arrancar, ETS2 avisa de que " +
                    "se usan funciones avanzadas del SDK: pulsa OK para continuar.");
                break;

            case ResultadoPlugin.JuegoAbierto:
                dialogos.Informar("No se ha podido copiar el plugin porque el juego lo está usando. Cierra ETS2 y vuelve a intentarlo.");
                break;

            case ResultadoPlugin.Error:
                dialogos.Informar("No se ha podido instalar el plugin. Los detalles están en registro.log (botón \"Carpeta de datos\").");
                break;

            case ResultadoPlugin.Cancelado:
                break;
        }

        Comprobar();
    }
}
