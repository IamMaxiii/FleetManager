using System.Windows;
using System.Windows.Controls;

namespace FleetManager.Views;

/// <summary>
/// Página de inicio. Los botones del mapa (acercar, alejar, ver todo) actúan
/// directamente sobre el lienzo, porque solo cambian lo que se ve.
/// </summary>
public partial class SeccionInicio : UserControl
{
    public SeccionInicio()
    {
        InitializeComponent();
    }

    private void Acercar_Click(object sender, RoutedEventArgs e) => Lienzo.Acercar(mas: true);

    private void Alejar_Click(object sender, RoutedEventArgs e) => Lienzo.Acercar(mas: false);

    private void VerTodo_Click(object sender, RoutedEventArgs e) => Lienzo.VerTodo();
}
