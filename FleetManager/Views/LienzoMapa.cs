using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using FleetManager.MapaJuego;
using FleetManager.Models;
using FleetManager.ViewModels;

namespace FleetManager.Views;

/// <summary>
/// Mapa interactivo: teselas del mapa del juego (si está preparado), nombres de las
/// ciudades, recorrido de una jornada (con una parte resaltada, si se pide) y el camión
/// como una flecha con su rumbo. Rueda del ratón = acercar o alejar; arrastrar = mover
/// el mapa (y deja de seguir al camión).
///
/// El recorrido se convierte en figuras una sola vez y solo se rehacen cuando cambia
/// <see cref="VersionRecorrido"/> o el tramo resaltado; en cada lectura solo se mueve la vista.
/// </summary>
public sealed class LienzoMapa : FrameworkElement
{
    private static readonly Brush FondoSinMapa = Congelar(new SolidColorBrush(Color.FromRgb(0x16, 0x16, 0x16)));
    private static readonly Brush FondoMapa = Congelar(new SolidColorBrush(Color.FromRgb(0x2B, 0x2F, 0x33)));
    private static readonly Brush ColorRecorrido = Congelar(new SolidColorBrush(Color.FromRgb(0x1E, 0x88, 0xE5)));
    private static readonly Brush ColorRecorridoTenue = Congelar(new SolidColorBrush(Color.FromArgb(0x70, 0x1E, 0x88, 0xE5)));
    private static readonly Brush ColorResaltado = Congelar(new SolidColorBrush(Color.FromRgb(0xFF, 0xA7, 0x26)));
    private static readonly Brush ColorCamion = Congelar(new SolidColorBrush(Colors.White));
    private static readonly Brush ColorTexto = Congelar(new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xA0)));
    private static readonly Brush ColorCiudad = Congelar(new SolidColorBrush(Colors.White));
    private static readonly Brush SombraCiudad = Congelar(new SolidColorBrush(Color.FromArgb(0xC0, 0, 0, 0)));
    private static readonly Pen BordeCamion = Congelar(new Pen(new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28)), 2));
    private static readonly Pen LineaHastaCamion = Congelar(new Pen(new SolidColorBrush(Color.FromRgb(0x1E, 0x88, 0xE5)), 3) { DashStyle = DashStyles.Dot });
    private static readonly Typeface LetraCiudad = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

    // Flecha del camión apuntando hacia arriba (norte), centrada en (0, 0).
    private static readonly Geometry Flecha = Congelar(Geometry.Parse("M 0,-13 L 9,9 L 0,4 L -9,9 Z"));

    /// <summary>Por debajo de esta escala (muy alejado) no se dibujan nombres de ciudad.</summary>
    private const double EscalaMinimaCiudades = 0.0015;

    public static readonly DependencyProperty RecorridoProperty = Registrar<MapaRecorrido?>(nameof(Recorrido), null, RecorridoCambiado);
    public static readonly DependencyProperty VersionRecorridoProperty = Registrar(nameof(VersionRecorrido), 0, RecorridoCambiado);
    public static readonly DependencyProperty ResaltadoDesdeProperty = Registrar(nameof(ResaltadoDesde), -1, RecorridoCambiado);
    public static readonly DependencyProperty ResaltadoHastaProperty = Registrar(nameof(ResaltadoHasta), -1, RecorridoCambiado);
    public static readonly DependencyProperty EncuadrarRecorridoProperty = Registrar(nameof(EncuadrarRecorrido), false, RecorridoCambiado);
    public static readonly DependencyProperty MapaJuegoProperty = Registrar<MapaJuegoViewModel?>(nameof(MapaJuego), null, null);
    public static readonly DependencyProperty VersionMapaJuegoProperty = Registrar(nameof(VersionMapaJuego), 0, null);
    public static readonly DependencyProperty HayPosicionProperty = Registrar(nameof(HayPosicion), false, null);
    public static readonly DependencyProperty PosicionXProperty = Registrar(nameof(PosicionX), 0.0, null);
    public static readonly DependencyProperty PosicionZProperty = Registrar(nameof(PosicionZ), 0.0, null);
    public static readonly DependencyProperty RumboProperty = Registrar(nameof(Rumbo), 0.0, null);

    public static readonly DependencyProperty SeguirCamionProperty = DependencyProperty.Register(
        nameof(SeguirCamion), typeof(bool), typeof(LienzoMapa),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    private readonly VistaMapa vista = new();
    private readonly Dictionary<string, FormattedText> textosCiudades = [];
    private Geometry? recorridoCompleto;
    private Geometry? recorridoResaltado;
    private Rect zonaResaltada = Rect.Empty;
    private bool recorridoAlDia;
    private bool encuadrePendiente;
    private Point? arrastreDesde;
    private bool centradoInicial;

    public LienzoMapa()
    {
        ClipToBounds = true;
        Focusable = false;
    }

    public MapaRecorrido? Recorrido { get => (MapaRecorrido?)GetValue(RecorridoProperty); set => SetValue(RecorridoProperty, value); }

    public int VersionRecorrido { get => (int)GetValue(VersionRecorridoProperty); set => SetValue(VersionRecorridoProperty, value); }

    /// <summary>Minuto de la jornada desde el que resaltar el recorrido (-1 = sin resaltado).</summary>
    public int ResaltadoDesde { get => (int)GetValue(ResaltadoDesdeProperty); set => SetValue(ResaltadoDesdeProperty, value); }

    /// <summary>Minuto de la jornada hasta el que resaltar el recorrido (-1 = sin resaltado).</summary>
    public int ResaltadoHasta { get => (int)GetValue(ResaltadoHastaProperty); set => SetValue(ResaltadoHastaProperty, value); }

    /// <summary>Al cambiar el recorrido (o lo resaltado), encuadrarlo (para el historial).</summary>
    public bool EncuadrarRecorrido { get => (bool)GetValue(EncuadrarRecorridoProperty); set => SetValue(EncuadrarRecorridoProperty, value); }

    public MapaJuegoViewModel? MapaJuego { get => (MapaJuegoViewModel?)GetValue(MapaJuegoProperty); set => SetValue(MapaJuegoProperty, value); }

    public int VersionMapaJuego { get => (int)GetValue(VersionMapaJuegoProperty); set => SetValue(VersionMapaJuegoProperty, value); }

    public bool HayPosicion { get => (bool)GetValue(HayPosicionProperty); set => SetValue(HayPosicionProperty, value); }

    public double PosicionX { get => (double)GetValue(PosicionXProperty); set => SetValue(PosicionXProperty, value); }

    public double PosicionZ { get => (double)GetValue(PosicionZProperty); set => SetValue(PosicionZProperty, value); }

    public double Rumbo { get => (double)GetValue(RumboProperty); set => SetValue(RumboProperty, value); }

    public bool SeguirCamion { get => (bool)GetValue(SeguirCamionProperty); set => SetValue(SeguirCamionProperty, value); }

    private bool HayResaltado => ResaltadoDesde >= 0 && ResaltadoHasta >= ResaltadoDesde;

    /// <summary>Acerca (true) o aleja (false) desde el centro.</summary>
    public void Acercar(bool mas)
    {
        vista.Acercar(mas ? 1.5 : 1 / 1.5, new Point(RenderSize.Width / 2, RenderSize.Height / 2), RenderSize);
        InvalidateVisual();
    }

    /// <summary>Encuadra todo el recorrido (y el camión).</summary>
    public void VerTodo()
    {
        Rect zona = Recorrido is null ? Rect.Empty : VistaMapa.Limites(Recorrido);

        if (HayPosicion)
        {
            zona.Union(new Point(PosicionX, PosicionZ));
        }

        if (zona.IsEmpty && MapaJuego?.Info is { } info)
        {
            zona = new Rect(info.MinX, info.MinZ, info.Lado, info.Lado); // sin recorrido: todo el mapa
        }

        SetCurrentValue(SeguirCamionProperty, false);
        vista.Encuadrar(Ampliar(zona), RenderSize);
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dibujo)
    {
        Size tamano = RenderSize;
        bool hayMapa = MapaJuego?.Disponible == true;
        dibujo.DrawRectangle(hayMapa ? FondoMapa : FondoSinMapa, null, new Rect(tamano));

        if (!recorridoAlDia)
        {
            CrearRecorrido();
        }

        if (encuadrePendiente && tamano.Width > 0)
        {
            Rect zona = HayResaltado && !zonaResaltada.IsEmpty ? zonaResaltada : Recorrido is null ? Rect.Empty : VistaMapa.Limites(Recorrido);
            vista.Encuadrar(Ampliar(zona), tamano);
            encuadrePendiente = false;
        }
        else if (HayPosicion && (SeguirCamion || !centradoInicial))
        {
            vista.Centro = new Point(PosicionX, PosicionZ);
            centradoInicial = true;
        }

        if (hayMapa)
        {
            DibujarTeselas(dibujo, tamano);
        }

        DibujarRecorrido(dibujo, tamano);

        if (hayMapa)
        {
            DibujarCiudades(dibujo, tamano);
        }

        if (HayPosicion)
        {
            DibujarCamion(dibujo, tamano);
        }
        else if (!hayMapa && recorridoCompleto is null)
        {
            Texto(dibujo, "Conduce con la jornada abierta para ir dibujando el recorrido.", new Point(tamano.Width / 2, tamano.Height / 2), ColorTexto, centrado: true);
        }
    }

    // ---------- Ratón ----------

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        // Siguiendo al camión se acerca hacia el centro (donde está él); si no, hacia el ratón.
        Point punto = SeguirCamion && HayPosicion ? new Point(RenderSize.Width / 2, RenderSize.Height / 2) : e.GetPosition(this);
        vista.Acercar(e.Delta > 0 ? 1.25 : 0.8, punto, RenderSize);
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        arrastreDesde = e.GetPosition(this);
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (arrastreDesde is not { } desde || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        Point ahora = e.GetPosition(this);
        Vector movimiento = ahora - desde;

        if (movimiento.Length < 1)
        {
            return;
        }

        SetCurrentValue(SeguirCamionProperty, false);
        vista.Desplazar(movimiento);
        arrastreDesde = ahora;
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        arrastreDesde = null;
        ReleaseMouseCapture();
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        InvalidateVisual();
    }

    // ---------- Dibujo ----------

    private void DibujarTeselas(DrawingContext dibujo, Size tamano)
    {
        if (MapaJuego is not { Info: { } info, CarpetaTeselas: { } carpeta })
        {
            return;
        }

        int zoom = TeselasMapa.ZoomPara(info, vista.Escala);
        Point esquina = vista.AMundo(new Point(0, 0), tamano);
        Point opuesta = vista.AMundo(new Point(tamano.Width, tamano.Height), tamano);
        var visible = new ZonaMundo(esquina.X, esquina.Y, opuesta.X - esquina.X, opuesta.Y - esquina.Y);

        foreach (var (x, y) in TeselasMapa.TeselasEn(info, zoom, visible))
        {
            Rect destino = Pantalla(TeselasMapa.ZonaDe(info, zoom, x, y), tamano);
            var imagen = CacheTeselas.Instancia.Obtener(TeselasMapa.RutaTesela(carpeta, zoom, x, y), InvalidateVisual, out bool existe);

            if (imagen is not null)
            {
                dibujo.DrawImage(imagen, destino);
            }
            else if (existe)
            {
                DibujarTeselaProvisional(dibujo, tamano, info, carpeta, zoom, x, y, destino);
            }
        }
    }

    /// <summary>Mientras carga una tesela, enseña ampliada la de menos detalle que ya esté en memoria.</summary>
    private void DibujarTeselaProvisional(DrawingContext dibujo, Size tamano, InfoMapaJuego info, string carpeta, int zoom, int x, int y, Rect destino)
    {
        for (int arriba = 1; arriba <= 3 && zoom - arriba >= info.ZoomMinimo; arriba++)
        {
            int zoomPadre = zoom - arriba;
            int xPadre = x >> arriba;
            int yPadre = y >> arriba;

            if (CacheTeselas.Instancia.Consultar(TeselasMapa.RutaTesela(carpeta, zoomPadre, xPadre, yPadre)) is { } padre)
            {
                dibujo.PushClip(new RectangleGeometry(destino));
                dibujo.DrawImage(padre, Pantalla(TeselasMapa.ZonaDe(info, zoomPadre, xPadre, yPadre), tamano));
                dibujo.Pop();
                return;
            }
        }
    }

    private void DibujarRecorrido(DrawingContext dibujo, Size tamano)
    {
        if (recorridoCompleto is null)
        {
            return;
        }

        dibujo.PushTransform(new MatrixTransform(vista.Transformacion(tamano)));

        // Grosor en píxeles de pantalla, sea cual sea el zoom.
        dibujo.DrawGeometry(null, Linea(HayResaltado ? ColorRecorridoTenue : ColorRecorrido, 3), recorridoCompleto);

        if (recorridoResaltado is not null)
        {
            dibujo.DrawGeometry(null, Linea(ColorResaltado, 5), recorridoResaltado);
        }

        dibujo.Pop();
    }

    private void DibujarCiudades(DrawingContext dibujo, Size tamano)
    {
        if (vista.Escala < EscalaMinimaCiudades || MapaJuego?.Ciudades is not { Count: > 0 } ciudades)
        {
            return;
        }

        var ocupado = new List<Rect>();
        double ppp = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        foreach (CiudadMapa ciudad in ciudades)
        {
            Point punto = vista.APantalla(new Point(ciudad.X, ciudad.Z), tamano);

            if (punto.X < -150 || punto.Y < -50 || punto.X > tamano.Width + 150 || punto.Y > tamano.Height + 50)
            {
                continue;
            }

            if (!textosCiudades.TryGetValue(ciudad.Nombre, out FormattedText? texto))
            {
                texto = new FormattedText(ciudad.Nombre, CultureInfo.GetCultureInfo("es-ES"), FlowDirection.LeftToRight, LetraCiudad, 14, ColorCiudad, ppp);
                textosCiudades[ciudad.Nombre] = texto;
            }

            var zona = new Rect(punto.X - texto.Width / 2, punto.Y - texto.Height / 2, texto.Width, texto.Height);

            if (ocupado.Any(r => r.IntersectsWith(zona)))
            {
                continue; // no pisar otro nombre
            }

            ocupado.Add(zona);
            dibujo.DrawText(ConColor(texto, SombraCiudad), new Point(zona.X + 1, zona.Y + 1));
            dibujo.DrawText(ConColor(texto, ColorCiudad), zona.TopLeft);
        }
    }

    private void DibujarCamion(DrawingContext dibujo, Size tamano)
    {
        Point camion = vista.APantalla(new Point(PosicionX, PosicionZ), tamano);

        // Del último punto guardado hasta el camión (los puntos se guardan cada cierto trecho).
        if (Recorrido is { Tramos.Count: > 0 } recorrido && recorrido.Tramos[^1].Count > 0)
        {
            int[] ultimo = recorrido.Tramos[^1][^1];
            dibujo.DrawLine(LineaHastaCamion, vista.APantalla(new Point(ultimo[0], ultimo[1]), tamano), camion);
        }

        // 0 = norte (arriba); el rumbo aumenta hacia el oeste: en sentido contrario a las agujas del reloj.
        dibujo.PushTransform(new TranslateTransform(camion.X, camion.Y));
        dibujo.PushTransform(new RotateTransform(-Rumbo * 360));
        dibujo.DrawGeometry(ColorCamion, BordeCamion, Flecha);
        dibujo.Pop();
        dibujo.Pop();
    }

    /// <summary>Rehace las figuras del recorrido (completo y parte resaltada).</summary>
    private void CrearRecorrido()
    {
        recorridoAlDia = true;
        recorridoCompleto = null;
        recorridoResaltado = null;
        zonaResaltada = Rect.Empty;

        if (Recorrido is not { Tramos.Count: > 0 } recorrido)
        {
            return;
        }

        var completo = new StreamGeometry();
        var resaltado = new StreamGeometry();
        bool hayResaltado = false;

        using (StreamGeometryContext dibujoCompleto = completo.Open())
        using (StreamGeometryContext dibujoResaltado = resaltado.Open())
        {
            foreach (List<int[]> tramo in recorrido.Tramos.Where(t => t.Count >= 2))
            {
                dibujoCompleto.BeginFigure(new Point(tramo[0][0], tramo[0][1]), isFilled: false, isClosed: false);
                dibujoCompleto.PolyLineTo(tramo.Skip(1).Select(p => new Point(p[0], p[1])).ToList(), isStroked: true, isSmoothJoin: true);

                if (!HayResaltado)
                {
                    continue;
                }

                // Partes del tramo dentro del intervalo de minutos.
                var parte = new List<Point>();

                foreach (int[] punto in tramo.Append([0, 0, int.MaxValue]))
                {
                    bool dentro = punto.Length > 2 && punto[2] >= ResaltadoDesde && punto[2] <= ResaltadoHasta;

                    if (dentro)
                    {
                        parte.Add(new Point(punto[0], punto[1]));
                        zonaResaltada.Union(new Point(punto[0], punto[1]));
                        continue;
                    }

                    if (parte.Count >= 2)
                    {
                        dibujoResaltado.BeginFigure(parte[0], isFilled: false, isClosed: false);
                        dibujoResaltado.PolyLineTo(parte.Skip(1).ToList(), isStroked: true, isSmoothJoin: true);
                        hayResaltado = true;
                    }

                    parte.Clear();
                }
            }
        }

        completo.Freeze();
        resaltado.Freeze();
        recorridoCompleto = completo;
        recorridoResaltado = hayResaltado ? resaltado : null;
    }

    private Rect Pantalla(ZonaMundo zona, Size tamano)
    {
        Point esquina = vista.APantalla(new Point(zona.X, zona.Z), tamano);
        double lado = zona.Ancho * vista.Escala;
        return new Rect(esquina.X, esquina.Y, lado + 0.5, lado + 0.5); // +0,5 px para que no se vean rayas entre teselas
    }

    private Pen Linea(Brush color, double grosorPixeles) => new(color, grosorPixeles / vista.Escala)
    {
        LineJoin = PenLineJoin.Round,
        StartLineCap = PenLineCap.Round,
        EndLineCap = PenLineCap.Round
    };

    /// <summary>Un recorrido de pocos metros se ve con un margen mínimo alrededor.</summary>
    private static Rect Ampliar(Rect zona)
    {
        if (zona.IsEmpty)
        {
            return zona;
        }

        const double Minimo = 3000;
        double ancho = Math.Max(zona.Width, Minimo);
        double alto = Math.Max(zona.Height, Minimo);
        return new Rect(zona.X + zona.Width / 2 - ancho / 2, zona.Y + zona.Height / 2 - alto / 2, ancho, alto);
    }

    private static FormattedText ConColor(FormattedText texto, Brush color)
    {
        texto.SetForegroundBrush(color);
        return texto;
    }

    private void Texto(DrawingContext dibujo, string texto, Point posicion, Brush color, bool centrado)
    {
        var formato = new FormattedText(texto, CultureInfo.GetCultureInfo("es-ES"), FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), 12, color, VisualTreeHelper.GetDpi(this).PixelsPerDip);

        if (centrado)
        {
            posicion = new Point(posicion.X - formato.Width / 2, posicion.Y - formato.Height / 2);
        }

        dibujo.DrawText(formato, posicion);
    }

    private static void RecorridoCambiado(DependencyObject objeto, DependencyPropertyChangedEventArgs e)
    {
        var lienzo = (LienzoMapa)objeto;
        lienzo.recorridoAlDia = false;

        // En el historial, al elegir otra jornada, día o trayecto, se encuadra lo elegido.
        if (lienzo.EncuadrarRecorrido && e.Property != VersionRecorridoProperty)
        {
            lienzo.encuadrePendiente = true;
        }

        lienzo.InvalidateVisual();
    }

    private static DependencyProperty Registrar<T>(string nombre, T porDefecto, PropertyChangedCallback? alCambiar) =>
        DependencyProperty.Register(nombre, typeof(T), typeof(LienzoMapa),
            new FrameworkPropertyMetadata(porDefecto, FrameworkPropertyMetadataOptions.AffectsRender, alCambiar));

    private static T Congelar<T>(T objeto) where T : Freezable
    {
        objeto.Freeze();
        return objeto;
    }
}
