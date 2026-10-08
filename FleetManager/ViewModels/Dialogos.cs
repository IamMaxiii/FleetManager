using System.Windows;
using Microsoft.Win32;

namespace FleetManager.ViewModels;

/// <summary>
/// Preguntas y avisos al usuario. Es una interfaz para que los ViewModels se puedan
/// probar con pruebas automáticas sin abrir ventanas.
/// </summary>
public interface IDialogos
{
    bool Confirmar(string mensaje);

    void Informar(string mensaje);

    /// <summary>Pregunta dónde guardar un CSV; vacío si el usuario cancela.</summary>
    string? ElegirArchivoCsv(string nombreSugerido);
}

/// <summary>Los diálogos de verdad, con ventanas de Windows.</summary>
public sealed class DialogosWpf : IDialogos
{
    private const string Titulo = "FleetManager";

    public bool Confirmar(string mensaje) =>
        MessageBox.Show(mensaje, Titulo, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public void Informar(string mensaje) =>
        MessageBox.Show(mensaje, Titulo, MessageBoxButton.OK, MessageBoxImage.Information);

    public string? ElegirArchivoCsv(string nombreSugerido)
    {
        var dialogo = new SaveFileDialog
        {
            Title = "Exportar a CSV",
            FileName = nombreSugerido,
            DefaultExt = ".csv",
            Filter = "Archivo CSV (*.csv)|*.csv",
            AddExtension = true,
            OverwritePrompt = true
        };

        return dialogo.ShowDialog() == true ? dialogo.FileName : null;
    }
}
