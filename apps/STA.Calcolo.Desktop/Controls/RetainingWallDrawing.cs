using System.Globalization;
using System.Windows;
using System.Windows.Media;
using STA.MuroSostegno.Core;
namespace STA.Calcolo.Desktop.Controls;

/// <summary>Disegno WPF vettoriale con scala identica sui due assi. Non esegue verifiche strutturali.</summary>
public sealed class RetainingWallDrawing : FrameworkElement
{
    public static readonly DependencyProperty GeometryProperty = DependencyProperty.Register(nameof(Geometry),typeof(WallGeometry),typeof(RetainingWallDrawing),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty ResultProperty = DependencyProperty.Register(nameof(Result),typeof(WallResult),typeof(RetainingWallDrawing),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.AffectsRender));
    public WallGeometry? Geometry {get=>(WallGeometry?)GetValue(GeometryProperty);set=>SetValue(GeometryProperty,value);}
    public WallResult? Result {get=>(WallResult?)GetValue(ResultProperty);set=>SetValue(ResultProperty,value);}
    private static Brush Color(string value) => new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString(value));
    private static readonly Brush Navy=Color("#192845"),Teal=Color("#135F6E"),Water=Color("#226BA0"),Amber=Color("#A35B18");
    private static string N(double value) => value.ToString("0.##",CultureInfo.GetCultureInfo("it-IT"));
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);var width=ActualWidth;var height=ActualHeight;
        if(width<100||height<100)return;
        dc.DrawRectangle(Brushes.White,null,new Rect(0,0,width,height));
        void Text(string text,double x,double y,Brush brush,double size=11)
        {
            var ft=new FormattedText(text,CultureInfo.GetCultureInfo("it-IT"),FlowDirection.LeftToRight,
                new Typeface(new FontFamily("/STA.Calcolo.Desktop;component/Assets/Brand/#Acherus Grotesque"),FontStyles.Normal,FontWeights.Normal,FontStretches.Normal),size,brush,VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(ft,new Point(Math.Clamp(x,0,Math.Max(0,width-ft.Width)),Math.Clamp(y,0,Math.Max(0,height-ft.Height))));
        }
        if(Geometry is not {} g) {Text("Correggere i parametri per visualizzare la sezione.",12,30,Navy);return;}
        var all=g.Wall.Concat(g.FrontSoil).Concat(g.BackSoil).ToArray();
        var minY=all.Min(p=>p.Y);var maxY=all.Max(p=>p.Y);var b=g.Wall[8].X;
        var scale=Math.Min((width-150)/(b+1),(height-100)/Math.Max(.1,maxY-minY));
        if(!double.IsFinite(scale)||scale<=0)return;
        var left=60+(width-150-(b+1)*scale)/2;var top=38+(height-100-(maxY-minY)*scale)/2;
        Point Map(WallPoint p)=>new(left+p.X*scale,top+(maxY-p.Y)*scale);
        void Poly(IEnumerable<WallPoint> points,Brush fill,Brush stroke)
        {
            var ps=points.Select(Map).ToArray();var shape=new StreamGeometry();
            using(var c=shape.Open()){c.BeginFigure(ps[0],true,true);c.PolyLineTo(ps.Skip(1).ToArray(),true,false);}shape.Freeze();
            dc.DrawGeometry(fill,new Pen(stroke,1.5),shape);
        }
        // Trama di fondo soltanto grafica; le quote sotto riportano le dimensioni reali.
        for(var x=20d;x<width;x+=30)dc.DrawLine(new Pen(Color("#F0F4F5"),.5),new(x,0),new(x,height));
        for(var y=10d;y<height;y+=30)dc.DrawLine(new Pen(Color("#F0F4F5"),.5),new(0,y),new(width,y));
        Poly(g.FrontSoil,Color("#E6DFCE"),Color("#9B8A6B"));
        Poly(g.BackSoil,Color("#E6DFCE"),Color("#9B8A6B"));
        Poly(g.Wall,Color("#C9DFE2"),Teal);
        void Line(WallPoint a,WallPoint z,Pen pen)=>dc.DrawLine(pen,Map(a),Map(z));
        var waterPen=new Pen(Water,1.5){DashStyle=DashStyles.Dash};
        // La falda anteriore resta visibile nel disegno, pur non entrando nel motore originale.
        if(g.FrontWater>=minY&&g.FrontWater<=maxY)
        {Line(new(-.35,g.FrontWater),new(g.Wall[3].X,g.FrontWater),waterPen);var p=Map(new(-.35,g.FrontWater));Text("Qfa "+N(g.FrontWater),p.X,p.Y-16,Water,10);}
        if(g.BackWater>=minY&&g.BackWater<=maxY)
        {Line(new(g.Wall[7].X,g.BackWater),new(b+.35,g.BackWater),waterPen);var p=Map(new(b,g.BackWater));Text("Qfp "+N(g.BackWater),p.X+4,p.Y-16,Water,10);}
        var dim=new Pen(Navy,1);var ext=new Pen(Color("#A5B6BF"),.8);
        void H(WallPoint a,WallPoint z,double y,string label)
        {
            var p=Map(a);var q=Map(z);dc.DrawLine(ext,p,new(p.X,y));dc.DrawLine(ext,q,new(q.X,y));dc.DrawLine(dim,new(p.X,y),new(q.X,y));
            dc.DrawLine(dim,new(p.X-3,y+4),new(p.X+3,y-4));dc.DrawLine(dim,new(q.X-3,y+4),new(q.X+3,y-4));
            Text(label,(p.X+q.X)/2-25,y+5,Navy);
        }
        void V(WallPoint a,WallPoint z,double x,string label)
        {
            var p=Map(a);var q=Map(z);dc.DrawLine(ext,p,new(x,p.Y));dc.DrawLine(ext,q,new(x,q.Y));dc.DrawLine(dim,new(x,p.Y),new(x,q.Y));
            dc.DrawLine(dim,new(x-4,p.Y+3),new(x+4,p.Y-3));dc.DrawLine(dim,new(x-4,q.Y+3),new(x+4,q.Y-3));Text(label,x-50,(p.Y+q.Y)/2-8,Navy);
        }
        var bottom=Map(new(0,minY)).Y;
        H(g.Wall[0],g.Wall[9],bottom+29,"Bf "+N(b)+" m");
        H(g.Wall[4],g.Wall[5],Map(g.Wall[4]).Y-23,"S1 "+N(g.Wall[5].X-g.Wall[4].X));
        V(g.Wall[1],g.Wall[4],left-17,"H1 "+N(g.Wall[4].Y-g.Wall[1].Y));
        // Dimensioni secondarie inserite nel disegno senza sovrapporre il diagramma di pressione.
        var basePoint=Map(g.Wall[2]);Text("Ba "+N(g.Wall[2].X),left+5,basePoint.Y+4,Navy,10);
        var tooth=Map(g.Wall[12]);Text("H4 "+N(g.Wall[13].Y-g.Wall[12].Y),tooth.X-58,tooth.Y+5,Navy,10);
        var crest=Map(g.Wall[6]);Text("Terreno a monte",crest.X+12,crest.Y+18,Color("#7B6341"),10);
        if(Result is {} result)
        {
            // Diagramma storico: σ = KA (γ z + q). I valori derivano dagli input del risultato, non da campi modificati.
            var rows=result.Pressures;
            if(rows.Count>0 && rows.Max(p=>p.Value)>0)
            {
                var x=Math.Min(width-86,Map(new(b+.35,0)).X);var max=rows.Max(p=>p.Value);
                var shape=new StreamGeometry();using(var c=shape.Open())
                {
                    c.BeginFigure(new(x,Map(new(0,rows[0].Elevation)).Y),true,true);
                    foreach(var p in rows)c.LineTo(new(x+55*p.Value/max,Map(new(0,p.Elevation)).Y),true,false);
                    c.LineTo(new(x,Map(new(0,rows[^1].Elevation)).Y),true,false);
                }
                dc.DrawGeometry(Color("#F4DBC0"),new Pen(Amber,1.5),shape);
                Text(N(rows[0].Value)+" kPa",x,Map(new(0,rows[0].Elevation)).Y-17,Amber,10);
                Text(N(rows[^1].Value)+" kPa",x,Map(new(0,rows[^1].Elevation)).Y+6,Amber,10);
            }
        }
    }
}
