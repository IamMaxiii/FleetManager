using System.Windows.Input;
using FleetManager.Core;
using FleetManager.Models;
using FleetManager.Storage;
using FleetManager.Idiomas;

namespace FleetManager.ViewModels;

/// <summary>
/// Sección "Conductor": perfil editable y kilómetros de la tarjeta.
/// Los cambios se guardan con el guardado automático.
/// </summary>
public sealed class ConductorViewModel : ObjetoObservable, IDisposable
{
    private readonly AlmacenDatos almacen;
    private readonly ServicioTacografo servicio;
    private readonly IDialogos dialogos;
    private string kilometrosTarjeta = "";
    private bool editandoKilometros;
    private string kilometrosEditados = "";
    private string errorKilometros = "";

    public ConductorViewModel(AlmacenDatos almacen, ServicioTacografo servicio, IDialogos dialogos, Random azar)
    {
        this.almacen = almacen;
        this.servicio = servicio;
        this.dialogos = dialogos;

        // La primera vez se inventan los números de permiso y ADR.
        if (Perfil.NumeroPermiso.Length == 0 || Perfil.NumeroAdr.Length == 0)
        {
            if (Perfil.NumeroPermiso.Length == 0)
            {
                Perfil.NumeroPermiso = GeneradorDocumentos.NumeroPermiso(azar);
            }

            if (Perfil.NumeroAdr.Length == 0)
            {
                Perfil.NumeroAdr = GeneradorDocumentos.NumeroAdr(azar);
            }

            almacen.MarcarCambios(ArchivosDatos.Perfil);
        }

        ComandoPonerKilometrosACero = new Comando(PonerKilometrosACero);
        ComandoCambiarKilometros = new Comando(EmpezarACambiarKilometros);
        ComandoGuardarKilometros = new Comando(() => GuardarKilometros());
        ComandoCancelarKilometros = new Comando(() => EditandoKilometros = false);

        servicio.Actualizado += ServicioActualizado;
        ActualizarKilometros();
    }

    public string Nombre
    {
        get => Perfil.Nombre;
        set => CambiarPerfil(Perfil.Nombre, value, v => Perfil.Nombre = v);
    }

    public string Apellidos
    {
        get => Perfil.Apellidos;
        set => CambiarPerfil(Perfil.Apellidos, value, v => Perfil.Apellidos = v);
    }

    public string FechaNacimiento
    {
        get => Perfil.FechaNacimiento;
        set => CambiarPerfil(Perfil.FechaNacimiento, value, v => Perfil.FechaNacimiento = v);
    }

    public string NumeroPermiso => Perfil.NumeroPermiso;

    public string NumeroAdr => Perfil.NumeroAdr;

    public string KilometrosTarjeta { get => kilometrosTarjeta; private set => Asignar(ref kilometrosTarjeta, value); }

    public ICommand ComandoPonerKilometrosACero { get; }

    // ---------- Cambiar los kilómetros a mano ----------
    // Mientras se escribe, el número no se toca aunque se esté conduciendo.

    public bool EditandoKilometros { get => editandoKilometros; private set => Asignar(ref editandoKilometros, value); }

    /// <summary>Lo que el usuario escribe en el campo de kilómetros.</summary>
    public string KilometrosEditados { get => kilometrosEditados; set => Asignar(ref kilometrosEditados, value); }

    public string ErrorKilometros { get => errorKilometros; private set => Asignar(ref errorKilometros, value); }

    public ICommand ComandoCambiarKilometros { get; }

    public ICommand ComandoGuardarKilometros { get; }

    public ICommand ComandoCancelarKilometros { get; }

    private PerfilConductor Perfil => almacen.Perfil;

    /// <returns>Verdadero si se ha guardado.</returns>
    public bool GuardarKilometros()
    {
        if (!LecturaCampos.Decimal(KilometrosEditados, out double kilometros) || kilometros < 0)
        {
            ErrorKilometros = Textos.T("Conductor.KmMal");
            return false;
        }

        Perfil.KilometrosTarjeta = kilometros;
        almacen.MarcarCambios(ArchivosDatos.Perfil);
        EditandoKilometros = false;
        ActualizarKilometros();
        return true;
    }

    private void EmpezarACambiarKilometros()
    {
        KilometrosEditados = LecturaCampos.Escribir(Perfil.KilometrosTarjeta);
        ErrorKilometros = "";
        EditandoKilometros = true;
    }

    public void Dispose() => servicio.Actualizado -= ServicioActualizado;

    private void CambiarPerfil(string actual, string nuevo, Action<string> asignar, [System.Runtime.CompilerServices.CallerMemberName] string? propiedad = null)
    {
        nuevo = nuevo.Trim();

        if (actual == nuevo)
        {
            return;
        }

        asignar(nuevo);
        almacen.MarcarCambios(ArchivosDatos.Perfil);
        Avisar(propiedad);
    }

    private void PonerKilometrosACero()
    {
        if (!dialogos.Confirmar(Textos.T("Conductor.ConfirmarCero")))
        {
            return;
        }

        Perfil.KilometrosTarjeta = 0;
        almacen.MarcarCambios(ArchivosDatos.Perfil);
        ActualizarKilometros();
    }

    private void ServicioActualizado(object? sender, EventArgs e) => ActualizarKilometros();

    private void ActualizarKilometros() => KilometrosTarjeta = Formato.Kilometros(Perfil.KilometrosTarjeta);
}
