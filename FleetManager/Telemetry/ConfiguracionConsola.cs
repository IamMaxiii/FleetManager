using System.Text.RegularExpressions;

namespace FleetManager.Telemetry;

/// <summary>
/// Lee y cambia en el texto de config.cfg del juego las dos opciones que activan la
/// consola: <c>uset g_developer "1"</c> y <c>uset g_console "1"</c>. No toca nada más.
/// </summary>
public static class ConfiguracionConsola
{
    private static readonly string[] Opciones = ["g_developer", "g_console"];

    public static bool EstaActivada(string texto) => Opciones.All(opcion => Valor(texto, opcion) == "1");

    /// <summary>Devuelve el texto con la consola activada (añade las líneas si faltan).</summary>
    public static string Activar(string texto)
    {
        foreach (string opcion in Opciones)
        {
            var patron = new Regex($@"^(\s*uset\s+{opcion}\s+)""[^""]*""", RegexOptions.Multiline);

            if (patron.IsMatch(texto))
            {
                texto = patron.Replace(texto, "${1}\"1\"");
            }
            else
            {
                string salto = texto.Contains("\r\n") ? "\r\n" : "\n";

                if (texto.Length > 0 && !texto.EndsWith('\n'))
                {
                    texto += salto;
                }

                texto += $"uset {opcion} \"1\"{salto}";
            }
        }

        return texto;
    }

    private static string? Valor(string texto, string opcion)
    {
        Match coincidencia = Regex.Match(texto, $@"^\s*uset\s+{opcion}\s+""([^""]*)""", RegexOptions.Multiline);
        return coincidencia.Success ? coincidencia.Groups[1].Value : null;
    }
}
