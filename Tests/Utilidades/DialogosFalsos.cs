using FleetManager.ViewModels;

namespace FleetManager.Tests.Utilidades;

/// <summary>
/// Diálogos para las pruebas: no abren ventanas, responden lo que se les diga y
/// apuntan lo que se ha preguntado.
/// </summary>
public sealed class DialogosFalsos : IDialogos
{
    /// <summary>Respuesta a todas las confirmaciones.</summary>
    public bool Respuesta { get; set; } = true;

    /// <summary>Ruta que se devuelve al pedir dónde guardar un CSV (vacía = el usuario cancela).</summary>
    public string? RutaCsv { get; set; }

    public List<string> Preguntas { get; } = [];

    public bool Confirmar(string mensaje)
    {
        Preguntas.Add(mensaje);
        return Respuesta;
    }

    public void Informar(string mensaje) => Preguntas.Add(mensaje);

    public string? ElegirArchivoCsv(string nombreSugerido) => RutaCsv;
}
