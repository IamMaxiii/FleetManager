using System.Windows.Controls;

namespace FleetManager.Views;

/// <summary>
/// Sección "Camión" de la ventana principal. Sin lógica: todo viene del
/// CamionViewModel mediante bindings.
/// </summary>
public partial class SeccionCamion : UserControl
{
    public SeccionCamion()
    {
        InitializeComponent();
    }
}
