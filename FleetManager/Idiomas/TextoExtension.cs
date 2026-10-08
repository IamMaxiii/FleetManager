using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;

namespace FleetManager.Idiomas;

/// <summary>
/// En XAML: <c>Text="{i:Texto Menu.Inicio}"</c> pone el texto de esa clave en el
/// idioma actual.
/// </summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class TextoExtension : MarkupExtension
{
    public TextoExtension()
    {
    }

    public TextoExtension(string clave)
    {
        Clave = clave;
    }

    [ConstructorArgument("clave")]
    public string Clave { get; set; } = "";

    public override object ProvideValue(IServiceProvider serviceProvider) => Textos.T(Clave);
}

/// <summary>
/// Texto con partes en negrita: <c>i:TextoRico.Clave="Ayuda.Uso1"</c> en un TextBlock.
/// En el archivo del idioma, lo que va entre **dos asteriscos** sale en negrita y
/// entre *uno* en cursiva.
/// </summary>
public static partial class TextoRico
{
    public static readonly DependencyProperty ClaveProperty = DependencyProperty.RegisterAttached(
        "Clave", typeof(string), typeof(TextoRico), new PropertyMetadata(null, ClaveCambiada));

    public static string? GetClave(DependencyObject objeto) => (string?)objeto.GetValue(ClaveProperty);

    public static void SetClave(DependencyObject objeto, string? valor) => objeto.SetValue(ClaveProperty, valor);

    [GeneratedRegex(@"(\*\*[^*]+\*\*|\*[^*]+\*)")]
    private static partial Regex Marcas();

    /// <summary>Trozos del texto: (texto, negrita, cursiva).</summary>
    public static IEnumerable<(string Texto, bool Negrita, bool Cursiva)> Trozos(string texto)
    {
        foreach (string trozo in Marcas().Split(texto))
        {
            if (trozo.Length == 0)
            {
                continue;
            }

            if (trozo.StartsWith("**") && trozo.EndsWith("**") && trozo.Length > 4)
            {
                yield return (trozo[2..^2], true, false);
            }
            else if (trozo.StartsWith('*') && trozo.EndsWith('*') && trozo.Length > 2)
            {
                yield return (trozo[1..^1], false, true);
            }
            else
            {
                yield return (trozo, false, false);
            }
        }
    }

    private static void ClaveCambiada(DependencyObject objeto, DependencyPropertyChangedEventArgs e)
    {
        if (objeto is not TextBlock bloque || e.NewValue is not string clave)
        {
            return;
        }

        bloque.Inlines.Clear();

        foreach (var (texto, negrita, cursiva) in Trozos(Textos.T(clave)))
        {
            Inline parte = new Run(texto);

            if (negrita)
            {
                parte = new Bold(parte);
            }
            else if (cursiva)
            {
                parte = new Italic(parte);
            }

            bloque.Inlines.Add(parte);
        }
    }
}
