using System.Globalization;
using System.Windows.Input;
using FleetManager.Core;
using FleetManager.Idiomas;
using FleetManager.Models;

namespace FleetManager.ViewModels;

/// <summary>
/// Lee lo que el usuario escribe en los campos de edición.
/// </summary>
public static class LecturaCampos
{
    // Coma decimal y punto de miles ("1.234,5"), como en español.
    private static readonly CultureInfo ComaDecimal = CultureInfo.GetCultureInfo("es-ES");

    /// <summary>
    /// Número decimal con coma ("12,5") o con punto ("12.5"). El separador de miles es el
    /// del idioma: en los idiomas con coma decimal, un punto entre grupos de tres cifras
    /// separa miles ("125.000" = 125000); en inglés, la coma ("125,000" = 125000).
    /// </summary>
    public static bool Decimal(string texto, out double valor)
    {
        texto = texto.Trim();
        bool comaDecimal = Textos.Cultura.NumberFormat.NumberDecimalSeparator == ",";
        string miles = comaDecimal ? @"\." : ",";

        if ((texto.Contains(',') && texto.Contains('.')) ||
            System.Text.RegularExpressions.Regex.IsMatch(texto, $@"^-?\d{{1,3}}({miles}\d{{3}})+$"))
        {
            // "1.234,5" / "125.000" (o "1,234.5" / "125,000" en inglés): hay separador de miles.
            CultureInfo formato = comaDecimal ? ComaDecimal : CultureInfo.InvariantCulture;
            return double.TryParse(texto, NumberStyles.Float | NumberStyles.AllowThousands, formato, out valor);
        }

        return double.TryParse(texto.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out valor);
    }

    /// <summary>Número de día del juego (1 o más).</summary>
    public static bool Dia(string texto, out int dia) =>
        int.TryParse(texto.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out dia) && dia >= 1;

    /// <summary>Hora "HH:mm" (de 00:00 a 23:59).</summary>
    public static bool Hora(string texto, out TimeSpan hora) =>
        TimeSpan.TryParseExact(texto.Trim(), [@"h\:mm", @"hh\:mm"], CultureInfo.InvariantCulture, out hora)
        && hora < TimeSpan.FromDays(1);

    public static string Escribir(double valor) => valor.ToString("0.0", Textos.Cultura);
}

/// <summary>
/// Panel de edición de un trayecto. Se edita en campos de texto; al guardar se
/// comprueba todo y solo entonces se cambia el trayecto de verdad.
/// </summary>
public sealed class EdicionTrayectoViewModel : ObjetoObservable
{
    private readonly Trayecto original;
    private readonly Action alGuardar;

    private string origen = "";
    private string destino = "";
    private string carga = "";
    private string kilometros = "";
    private string velocidadMedia = "";
    private string velocidadMaxima = "";
    private string diaInicio = "";
    private string horaInicio = "";
    private string diaFin = "";
    private string horaFin = "";
    private bool faltaVelocidad;
    private bool faltaConduccion;
    private string errores = "";

    public EdicionTrayectoViewModel(Jornada jornada, Trayecto trayecto, Action alGuardar)
    {
        original = trayecto;
        this.alGuardar = alGuardar;
        Titulo = Textos.T("Edicion.TrayectoDeJornada", jornada.Numero);

        ComandoGuardar = new Comando(() => Guardar());
        ComandoDescartar = new Comando(Cargar);
        Cargar();
    }

    public string Titulo { get; }

    public string Origen { get => origen; set => Asignar(ref origen, value); }

    public string Destino { get => destino; set => Asignar(ref destino, value); }

    public string Carga { get => carga; set => Asignar(ref carga, value); }

    public string Kilometros { get => kilometros; set => Asignar(ref kilometros, value); }

    public string VelocidadMedia { get => velocidadMedia; set => Asignar(ref velocidadMedia, value); }

    public string VelocidadMaxima { get => velocidadMaxima; set => Asignar(ref velocidadMaxima, value); }

    public string DiaInicio { get => diaInicio; set => Asignar(ref diaInicio, value); }

    public string HoraInicio { get => horaInicio; set => Asignar(ref horaInicio, value); }

    public string DiaFin { get => diaFin; set => Asignar(ref diaFin, value); }

    public string HoraFin { get => horaFin; set => Asignar(ref horaFin, value); }

    public bool FaltaVelocidad { get => faltaVelocidad; set => Asignar(ref faltaVelocidad, value); }

    public bool FaltaConduccion { get => faltaConduccion; set => Asignar(ref faltaConduccion, value); }

    /// <summary>Problemas encontrados al intentar guardar; vacío si no hay.</summary>
    public string Errores { get => errores; private set => Asignar(ref errores, value); }

    public ICommand ComandoGuardar { get; }

    public ICommand ComandoDescartar { get; }

