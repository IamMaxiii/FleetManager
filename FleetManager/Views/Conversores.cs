using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FleetManager.Views;

/// <summary>
/// Verdadero si el valor enlazado es igual al parámetro. Sirve para marcar el
/// elemento del menú lateral de la sección actual; al pulsarlo, cambia la sección.
/// </summary>
public sealed class IgualAParametro : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Equals(value, parameter);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? parameter : Binding.DoNothing;
}

/// <summary>
/// Visible si el valor enlazado es igual al parámetro; si no, oculto. Sirve para
/// mostrar solo la sección elegida en el menú lateral.
/// </summary>
public sealed class VisibleSiIgual : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Equals(value, parameter) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Visible si el número enlazado es cero (por ejemplo, para mostrar "Ninguna" en una lista vacía).</summary>
public sealed class CeroAVisible : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Visible si el valor enlazado es falso; oculto si es verdadero.</summary>
public sealed class InversoAVisibilidad : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Oculto si el valor enlazado es nulo; visible si tiene algo.</summary>
public sealed class NuloAOculto : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Marca hecha (✓) o pendiente (○).</summary>
public sealed class MarcaHecha : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? "✓" : "○";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
