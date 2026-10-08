using System.Windows.Input;

namespace FleetManager.ViewModels;

/// <summary>
/// Acción que un botón puede ejecutar mediante binding (Command="{Binding ...}").
/// </summary>
public sealed class Comando : ICommand
{
    private readonly Action ejecutar;
    private readonly Func<bool>? sePuedeEjecutar;

    public Comando(Action ejecutar, Func<bool>? sePuedeEjecutar = null)
    {
        this.ejecutar = ejecutar;
        this.sePuedeEjecutar = sePuedeEjecutar;
    }

    // WPF vuelve a preguntar si el botón está activo cuando el usuario interactúa
    // o cuando alguien llama a CommandManager.InvalidateRequerySuggested().
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => sePuedeEjecutar?.Invoke() ?? true;

    public void Execute(object? parameter) => ejecutar();
}
