using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

// Disegna l'icona del modulo Vento sulle costruzioni: pittogramma Tassinari Associati con tre linee di vento.
// Coordinate su una tela 256×256; ogni dimensione dell'icona è resa in vettoriale, non ricampionata.
static class Program
{
    static readonly Color Petrolio = Color.FromRgb(0x2D, 0x62, 0x6D), Navy = Color.FromRgb(0x19, 0x28, 0x45);

    static void Draw(DrawingContext dc)
    {
        var navy = new SolidColorBrush(Navy); var teal = new SolidColorBrush(Petrolio);
        // tessera bianca con bordo navy, come l'icona dell'applicazione
        dc.DrawRoundedRectangle(Brushes.White, new Pen(navy, 12), new Rect(10, 30, 236, 196), 30, 30);

        // pittogramma: quadrato con barra superiore staccata dai montanti, asse centrale e due diagonali dal vertice
        const double x = 104, y = 62, s = 126, t = 6.5;
        var pen = new Pen(teal, t) { StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat };
        double h = t / 2;
        dc.DrawLine(pen, new Point(x, y + h), new Point(x + s, y + h));                         // barra superiore
        dc.DrawLine(pen, new Point(x, y + s - h), new Point(x + s, y + s - h));                 // base
        dc.DrawLine(pen, new Point(x + h, y + t * 2.2), new Point(x + h, y + s));               // montante sinistro
        dc.DrawLine(pen, new Point(x + s - h, y + t * 2.2), new Point(x + s - h, y + s));       // montante destro
        dc.DrawLine(pen, new Point(x + s / 2, y + t), new Point(x + s / 2, y + s - t));         // asse
        dc.DrawLine(pen, new Point(x + s / 2, y + t * .6), new Point(x + t * .9, y + s - t));   // diagonale sinistra
        dc.DrawLine(pen, new Point(x + s / 2, y + t * .6), new Point(x + s - t * .9, y + s - t)); // diagonale destra

        // vento: tre linee orizzontali di lunghezza diversa che arrivano al pittogramma da sinistra
        var wind = new Pen(navy, 9) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        dc.DrawLine(wind, new Point(44, 92), new Point(86, 92));
        dc.DrawLine(wind, new Point(30, 125), new Point(86, 125));
        dc.DrawLine(wind, new Point(52, 158), new Point(86, 158));
    }

    [STAThread]
    static void Main(string[] args)
    {
        foreach (int size in new[] { 16, 20, 24, 32, 40, 48, 64, 128, 256 })
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen()) { dc.PushTransform(new ScaleTransform(size / 256d, size / 256d)); Draw(dc); }
            var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var file = File.Create(Path.Combine(args[0], $"vento-{size}.png")); encoder.Save(file);
        }
    }
}
