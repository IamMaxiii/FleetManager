using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace FleetManager.Idiomas;

/// <summary>Un idioma de la aplicación: código ("es"), nombre en ese idioma y cultura para números.</summary>
public sealed record IdiomaApp(string Codigo, string Nombre, string Cultura)
{
    /// <summary>Así se ve en el desplegable de idiomas.</summary>
    public override string ToString() => Nombre;
}

/// <summary>
/// Textos de la aplicación en el idioma elegido. Cada idioma es un archivo
/// Idiomas\&lt;código&gt;.json (clave → texto) incluido dentro del programa. Si a un
/// idioma le falta un texto se usa el español; si tampoco existe, la clave.
///
/// El idioma se elige una vez al arrancar (<see cref="Usar"/>); cambiarlo reinicia la
/// aplicación, así que no hace falta avisar a la interfaz de cambios.
/// </summary>
public static class Textos
{
    public const string IdiomaBase = "es";

    /// <summary>Si Windows está en un idioma que no tenemos, se usa este.</summary>
    public const string IdiomaPorDefecto = "en";

    public static readonly IReadOnlyList<IdiomaApp> Disponibles =
    [
        new("es", "Español", "es-ES"),
        new("en", "English", "en-GB"),
        new("de", "Deutsch", "de-DE"),
        new("fr", "Français", "fr-FR"),
        new("it", "Italiano", "it-IT"),
        new("pt", "Português", "pt-BR"),
        new("pl", "Polski", "pl-PL"),
        new("nl", "Nederlands", "nl-NL"),
        new("tr", "Türkçe", "tr-TR")
    ];

    private static IReadOnlyDictionary<string, string> textos = Cargar(IdiomaBase);
    private static IReadOnlyDictionary<string, string> textosBase = textos;

    public static IdiomaApp Actual { get; private set; } = Disponibles[0];

    /// <summary>Cultura del idioma actual, para escribir números y decimales.</summary>
    public static CultureInfo Cultura { get; private set; } = CultureInfo.GetCultureInfo(Disponibles[0].Cultura);

    /// <summary>Pasa a usar ese idioma (si no existe, el idioma por defecto).</summary>
    public static void Usar(string? codigo)
    {
        IdiomaApp idioma = Buscar(codigo) ?? Buscar(IdiomaPorDefecto)!;
        textosBase = Cargar(IdiomaBase);
        textos = idioma.Codigo == IdiomaBase ? textosBase : Cargar(idioma.Codigo);
        Actual = idioma;
        Cultura = CultureInfo.GetCultureInfo(idioma.Cultura);
    }

    /// <summary>El idioma de la aplicación que corresponde a una cultura de Windows ("de-AT" → "de").</summary>
    public static string ElegirSegunWindows(CultureInfo culturaWindows) =>
        Buscar(culturaWindows.TwoLetterISOLanguageName)?.Codigo ?? IdiomaPorDefecto;

    public static IdiomaApp? Buscar(string? codigo) =>
        Disponibles.FirstOrDefault(i => string.Equals(i.Codigo, codigo, StringComparison.OrdinalIgnoreCase));

    /// <summary>El texto de esa clave en el idioma actual.</summary>
    public static string T(string clave) =>
        textos.TryGetValue(clave, out string? texto) || textosBase.TryGetValue(clave, out texto) ? texto : clave;

    /// <summary>El texto de esa clave con sus huecos {0}, {1}... rellenados.</summary>
    public static string T(string clave, params object?[] valores) => string.Format(Cultura, T(clave), valores);

    /// <summary>
    /// Cantidad con su palabra en singular o plural ("1 trayecto", "3 trayectos").
    /// Usa las claves &lt;clave&gt;.1 (singular) y &lt;clave&gt;.N (plural), con {0} para
    /// el número; en polaco también &lt;clave&gt;.2_4 ("2 trasy", "5 tras").
    /// </summary>
    public static string Plural(string clave, int cantidad) => T($"{clave}.{FormaPlural(Actual.Codigo, cantidad)}", cantidad);

    /// <summary>Qué forma de la palabra lleva un número en un idioma: "1", "2_4" o "N".</summary>
    public static string FormaPlural(string idioma, int cantidad)
    {
        int n = Math.Abs(cantidad);

        return idioma switch
        {
            // En francés y portugués el 0 también va en singular ("0 trajet").
            "fr" or "pt" => n <= 1 ? "1" : "N",
            // En polaco: 1 trasa, 2-4 trasy (salvo 12-14), 5 tras.
            "pl" => n == 1 ? "1" : n % 10 is >= 2 and <= 4 && n % 100 is < 12 or > 14 ? "2_4" : "N",
            _ => n == 1 ? "1" : "N"
        };
    }

    /// <summary>Lee los textos de un idioma (las pruebas lo usan para comparar idiomas).</summary>
    public static IReadOnlyDictionary<string, string> Cargar(string codigo)
    {
        using Stream? archivo = Assembly.GetExecutingAssembly().GetManifestResourceStream($"Idiomas.{codigo}.json");

        if (archivo is null)
        {
            return new Dictionary<string, string>();
        }

        return JsonSerializer.Deserialize<Dictionary<string, string>>(archivo) ?? [];
    }
}
