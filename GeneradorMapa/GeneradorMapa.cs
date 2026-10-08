using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text.Json;
using TsMap;
using TsMap.TsItem;

namespace FleetManager.MapaJuego;

/// <summary>Cómo va la generación del mapa.</summary>
public sealed record ProgresoMapa(string Texto, double Fraccion);

/// <summary>
/// Genera el mapa del juego en teselas PNG de 256 px (zoom 3 a 9) a partir de los
/// archivos del juego, con la biblioteca ts-map (MIT, en ext/ts-map).
///
/// - Solo se dibujan las teselas que tienen algo (carreteras, cruces, zonas o ferris):
///   el mar y las zonas vacías se pintan con el color de fondo.
/// - Los nombres de ciudad no van en las teselas (se cortarían en los bordes): se
///   guardan en ciudades.json y FleetManager los dibuja encima.
/// - Todo se escribe en una carpeta temporal que solo se da por buena al terminar,
///   así que un corte a medias no deja un mapa roto.
/// - Se ejecuta en un programa aparte (FleetManager.GeneradorMapa.exe): ts-map ocupa
///   unos 600 MB y deja abiertos los archivos del juego hasta que el programa termina,
///   así que al cerrarse el generador Windows lo libera todo.
/// </summary>
public static class GeneradorMapa
{
    public const int VersionFormato = 1;
    public const int ZoomMinimo = 3;
    public const int ZoomMaximo = 9;
    public const int TamanoTesela = 256;

    public const string ArchivoInfo = "info.json";
    public const string ArchivoCiudades = "ciudades.json";

    /// <summary>Colores del mapa (fondo gris oscuro y carreteras claras, como el mapa del juego).</summary>
    public static readonly Color ColorFondo = Color.FromArgb(0x2B, 0x2F, 0x33);

    // Holgura alrededor de cada punto con contenido (ancho de carretera, curvas...).
    private const double Holgura = 40;

    public static void Generar(string carpetaJuego, string carpetaDestino, string huella, IProgress<ProgresoMapa> progreso, CancellationToken cancelar)
    {
        string temporal = carpetaDestino + ".generando";

        if (Directory.Exists(temporal))
        {
            Directory.Delete(temporal, recursive: true);
        }

        Directory.CreateDirectory(temporal);
        progreso.Report(new ProgresoMapa("Leyendo el mapa del juego…", 0));

        var mapa = new TsMapper(carpetaJuego, new List<Mod>());
        mapa.Parse();
        cancelar.ThrowIfCancellationRequested();

        double lado = Math.Max(mapa.maxX - mapa.minX, mapa.maxZ - mapa.minZ);
        var info = new InfoMapaJuego(VersionFormato, mapa.minX, mapa.minZ, lado, ZoomMinimo, ZoomMaximo, TamanoTesela, huella);

        File.WriteAllText(Path.Combine(temporal, ArchivoCiudades), JsonSerializer.Serialize(Ciudades(mapa)));

        // Qué teselas tienen algo que dibujar, en cada zoom.
        progreso.Report(new ProgresoMapa("Calculando qué zonas dibujar…", 0));
        var (puntos, tramos) = Contenido(mapa);
        var porZoom = new Dictionary<int, HashSet<(int X, int Y)>>();

        for (int zoom = ZoomMinimo; zoom <= ZoomMaximo; zoom++)
        {
            porZoom[zoom] = TeselasConContenido(info, zoom, puntos, tramos);
        }

        int total = porZoom.Values.Sum(t => t.Count);
        int hechas = 0;

        var paleta = Paleta();
        var dibujante = new TsMapRenderer(mapa);
        var opciones = RenderFlags.All & ~RenderFlags.TextOverlay & ~RenderFlags.SecretRoads
                       & ~RenderFlags.BusStopOverlay & ~RenderFlags.CityNames;

        foreach (var (zoom, teselas) in porZoom)
        {
            Directory.CreateDirectory(Path.Combine(temporal, zoom.ToString()));
            float escala = (float)TeselasMapa.Escala(info, zoom);

            foreach (var (x, y) in teselas)
            {
                cancelar.ThrowIfCancellationRequested();

                using var imagen = new Bitmap(TamanoTesela, TamanoTesela);
                using (var g = Graphics.FromImage(imagen))
                {
                    ZonaMundo zona = TeselasMapa.ZonaDe(info, zoom, x, y);
                    dibujante.Render(g, new Rectangle(0, 0, TamanoTesela, TamanoTesela), escala, new PointF((float)zona.X, (float)zona.Z), paleta, opciones);
                }

                imagen.Save(TeselasMapa.RutaTesela(temporal, zoom, x, y), ImageFormat.Png);
                hechas++;

                if (hechas % 50 == 0 || hechas == total)
                {
                    progreso.Report(new ProgresoMapa($"Dibujando el mapa: {hechas:N0} de {total:N0} teselas", (double)hechas / total));
                }
            }
        }

        // info.json se escribe lo último: su presencia indica que el mapa está completo.
        File.WriteAllText(Path.Combine(temporal, ArchivoInfo), JsonSerializer.Serialize(info));

        if (Directory.Exists(carpetaDestino))
        {
            Directory.Delete(carpetaDestino, recursive: true);
        }

        Directory.Move(temporal, carpetaDestino);
    }

