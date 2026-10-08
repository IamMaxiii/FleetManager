using System.Windows;
using FleetManager.ViewModels;

namespace FleetManager.Views;

/// <summary>
/// Ventana principal. No contiene lógica: todo lo que muestra viene del
/// <see cref="MainViewModel"/> mediante bindings.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
