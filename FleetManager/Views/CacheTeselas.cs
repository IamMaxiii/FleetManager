using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace FleetManager.Views;

/// <summary>
/// Teselas del mapa ya cargadas en memoria (las últimas <see cref="Maximo"/>), compartidas
/// por todos los mapas de la aplicación. Se leen del disco en segundo plano para no
/// trabar la ventana; cuando una está lista se avisa para redibujar.
/// Las teselas que no existen (mar, zonas vacías) se recuerdan como vacías.
/// </summary>
public sealed class CacheTeselas
{
    public const int Maximo = 400;

    public static CacheTeselas Instancia { get; } = new();

    private readonly Dictionary<string, LinkedListNode<(string Ruta, BitmapSource? Imagen)>> porRuta = [];
    private readonly LinkedList<(string Ruta, BitmapSource? Imagen)> recientes = new();
    private readonly HashSet<string> cargando = [];

    /// <summary>
    /// Devuelve la tesela si ya está en memoria. Si no, empieza a cargarla y devuelve
    /// vacío; al terminar llama a <paramref name="alCargar"/> (en el hilo de la ventana).
    /// </summary>
    /// <param name="existe">Falso si se sabe que no hay tesela (no hay nada que dibujar ahí).</param>
    public BitmapSource? Obtener(string ruta, Action alCargar, out bool existe)
    {
        if (porRuta.TryGetValue(ruta, out var nodo))
        {
            recientes.Remove(nodo);
            recientes.AddFirst(nodo);
            existe = nodo.Value.Imagen is not null;
            return nodo.Value.Imagen;
        }

        existe = true;

        if (cargando.Add(ruta))
        {
            Dispatcher ventana = Dispatcher.CurrentDispatcher;

            Task.Run(() =>
            {
                BitmapSource? imagen = Leer(ruta);
                ventana.InvokeAsync(() =>
                {
                    cargando.Remove(ruta);
                    Guardar(ruta, imagen);
                    alCargar();
                });
            });
        }

        return null;
    }

    /// <summary>La tesela si ya está en memoria; no la carga si no lo está.</summary>
    public BitmapSource? Consultar(string ruta) =>
        porRuta.TryGetValue(ruta, out var nodo) ? nodo.Value.Imagen : null;

    private void Guardar(string ruta, BitmapSource? imagen)
    {
        if (porRuta.ContainsKey(ruta))
        {
            return;
        }

        porRuta[ruta] = recientes.AddFirst((ruta, imagen));

        while (recientes.Count > Maximo && recientes.Last is { } viejo)
        {
            porRuta.Remove(viejo.Value.Ruta);
            recientes.RemoveLast();
        }
    }

    private static BitmapSource? Leer(string ruta)
    {
        if (!File.Exists(ruta))
        {
            return null;
        }

        try
        {
            using var archivo = File.OpenRead(ruta);
            var imagen = new BitmapImage();
            imagen.BeginInit();
            imagen.CacheOption = BitmapCacheOption.OnLoad;
            imagen.StreamSource = archivo;
            imagen.EndInit();
            imagen.Freeze(); // para poder usarla en el hilo de la ventana
            return imagen;
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
