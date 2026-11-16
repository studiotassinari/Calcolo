using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using STA.Calcolo.Application;
using STA.Calcolo.Desktop.Services;
namespace STA.Calcolo.Desktop.Views;

/// <summary>Controllo di presentazione geografica: pan, zoom e marker WGS84.
/// Non calcola spettri; pubblica una coppia di coordinate attraverso il binding MVVM.</summary>
public sealed class GeographicMapView : FrameworkElement
{
    public static readonly DependencyProperty LocationProperty = DependencyProperty.Register(nameof(Location), typeof(GeographicPoint), typeof(GeographicMapView),
        new FrameworkPropertyMetadata(new GeographicPoint(12.19944, 44.41778), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnLocation));
    public GeographicPoint Location { get => (GeographicPoint)GetValue(LocationProperty); set => SetValue(LocationProperty, value); }
    public IMapTileService TileService { get; set; } = new OsmTileService();
    private readonly DispatcherTimer debounce = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private CancellationTokenSource? cancellation;
    private Dictionary<(int X, int Y), BitmapSource> tiles = [];
    private int zoom = 7;
    private PlotPoint center = MapProjection.Project(new(12.19944, 44.41778), 7);
    private Point down, previous;
    private bool moving, marker, moved, ownSelection;
    private string message = "Mappa OpenStreetMap · caricamento quando la scheda è visibile";
    public int Zoom => zoom;
    public GeographicMapView()
    {
        Focusable = true; ClipToBounds = true;
        debounce.Tick += async (_, _) => { debounce.Stop(); await RefreshTiles(); };
        Loaded += (_, _) => Schedule();
        Unloaded += (_, _) => { debounce.Stop(); cancellation?.Cancel(); };
        IsVisibleChanged += (_, _) => { if (IsVisible) Schedule(); else { debounce.Stop(); cancellation?.Cancel(); } };
        SizeChanged += (_, _) => Schedule();
    }
    private static void OnLocation(DependencyObject d, DependencyPropertyChangedEventArgs args)
    {
        var view = (GeographicMapView)d;
        if (!view.ownSelection && args.NewValue is GeographicPoint point) view.center = MapProjection.Project(point, view.zoom);
        view.InvalidateVisual(); view.Schedule();
    }
    private PlotPoint World(Point screen) => new(center.X + screen.X - ActualWidth / 2, center.Y + screen.Y - ActualHeight / 2);
    private Point Screen(PlotPoint world) => new(world.X - center.X + ActualWidth / 2, world.Y - center.Y + ActualHeight / 2);
    /// <summary>Un clic aggiorna entrambe le coordinate in un solo evento.</summary>
    public void SelectPixel(double x, double y)
    {
        var point = MapProjection.Unproject(World(new(x, y)), zoom);
        ownSelection = true;
        try { SetCurrentValue(LocationProperty, new GeographicPoint(Math.Clamp(point.Longitude, -180, 180), Math.Clamp(point.Latitude, -85, 85))); }
        finally { ownSelection = false; }
    }
    public void ZoomAt(int delta, double x, double y)
    {
        int next = Math.Clamp(zoom + delta, 3, 18); if (next == zoom) return;
        var anchor = MapProjection.Unproject(World(new(x, y)), zoom);
        zoom = next; var world = MapProjection.Project(anchor, zoom);
        center = new(world.X - x + ActualWidth / 2, world.Y - y + ActualHeight / 2);
        tiles.Clear(); ConstrainCenter(); Schedule(); InvalidateVisual();
    }
    public void PanBy(double x, double y)
    {
        center = new(center.X - x, center.Y - y); ConstrainCenter(); Schedule(); InvalidateVisual();
    }
    private void ConstrainCenter()
    {
        double size = 256d * (1 << zoom); center = new(Math.Clamp(center.X, 0, size), Math.Clamp(center.Y, 0, size));
    }
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e); Focus(); var p = e.GetPosition(this);
        if (p.X < 48 && p.Y < 80) { ZoomAt(p.Y < 40 ? 1 : -1, ActualWidth / 2, ActualHeight / 2); e.Handled = true; return; }
        if (p.Y > ActualHeight - 26) return;
        down = previous = p; moved = false; moving = true;
        marker = (p - Screen(MapProjection.Project(Location, zoom))).Length < 18; CaptureMouse(); e.Handled = true;
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e); if (!moving) return;
        var p = e.GetPosition(this); moved |= (p - down).Length > 4;
        if (moved) { if (marker) SelectPixel(p.X, p.Y); else PanBy(p.X - previous.X, p.Y - previous.Y); }
        previous = p;
    }
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e); if (!moving) return;
        if (!moved) { var p = e.GetPosition(this); SelectPixel(p.X, p.Y); }
        moving = false; ReleaseMouseCapture(); Schedule(); e.Handled = true;
    }
    protected override void OnLostMouseCapture(MouseEventArgs e) { base.OnLostMouseCapture(e); moving = false; }
    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        var p = e.GetPosition(this); ZoomAt(Math.Sign(e.Delta), p.X, p.Y); e.Handled = true;
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key is Key.Add or Key.OemPlus) ZoomAt(1, ActualWidth / 2, ActualHeight / 2);
        else if (e.Key is Key.Subtract or Key.OemMinus) ZoomAt(-1, ActualWidth / 2, ActualHeight / 2);
        else if (e.Key == Key.Home) { center = MapProjection.Project(Location, zoom); Schedule(); InvalidateVisual(); }
        else return;
        e.Handled = true;
    }
    private void Schedule()
    {
        if (!IsLoaded || !IsVisible) return;
        cancellation?.Cancel(); debounce.Stop(); debounce.Start();
    }
    private async Task RefreshTiles()
    {
        if (!IsVisible || ActualWidth < 1 || ActualHeight < 1) return;
        cancellation?.Cancel(); cancellation?.Dispose(); cancellation = new(); var token = cancellation.Token;
        int currentZoom = zoom, max = (1 << zoom) - 1;
        var origin = World(new(0, 0)); var end = World(new(ActualWidth, ActualHeight));
        int x0 = Math.Clamp((int)Math.Floor(origin.X / 256), 0, max), x1 = Math.Clamp((int)Math.Floor(end.X / 256), 0, max);
        int y0 = Math.Clamp((int)Math.Floor(origin.Y / 256), 0, max), y1 = Math.Clamp((int)Math.Floor(end.Y / 256), 0, max);
        var next = new Dictionary<(int X, int Y), BitmapSource>(); bool failed = false;
        try
        {
            // Nessun prefetch: il ciclo è limitato all'area effettivamente visibile.
            for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++)
            {
                token.ThrowIfCancellationRequested();
                if (tiles.TryGetValue((x, y), out var cached)) { next[(x, y)] = cached; continue; }
                try
                {
                    var bytes = await TileService.ReadAsync(currentZoom, x, y, token);
                    using var stream = new MemoryStream(bytes);
                    var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze();
                    next[(x, y)] = bitmap;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception) { failed = true; break; }
            }
            token.ThrowIfCancellationRequested(); tiles = next;
            message = failed ? "Mappa non disponibile · usare le coordinate manuali" : $"Zoom {zoom} · WGS84";
            InvalidateVisual();
        }
        catch (OperationCanceledException)
        {
            if (!token.IsCancellationRequested) { message = "Servizio mappa non raggiungibile · riprovare con pan/zoom"; InvalidateVisual(); }
        }
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc); dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(234,240,241)), null, new Rect(RenderSize));
        void Text(string value, double x, double y, double size = 12) => dc.DrawText(new FormattedText(value, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, Brushes.DarkSlateGray, VisualTreeHelper.GetDpi(this).PixelsPerDip), new(x,y));
        foreach (var tile in tiles) { var p = Screen(new(tile.Key.X * 256, tile.Key.Y * 256)); dc.DrawImage(tile.Value, new Rect(p, new Size(256,256))); }
        var markerPoint = Screen(MapProjection.Project(Location, zoom));
        dc.DrawEllipse(Brushes.DarkOrange, new(Brushes.White, 3), markerPoint, 10, 10);
        dc.DrawLine(new(Brushes.DarkOrange, 2), new(markerPoint.X, markerPoint.Y - 18), new(markerPoint.X, markerPoint.Y + 18));
        dc.DrawLine(new(Brushes.DarkOrange, 2), new(markerPoint.X - 18, markerPoint.Y), new(markerPoint.X + 18, markerPoint.Y));
        dc.DrawRectangle(Brushes.White, new(Brushes.LightGray, 1), new Rect(5,5,38,72)); Text("+", 14, 7, 24); Text("−", 14, 42, 24);
        dc.DrawRectangle(Brushes.White, null, new Rect(0, Math.Max(0, ActualHeight - 48), ActualWidth, 48));
        Text(message, 8, ActualHeight - 46);
        Text("© OpenStreetMap contributors · openstreetmap.org/copyright", 8, ActualHeight - 24, 11);
    }
}
