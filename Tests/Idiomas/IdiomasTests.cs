using System.Globalization;
using System.Text.RegularExpressions;
using FleetManager.Idiomas;

namespace FleetManager.Tests.Idiomas;

/// <summary>
/// Los archivos de idioma: que estén completos, que encajen con el español y con el
/// código, y las reglas de plurales. No cambian el idioma de la aplicación (es global).
/// </summary>
public partial class IdiomasTests
{
    private static readonly IReadOnlyDictionary<string, string> Espanol = Textos.Cargar(Textos.IdiomaBase);

    public static TheoryData<string> OtrosIdiomas()
    {
        var datos = new TheoryData<string>();

        foreach (IdiomaApp idioma in Textos.Disponibles.Where(i => i.Codigo != Textos.IdiomaBase))
        {
            datos.Add(idioma.Codigo);
        }

        return datos;
    }

    [GeneratedRegex(@"\{\d+(:[^}]*)?\}")]
    private static partial Regex Huecos();

    [Fact]
    public void El_espanol_tiene_textos()
    {
        Assert.True(Espanol.Count > 300, $"Solo {Espanol.Count} textos");
    }

    [Theory]
    [MemberData(nameof(OtrosIdiomas))]
    public void Cada_idioma_tiene_los_mismos_textos_que_el_espanol(string idioma)
    {
        IReadOnlyDictionary<string, string> textos = Textos.Cargar(idioma);
        var claves = textos.Keys.Where(c => !c.EndsWith(".2_4")).ToHashSet();

        Assert.Empty(Espanol.Keys.Except(claves).Order()); // le faltan
        Assert.Empty(claves.Except(Espanol.Keys).Order()); // le sobran
    }

    [Theory]
    [MemberData(nameof(OtrosIdiomas))]
    public void Cada_texto_traducido_tiene_los_mismos_huecos(string idioma)
    {
        var distintos = Textos.Cargar(idioma)
            .Where(t => Espanol.TryGetValue(BaseDePlural(t.Key), out string? original) &&
                        !HuecosDe(t.Value).SequenceEqual(HuecosDe(original)))
            .Select(t => t.Key);

        Assert.Empty(distintos);
    }

    [Theory]
    [MemberData(nameof(OtrosIdiomas))]
    [InlineData("es")]
    public void Las_negritas_estan_bien_cerradas_y_los_dias_son_siete(string idioma)
    {
        IReadOnlyDictionary<string, string> textos = Textos.Cargar(idioma);

        Assert.Empty(textos.Where(t => Regex.Matches(t.Value, @"\*\*").Count % 2 != 0).Select(t => t.Key));
        Assert.Equal(7, textos["Fecha.DiasCortos"].Split(',').Length);
    }

    [Fact]
    public void El_polaco_tiene_la_forma_de_plural_de_2_a_4()
    {
        IReadOnlyDictionary<string, string> polaco = Textos.Cargar("pl");

        foreach (string clave in Espanol.Keys.Where(c => c.StartsWith("Plural.") && c.EndsWith(".1")))
        {
            Assert.True(polaco.ContainsKey(clave[..^2] + ".2_4"), $"Falta {clave[..^2]}.2_4 en polaco");
        }
    }

    [Theory]
    [InlineData("es", 1, "1")]
    [InlineData("es", 0, "N")]
    [InlineData("es", 2, "N")]
    [InlineData("fr", 0, "1")]
    [InlineData("pt", 1, "1")]
    [InlineData("pl", 3, "2_4")]
    [InlineData("pl", 5, "N")]
    [InlineData("pl", 13, "N")]
    [InlineData("pl", 22, "2_4")]
    public void Plurales_segun_el_idioma(string idioma, int cantidad, string forma)
    {
        Assert.Equal(forma, Textos.FormaPlural(idioma, cantidad));
    }

    [Theory]
    [InlineData("de-AT", "de")]
    [InlineData("pt-PT", "pt")]
    [InlineData("es-MX", "es")]
    [InlineData("ja-JP", "en")] // idioma que no tenemos: inglés
    public void El_idioma_de_windows_elige_el_de_la_aplicacion(string cultura, string esperado)
    {
        Assert.Equal(esperado, Textos.ElegirSegunWindows(CultureInfo.GetCultureInfo(cultura)));
    }

    [Fact]
    public void Las_claves_que_usa_el_codigo_existen_y_no_sobra_ninguna()
    {
        HashSet<string> usadas = ClavesUsadasEnElCodigo();

        Assert.Empty(usadas.Where(c => !Espanol.ContainsKey(c)).Order());           // usadas pero sin texto
        Assert.Empty(Espanol.Keys.Where(c => !usadas.Contains(c)).Order());         // con texto pero sin usar
    }

    /// <summary>Busca en el código todas las formas de usar un texto.</summary>
    private static HashSet<string> ClavesUsadasEnElCodigo()
    {
        string raiz = RaizDelProyecto();
        var archivos = Directory.EnumerateFiles(Path.Combine(raiz, "FleetManager"), "*.*", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(Path.Combine(raiz, "GeneradorMapa"), "*.*", SearchOption.AllDirectories))
            .Where(a => (a.EndsWith(".cs") || a.EndsWith(".xaml")) && !a.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));

        var usadas = new HashSet<string>();

        foreach (string archivo in archivos)
        {
            string codigo = File.ReadAllText(archivo);

            foreach (Match m in Regex.Matches(codigo, @"Textos\.T\(""([A-Za-z0-9_.]+)"""))
            {
                usadas.Add(m.Groups[1].Value);
            }

            foreach (Match m in Regex.Matches(codigo, @"Textos\.Plural\(""([A-Za-z0-9_.]+)"""))
            {
                usadas.Add(m.Groups[1].Value + ".1");
                usadas.Add(m.Groups[1].Value + ".N");
            }

            foreach (Match m in Regex.Matches(codigo, @"\{i:Texto ([A-Za-z0-9_.]+)\}|TextoRico\.Clave=""([A-Za-z0-9_.]+)"""))
            {
                usadas.Add(m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value);
            }

            // Claves escritas en listas o mandadas por el generador ("Csv.Dia", "Generador.Dibujando|...").
            foreach (Match m in Regex.Matches(codigo, @"""((?:Csv|Generador)\.[A-Za-z0-9_]+)[""|]"))
            {
                usadas.Add(m.Groups[1].Value);
            }
        }

        // Claves que se arman en el código: Edicion.Dia{Inicio|Fin}Mal, Edicion.Hora{Inicio|Fin}Mal.
        foreach (string cual in new[] { "Inicio", "Fin" })
        {
            usadas.Add($"Edicion.Dia{cual}Mal");
            usadas.Add($"Edicion.Hora{cual}Mal");
        }

        return usadas;
    }

    private static string RaizDelProyecto()
    {
        var carpeta = new DirectoryInfo(AppContext.BaseDirectory);

        while (carpeta is not null && !File.Exists(Path.Combine(carpeta.FullName, "FleetManager.slnx")))
        {
            carpeta = carpeta.Parent;
        }

        return carpeta?.FullName ?? throw new InvalidOperationException("No se encuentra la carpeta del proyecto.");
    }

    private static string BaseDePlural(string clave) => clave.EndsWith(".2_4") ? clave[..^4] + ".N" : clave;

    private static IEnumerable<string> HuecosDe(string texto) =>
        Huecos().Matches(texto).Select(m => m.Value).Order();
}
