using System.Globalization;
using System.IO;
using System.Text;
using FleetManager.Core;
using FleetManager.Idiomas;
using FleetManager.Models;

namespace FleetManager.Storage;

/// <summary>
/// Exporta trayectos a CSV para abrirlos con Excel en el idioma de la aplicación:
/// cabecera traducida, decimales con la coma o el punto de ese idioma, separador ";"
/// (o "," en inglés, que es lo que espera Excel en inglés) y UTF-8 con BOM (para que
/// Excel lea bien los acentos).
/// </summary>
public static class ExportadorCsv
{
    private static readonly string[] ClavesColumnas =
    [
        "Csv.Jornada", "Csv.Dia", "Csv.Origen", "Csv.Destino", "Csv.Carga", "Csv.Kilometros",
        "Csv.VelocidadMedia", "Csv.VelocidadMaxima", "Csv.Inicio", "Csv.Fin",
        "Csv.FaltaVelocidad", "Csv.FaltaConduccion"
    ];

    /// <summary>";" o, si los decimales llevan punto (inglés), ",".</summary>
    public static char Separador => Cultura.NumberFormat.NumberDecimalSeparator == "," ? ';' : ',';

    public static IReadOnlyList<string> Columnas => ClavesColumnas.Select(Textos.T).ToList();

    private static CultureInfo Cultura => Textos.Cultura;

    public static string Generar(IEnumerable<(Jornada Jornada, Trayecto Trayecto)> filas)
    {
        var texto = new StringBuilder();
        texto.Append(string.Join(Separador, Columnas.Select(Escapar))).Append("\r\n");

        foreach (var (jornada, trayecto) in filas)
        {
            string[] campos =
            [
                jornada.Numero.ToString(CultureInfo.InvariantCulture),
                FechaJuego.TextoDia(trayecto.Inicio),
                trayecto.Origen,
                trayecto.Destino,
                trayecto.Carga,
                trayecto.Kilometros.ToString("0.0", Cultura),
                trayecto.VelocidadMedia.ToString("0.0", Cultura),
                trayecto.VelocidadMaxima.ToString("0.0", Cultura),
                FechaJuego.TextoHora(trayecto.Inicio),
                // El fin lleva el día solo si es distinto del de inicio.
                trayecto.Fin.Date == trayecto.Inicio.Date
                    ? FechaJuego.TextoHora(trayecto.Fin)
                    : FechaJuego.TextoDiaYHora(trayecto.Fin),
                trayecto.FaltaVelocidad ? Textos.T("Comun.Si") : Textos.T("Comun.No"),
                trayecto.FaltaConduccion ? Textos.T("Comun.Si") : Textos.T("Comun.No")
            ];

            texto.Append(string.Join(Separador, campos.Select(Escapar))).Append("\r\n");
        }

        return texto.ToString();
    }

    public static void Guardar(string ruta, string contenido) =>
        File.WriteAllText(ruta, contenido, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

    /// <summary>Entre comillas si el texto lleva el separador, comillas o saltos de línea (las comillas se duplican).</summary>
    private static string Escapar(string campo) =>
        campo.IndexOfAny([Separador, '"', '\r', '\n']) >= 0
            ? "\"" + campo.Replace("\"", "\"\"") + "\""
            : campo;
}
