using System.Windows.Controls;

namespace FleetManager.Views;

/// <summary>
/// Sección "Conductor" de la ventana principal. Sin lógica: todo viene del
/// ConductorViewModel mediante bindings.
/// </summary>
public partial class SeccionConductor : UserControl
{
    public SeccionConductor()
    {
        InitializeComponent();
    }
}
