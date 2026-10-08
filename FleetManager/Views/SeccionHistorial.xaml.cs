using System.Windows;
using System.Windows.Controls;
using FleetManager.ViewModels;

namespace FleetManager.Views;

/// <summary>
/// Sección "Historial". El árbol y la tabla de WPF no permiten enlazar lo elegido
/// con un binding normal, así que aquí solo se le pasa al ViewModel; el resto va
/// por bindings.
/// </summary>
public partial class SeccionHistorial : UserControl
{
    public SeccionHistorial()
    {
        InitializeComponent();
    }

    private HistorialViewModel? ViewModel => DataContext as HistorialViewModel;

    private void Arbol_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (ViewModel is { } viewModel && e.NewValue is NodoHistorial nodo)
        {
            viewModel.NodoSeleccionado = nodo;
        }
    }

    private void Tabla_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is { } viewModel)
        {
            viewModel.FilasSeleccionadas = Tabla.SelectedItems.OfType<FilaTrayecto>().ToList();
        }
    }
}
