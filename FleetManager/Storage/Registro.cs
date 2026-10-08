using System.IO;
using System.Text;

namespace FleetManager.Storage;

/// <summary>
/// Registro de sucesos y errores en registro.log, para poder saber qué pasó
/// si algo falla. Cuando supera 1 MB se renombra a registro.anterior.log.
/// </summary>
public sealed class Registro
{
    private const long TamanoMaximo = 1024 * 1024;

    private readonly string ruta;
    private readonly object cerrojo = new();

    public Registro(string ruta)
    {
        this.ruta = ruta;
    }

    public void Info(string mensaje) => Escribir("INFO", mensaje, null);

    public void Aviso(string mensaje, Exception? error = null) => Escribir("AVISO", mensaje, error);

    public void Error(string mensaje, Exception? error = null) => Escribir("ERROR", mensaje, error);

    private void Escribir(string nivel, string mensaje, Exception? error)
    {
        var texto = new StringBuilder()
            .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
            .Append(" [").Append(nivel).Append("] ")
            .AppendLine(mensaje);

        if (error is not null)
        {
            texto.AppendLine("    " + error.ToString().Replace(Environment.NewLine, Environment.NewLine + "    "));
        }

        try
        {
            lock (cerrojo)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
                RotarSiHaceFalta();
                File.AppendAllText(ruta, texto.ToString(), Encoding.UTF8);
            }
        }
        catch (Exception)
        {
            // Si ni siquiera se puede escribir el registro, no hay dónde anotarlo.
            // Es el único sitio de la aplicación donde un error se ignora a propósito:
            // un fallo del registro nunca debe cerrar la aplicación.
        }
    }

    private void RotarSiHaceFalta()
    {
        var archivo = new FileInfo(ruta);

        if (archivo.Exists && archivo.Length > TamanoMaximo)
        {
            File.Move(ruta, Path.ChangeExtension(ruta, ".anterior.log"), overwrite: true);
        }
    }
}
