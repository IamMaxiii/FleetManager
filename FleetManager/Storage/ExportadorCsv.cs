using System.Globalization;
using System.IO;
using System.Text;
using FleetManager.Core;
using FleetManager.Models;

namespace FleetManager.Storage;

/// <summary>
/// Exporta trayectos a CSV para abrirlos con Excel en español: separador ";",
/// coma decimal y UTF-8 con BOM (para que Excel lea bien los acentos).
/// </summary>
public static class ExportadorCsv
{
    public const char Separador = ';';

    public static readonly IReadOnlyList<string> Columnas =
    [
        "Jornada", "Día", "Origen", "Destino", "Carga", "Kilómetros",
        "Velocidad media", "Velocidad máxima", "Inicio", "Fin",
        "Falta de velocidad", "Falta de conducción"
    ];

    private static readonly CultureInfo Espanol = CultureInfo.GetCultureInfo("es-ES");

    public static string Generar(IEnumerable<(Jornada Jornada, Trayecto Trayecto)> filas)
    {
        var texto = new StringBuilder();
        texto.Append(string.Join(Separador, Columnas)).Append("\r\n");

        foreach (var (jornada, trayecto) in filas)
        {
            string[] campos =
            [
                jornada.Numero.ToString(Espanol),
                FechaJuego.TextoDia(trayecto.Inicio),
                trayecto.Origen,
                trayecto.Destino,
                trayecto.Carga,
                trayecto.Kilometros.ToString("0.0", Espanol),
                trayecto.VelocidadMedia.ToString("0.0", Espanol),
                trayecto.VelocidadMaxima.ToString("0.0", Espanol),
                FechaJuego.TextoHora(trayecto.Inicio),
                // El fin lleva el día solo si es distinto del de inicio.
                trayecto.Fin.Date == trayecto.Inicio.Date
                    ? FechaJuego.TextoHora(trayecto.Fin)
                    : FechaJuego.TextoDiaYHora(trayecto.Fin),
                trayecto.FaltaVelocidad ? "Sí" : "No",
                trayecto.FaltaConduccion ? "Sí" : "No"
            ];

            texto.Append(string.Join(Separador, campos.Select(Escapar))).Append("\r\n");
        }

        return texto.ToString();
    }

    public static void Guardar(string ruta, string contenido) =>
        File.WriteAllText(ruta, contenido, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

    /// <summary>Entre comillas si el texto lleva ";", comillas o saltos de línea (las comillas se duplican).</summary>
    private static string Escapar(string campo) =>
        campo.IndexOfAny([Separador, '"', '\r', '\n']) >= 0
            ? "\"" + campo.Replace("\"", "\"\"") + "\""
            : campo;
}
