namespace FleetManager.Tests.Utilidades;

/// <summary>
/// Carpeta vacía y exclusiva para una prueba. Se borra al terminar.
/// </summary>
public sealed class CarpetaTemporal : IDisposable
{
    public CarpetaTemporal()
    {
        Ruta = Path.Combine(Path.GetTempPath(), "FleetManager.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Ruta);
    }

    public string Ruta { get; }

    public string Archivo(string nombre) => Path.Combine(Ruta, nombre);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Ruta, recursive: true);
        }
        catch (IOException)
        {
            // Si Windows aún tiene algún archivo abierto, la carpeta temporal se queda; no afecta a la prueba.
        }
    }
}
