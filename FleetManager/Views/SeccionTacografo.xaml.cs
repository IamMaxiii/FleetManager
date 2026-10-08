using System.Windows.Controls;

namespace FleetManager.Views;

/// <summary>
/// Sección "Tacógrafo" de la ventana principal. Sin lógica: todo viene del
/// TacografoViewModel mediante bindings.
/// </summary>
public partial class SeccionTacografo : UserControl
{
    public SeccionTacografo()
    {
        InitializeComponent();
    }
}