    /// <returns>Verdadero si se ha guardado.</returns>
    public bool Guardar()
    {
        var problemas = new List<string>();

        if (!LecturaCampos.Decimal(Kilometros, out double km))
        {
            problemas.Add(Textos.T("Edicion.KmMal"));
        }

        if (!LecturaCampos.Decimal(VelocidadMedia, out double media) || !LecturaCampos.Decimal(VelocidadMaxima, out double maxima))
        {
            problemas.Add(Textos.T("Edicion.VelocidadesMal"));
            media = maxima = 0;
        }

        DateTime? inicio = LeerFecha(DiaInicio, HoraInicio, "Inicio", problemas);
        DateTime? fin = LeerFecha(DiaFin, HoraFin, "Fin", problemas);

        if (problemas.Count == 0)
        {
            var editado = new Trayecto
            {
                Kilometros = km,
                VelocidadMedia = media,
                VelocidadMaxima = maxima,
                Inicio = inicio!.Value,
                Fin = fin!.Value
            };

            problemas.AddRange(ValidadorHistorial.ValidarTrayecto(editado));
        }

        if (problemas.Count > 0)
        {
            Errores = string.Join(Environment.NewLine, problemas);
            return false;
        }

        original.Origen = Origen.Trim();
        original.Destino = Destino.Trim();
        original.Carga = Carga.Trim();
        original.Kilometros = km;
        original.VelocidadMedia = media;
        original.VelocidadMaxima = maxima;
        original.Inicio = inicio!.Value;
        original.Fin = fin!.Value;
        original.FaltaVelocidad = FaltaVelocidad;
        original.FaltaConduccion = FaltaConduccion;

        Errores = "";
        alGuardar();
        return true;
    }

    private void Cargar()
    {
        Origen = original.Origen;
        Destino = original.Destino;
        Carga = original.Carga;
        Kilometros = LecturaCampos.Escribir(original.Kilometros);
        VelocidadMedia = LecturaCampos.Escribir(original.VelocidadMedia);
        VelocidadMaxima = LecturaCampos.Escribir(original.VelocidadMaxima);
        DiaInicio = FechaJuego.NumeroDia(original.Inicio).ToString(CultureInfo.InvariantCulture);
        HoraInicio = FechaJuego.TextoHora(original.Inicio);
        DiaFin = FechaJuego.NumeroDia(original.Fin).ToString(CultureInfo.InvariantCulture);
        HoraFin = FechaJuego.TextoHora(original.Fin);
        FaltaVelocidad = original.FaltaVelocidad;
        FaltaConduccion = original.FaltaConduccion;
        Errores = "";
    }

    /// <param name="cual">"Inicio" o "Fin" (parte de la clave del mensaje).</param>
    internal static DateTime? LeerFecha(string dia, string hora, string cual, List<string> problemas)
    {
        if (!LecturaCampos.Dia(dia, out int numeroDia))
        {
            problemas.Add(Textos.T($"Edicion.Dia{cual}Mal"));
            return null;
        }

        if (!LecturaCampos.Hora(hora, out TimeSpan momento))
        {
            problemas.Add(Textos.T($"Edicion.Hora{cual}Mal"));
            return null;
        }

        return FechaJuego.Componer(numeroDia, momento);
    }
}

/// <summary>
/// Panel de edición de una jornada: horas de inicio y de fin. La jornada abierta no se edita.
/// </summary>
public sealed class EdicionJornadaViewModel : ObjetoObservable
{
    private readonly Jornada original;
    private readonly Action alGuardar;

    private string diaInicio = "";
    private string horaInicio = "";
    private string diaFin = "";
    private string horaFin = "";
    private string errores = "";

    public EdicionJornadaViewModel(Jornada jornada, Action alGuardar)
    {
        original = jornada;
        this.alGuardar = alGuardar;
        Titulo = Textos.T("Tarjeta.Jornada", jornada.Numero);
        Editable = !jornada.Abierta;

        ComandoGuardar = new Comando(() => Guardar(), () => Editable);
        ComandoDescartar = new Comando(Cargar, () => Editable);
        Cargar();
    }

    public string Titulo { get; }

    /// <summary>La jornada abierta no se puede editar hasta cerrarla.</summary>
    public bool Editable { get; }

    public string DiaInicio { get => diaInicio; set => Asignar(ref diaInicio, value); }

    public string HoraInicio { get => horaInicio; set => Asignar(ref horaInicio, value); }

    public string DiaFin { get => diaFin; set => Asignar(ref diaFin, value); }

    public string HoraFin { get => horaFin; set => Asignar(ref horaFin, value); }

    public string Errores { get => errores; private set => Asignar(ref errores, value); }

    public ICommand ComandoGuardar { get; }

    public ICommand ComandoDescartar { get; }

    /// <returns>Verdadero si se ha guardado.</returns>
    public bool Guardar()
    {
        if (!Editable)
        {
            return false;
        }

        var problemas = new List<string>();
        DateTime? inicio = EdicionTrayectoViewModel.LeerFecha(DiaInicio, HoraInicio, "Inicio", problemas);
        DateTime? fin = EdicionTrayectoViewModel.LeerFecha(DiaFin, HoraFin, "Fin", problemas);

        if (problemas.Count == 0)
        {
            var editada = new Jornada { Inicio = inicio!.Value, Fin = fin!.Value, Trayectos = original.Trayectos };
            problemas.AddRange(ValidadorHistorial.ValidarJornada(editada));
        }

        if (problemas.Count > 0)
        {
            Errores = string.Join(Environment.NewLine, problemas);
            return false;
        }

        original.Inicio = inicio!.Value;
        original.Fin = fin!.Value;
        Errores = "";
        alGuardar();
        return true;
    }

    private void Cargar()
    {
        DiaInicio = FechaJuego.NumeroDia(original.Inicio).ToString(CultureInfo.InvariantCulture);
        HoraInicio = FechaJuego.TextoHora(original.Inicio);
        DiaFin = original.Fin is { } fin ? FechaJuego.NumeroDia(fin).ToString(CultureInfo.InvariantCulture) : "";
        HoraFin = original.Fin is { } f ? FechaJuego.TextoHora(f) : "";
        Errores = "";
    }
}
