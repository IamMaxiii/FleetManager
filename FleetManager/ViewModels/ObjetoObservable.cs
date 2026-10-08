using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FleetManager.ViewModels;

/// <summary>
/// Base de los ViewModels: avisa a la interfaz cuando cambia una propiedad, y
/// solo si el valor nuevo es distinto del anterior (así WPF solo redibuja lo
/// que cambia de verdad).
/// </summary>
public abstract class ObjetoObservable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <returns>Verdadero si el valor ha cambiado.</returns>
    protected bool Asignar<T>(ref T campo, T valor, [CallerMemberName] string? propiedad = null)
    {
        if (EqualityComparer<T>.Default.Equals(campo, valor))
        {
            return false;
        }

        campo = valor;
        Avisar(propiedad);
        return true;
    }

    protected void Avisar(string? propiedad) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propiedad));
}