    private static MapPalette Paleta() => new()
    {
        Background = new SolidBrush(ColorFondo),
        Road = new SolidBrush(Color.FromArgb(0xD0, 0xD0, 0xD0)),
        PrefabRoad = new SolidBrush(Color.FromArgb(0xD0, 0xD0, 0xD0)),
        PrefabLight = new SolidBrush(Color.FromArgb(0x3C, 0x41, 0x47)),
        PrefabDark = new SolidBrush(Color.FromArgb(0x34, 0x38, 0x3D)),
        PrefabGreen = new SolidBrush(Color.FromArgb(0x3C, 0x41, 0x47)),
        FerryLines = new SolidBrush(Color.FromArgb(0x4F, 0xA3, 0xE0)),
        CityName = new SolidBrush(Color.White),
        Error = new SolidBrush(Color.Red)
    };

    /// <summary>Nombres de las ciudades (en español si el juego los tiene) y su posición.</summary>
    private static List<CiudadMapa> Ciudades(TsMapper mapa)
    {
        var ciudades = new List<CiudadMapa>();
        var grupos = new HashSet<string>();

        foreach (TsCityItem ciudad in mapa.Cities.Where(c => c.City is not null && !c.Hidden))
        {
            if (ciudad.City.Group is { } grupo && !grupos.Add(grupo))
            {
                continue; // varias zonas de la misma ciudad: un solo nombre
            }

            string nombre = mapa.Localization.GetLocaleValue(ciudad.City.LocalizationToken, "es_es")
                            ?? mapa.Localization.GetLocaleValue(ciudad.City.LocalizationToken)
                            ?? ciudad.City.Name;
            TsNode? nodo = mapa.GetNodeByUid(ciudad.NodeUid);

            ciudades.Add(new CiudadMapa(nombre, nodo?.X ?? ciudad.X, nodo?.Z ?? ciudad.Z));
        }

        return ciudades;
    }

    /// <summary>Puntos y tramos con algo que dibujar: carreteras, cruces, zonas del mapa y ferris.</summary>
    private static (List<(double X, double Z)> Puntos, List<(double X1, double Z1, double X2, double Z2)> Tramos) Contenido(TsMapper mapa)
    {
        var puntos = new List<(double, double)>();
        var tramos = new List<(double, double, double, double)>();

        foreach (TsRoadItem carretera in mapa.Roads.Where(r => !r.IsSecret))
        {
            if (carretera.GetStartNode() is { } inicio && carretera.GetEndNode() is { } fin)
            {
                tramos.Add((inicio.X, inicio.Z, fin.X, fin.Z));
            }
        }

        foreach (TsPrefabItem cruce in mapa.Prefabs.Where(p => !p.IsSecret))
        {
            puntos.Add((cruce.X, cruce.Z));
            AnadirNodos(mapa, cruce.Nodes, puntos, null);
        }

        foreach (TsMapAreaItem zona in mapa.MapAreas)
        {
            AnadirNodos(mapa, zona.Nodes, puntos, tramos);
        }

        foreach (TsFerryItem puerto in mapa.FerryConnections)
        {
            foreach (TsFerryConnection linea in mapa.LookupFerryConnection(puerto.FerryPortId))
            {
                var recorrido = new List<(double X, double Z)> { (linea.StartPortLocation.X, linea.StartPortLocation.Y) };
                recorrido.AddRange(linea.Connections.Select(c => ((double)c.X, (double)c.Z)));
                recorrido.Add((linea.EndPortLocation.X, linea.EndPortLocation.Y));

                for (int i = 1; i < recorrido.Count; i++)
                {
                    tramos.Add((recorrido[i - 1].X, recorrido[i - 1].Z, recorrido[i].X, recorrido[i].Z));
                }
            }
        }

        return (puntos, tramos);
    }

    private static void AnadirNodos(TsMapper mapa, List<ulong>? nodos, List<(double, double)> puntos, List<(double, double, double, double)>? tramos)
    {
        if (nodos is null)
        {
            return;
        }

        TsNode? anterior = null;

        foreach (ulong uid in nodos)
        {
            if (mapa.GetNodeByUid(uid) is not { } nodo)
            {
                continue;
            }

            puntos.Add((nodo.X, nodo.Z));

            if (tramos is not null && anterior is not null)
            {
                tramos.Add((anterior.X, anterior.Z, nodo.X, nodo.Z));
            }

            anterior = nodo;
        }
    }

    /// <summary>Teselas de un zoom que tocan algún punto o tramo (con una pequeña holgura).</summary>
    public static HashSet<(int X, int Y)> TeselasConContenido(
        InfoMapaJuego info,
        int zoom,
        IEnumerable<(double X, double Z)> puntos,
        IEnumerable<(double X1, double Z1, double X2, double Z2)> tramos)
    {
        var teselas = new HashSet<(int, int)>();
        double paso = TeselasMapa.UnidadesPorTesela(info, zoom) / 4;

        void Marcar(double x, double z)
        {
            foreach (var tesela in TeselasMapa.TeselasEn(info, zoom, new ZonaMundo(x - Holgura, z - Holgura, 2 * Holgura, 2 * Holgura)))
            {
                teselas.Add(tesela);
            }
        }

        foreach (var (x, z) in puntos)
        {
            Marcar(x, z);
        }

        foreach (var (x1, z1, x2, z2) in tramos)
        {
            double longitud = Math.Sqrt((x2 - x1) * (x2 - x1) + (z2 - z1) * (z2 - z1));
            int pasos = Math.Max(1, (int)Math.Ceiling(longitud / paso));

            for (int i = 0; i <= pasos; i++)
            {
                double t = (double)i / pasos;
                Marcar(x1 + (x2 - x1) * t, z1 + (z2 - z1) * t);
            }
        }

        return teselas;
    }
}
