using System.Windows;
using System.Windows.Input;
using FleetManager.ViewModels;

namespace FleetManager.Views;

/// <summary>
/// Mini tacógrafo: ventana pequeña siempre encima. Lo que muestra viene del mismo
/// TacografoViewModel que la ventana principal. Aquí solo está lo que es propio de
/// la ventana: colocarla, arrastrarla y recordar dónde se dejó.
/// </summary>
public partial class MiniTacografoWindow : Window
{
    private readonly MainViewModel viewModel;

    public MiniTacografoWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        DataContext = viewModel;

        Loaded += (_, _) => Colocar();
        MouseLeftButtonDown += Arrastrar;
    }

    /// <summary>Lo pone donde se dejó la última vez, dentro de la zona visible de las pantallas.</summary>
    public void Colocar()
    {
        var zonaVisible = new Rect(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);

        Point posicion = PosicionVentana.Ajustar(
            viewModel.Ajustes.MiniTacografoIzquierda,
            viewModel.Ajustes.MiniTacografoArriba,
            new Size(ActualWidth, ActualHeight),
            zonaVisible);

        Left = posicion.X;
        Top = posicion.Y;
    }

    private void Arrastrar(object sender, MouseButtonEventArgs e)
    {
        // DragMove no vuelve hasta que se suelta el botón: después se guarda la posición.
        DragMove();
        viewModel.GuardarPosicionMini(Left, Top);
    }

    private void Ocultar_Click(object sender, RoutedEventArgs e) => viewModel.MiniVisible = false;
}
