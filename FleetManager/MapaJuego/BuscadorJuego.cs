using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace FleetManager.MapaJuego;

/// <summary>
/// Encuentra la carpeta de Euro Truck Simulator 2 (a través de Steam) y calcula una
/// "huella" de la versión del juego y sus archivos, para saber si hay que volver a
/// generar el mapa (al actualizar el juego o comprar un DLC de mapa).
/// </summary>
public static class BuscadorJuego
{
    private const string CarpetaEnSteam = @"steamapps\common\Euro Truck Simulator 2";
    private const string Ejecutable = @"bin\win_x64\eurotrucks2.exe";

    /// <summary>Carpeta del juego, o vacío si no se encuentra.</summary>
    public static string? Buscar()
    {
        string? steam = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string;

        if (string.IsNullOrEmpty(steam))
        {
            return null;
        }

        steam = steam.Replace('/', '\\');
        var bibliotecas = new List<string> { steam };
        string vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");

        if (File.Exists(vdf))
        {
            bibliotecas.AddRange(LeerBibliotecas(File.ReadAllText(vdf)));
        }

        return bibliotecas
            .Select(b => Path.Combine(b, CarpetaEnSteam))
            .FirstOrDefault(c => File.Exists(Path.Combine(c, Ejecutable)));
    }

    /// <summary>Carpetas de bibliotecas de Steam que aparecen en libraryfolders.vdf.</summary>
    public static IReadOnlyList<string> LeerBibliotecas(string textoVdf) =>
        Regex.Matches(textoVdf, "\"path\"\\s+\"([^\"]+)\"")
            .Select(m => m.Groups[1].Value.Replace(@"\\", @"\"))
            .ToList();

    /// <summary>
    /// Huella corta de la versión del juego y de sus archivos .scs (nombre y tamaño).
    /// Cambia al actualizar el juego o al instalar o quitar un DLC.
    /// </summary>
    public static string Huella(string carpetaJuego)
    {
        var texto = new StringBuilder();
        string exe = Path.Combine(carpetaJuego, Ejecutable);

        if (File.Exists(exe))
        {
            texto.Append(System.Diagnostics.FileVersionInfo.GetVersionInfo(exe).FileVersion).Append('|');
        }

        foreach (FileInfo archivo in new DirectoryInfo(carpetaJuego).GetFiles("*.scs").OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase))
        {
            texto.Append(archivo.Name.ToLowerInvariant()).Append(':').Append(archivo.Length).Append('|');
        }

        byte[] resumen = SHA256.HashData(Encoding.UTF8.GetBytes(texto.ToString()));
        return Convert.ToHexString(resumen)[..12].ToLowerInvariant();
    }
}
