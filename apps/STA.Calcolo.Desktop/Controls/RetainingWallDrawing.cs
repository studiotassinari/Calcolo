using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using STA.MuroSostegno.Core;
namespace STA.Calcolo.Desktop.Controls;

/// <summary>
/// Disegno WPF vettoriale con scala identica sui due assi. Non esegue verifiche strutturali.
/// I grip (quadratini) si trascinano col mouse: il controllo converte la posizione in metri e invia
/// una <see cref="WallGripDrag"/> a GripCommand; i valori cambiano soltanto nel ViewModel.
/// Passo di aggancio 5 cm, 1 cm tenendo premuto Ctrl; Esc annulla il trascinamento in corso.
/// </summary>
public sealed class RetainingWallDrawing : FrameworkElement
{
    public static readonly DependencyProperty GripsProperty = DependencyProperty.Register(nameof(Grips),typeof(IReadOnlyList<WallGrip>),typeof(RetainingWallDrawing),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty GripCommandProperty = DependencyProperty.Register(nameof(GripCommand),typeof(ICommand),typeof(RetainingWallDrawing));
    public static readonly DependencyProperty ParametersProperty = DependencyProperty.Register(nameof(Parameters),typeof(IReadOnlyDictionary<string,ViewModels.WallNumberField>),typeof(RetainingWallDrawing),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.AffectsRender));
    public IReadOnlyList<WallGrip>? Grips {get=>(IReadOnlyList<WallGrip>?)GetValue(GripsProperty);set=>SetValue(GripsProperty,value);}
    public ICommand? GripCommand {get=>(ICommand?)GetValue(GripCommandProperty);set=>SetValue(GripCommandProperty,value);}
    /// <summary>Campi del ViewModel: servono solo a mostrare il valore corrente accanto al grip attivo.</summary>
    public IReadOnlyDictionary<string,ViewModels.WallNumberField>? Parameters {get=>(IReadOnlyDictionary<string,ViewModels.WallNumberField>?)GetValue(ParametersProperty);set=>SetValue(ParametersProperty,value);}

    /// <summary>Trasformazione metri → pixel. Durante il trascinamento resta bloccata per evitare che il disegno "scappi" sotto il mouse.</summary>
    private readonly record struct View(double Left,double Top,double MaxY,double Scale)
    {
        public Point Map(WallPoint p) => new(Left+p.X*Scale,Top+(MaxY-p.Y)*Scale);
        public WallPoint Unmap(Point p) => new((p.X-Left)/Scale,MaxY-(p.Y-Top)/Scale);
    }
    private View? view, frozen;
    private string? hover, active;
    private Dictionary<string,string>? before; // valori all'inizio del trascinamento, per l'annullamento con Esc
    private const double GripRadius = 6;

    public RetainingWallDrawing() { Focusable=true; FocusVisualStyle=null; }
    public static readonly DependencyProperty GeometryProperty = DependencyProperty.Register(nameof(Geometry),typeof(WallGeometry),typeof(RetainingWallDrawing),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.AffectsRender));
    /// <summary>Diagramma delle pressioni del motore selezionato (quote assolute in m, valori in kPa).</summary>
    public static readonly DependencyProperty PressuresProperty = DependencyProperty.Register(nameof(Pressures),typeof(IReadOnlyList<WallPressurePoint>),typeof(RetainingWallDrawing),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.AffectsRender));
    public WallGeometry? Geometry {get=>(WallGeometry?)GetValue(GeometryProperty);set=>SetValue(GeometryProperty,value);}
    public IReadOnlyList<WallPressurePoint>? Pressures {get=>(IReadOnlyList<WallPressurePoint>?)GetValue(PressuresProperty);set=>SetValue(PressuresProperty,value);}
    /// <summary>Forze della combinazione selezionata (motore rivisto): frecce proporzionali all'intensità.</summary>
    public static readonly DependencyProperty ForcesProperty = DependencyProperty.Register(nameof(Forces),typeof(IReadOnlyList<WallForce>),typeof(RetainingWallDrawing),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.AffectsRender));
    public IReadOnlyList<WallForce>? Forces {get=>(IReadOnlyList<WallForce>?)GetValue(ForcesProperty);set=>SetValue(ForcesProperty,value);}
    /// <summary>Diagrammi della combinazione selezionata: spinta attiva, acqua, passiva e pressione sul terreno di fondazione.</summary>
    public static readonly DependencyProperty DiagramsProperty = DependencyProperty.Register(nameof(Diagrams),typeof(WallDiagrams),typeof(RetainingWallDrawing),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.AffectsRender));
    public WallDiagrams? Diagrams {get=>(WallDiagrams?)GetValue(DiagramsProperty);set=>SetValue(DiagramsProperty,value);}
    public static readonly DependencyProperty ShowForcesProperty = DependencyProperty.Register(nameof(ShowForces),typeof(bool),typeof(RetainingWallDrawing),new FrameworkPropertyMetadata(true,FrameworkPropertyMetadataOptions.AffectsRender));
    public bool ShowForces {get=>(bool)GetValue(ShowForcesProperty);set=>SetValue(ShowForcesProperty,value);}
    public static readonly DependencyProperty ShowDiagramsProperty = DependencyProperty.Register(nameof(ShowDiagrams),typeof(bool),typeof(RetainingWallDrawing),new FrameworkPropertyMetadata(true,FrameworkPropertyMetadataOptions.AffectsRender));
    public bool ShowDiagrams {get=>(bool)GetValue(ShowDiagramsProperty);set=>SetValue(ShowDiagramsProperty,value);}
    private static Brush Color(string value) => new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString(value));
    private static readonly Brush Navy=Color("#192845"),Teal=Color("#135F6E"),Water=Color("#226BA0"),Amber=Color("#A35B18"),
        Passive=Color("#2E7D4F"),Bearing=Color("#6B4C9A"),Variable=Color("#C3872A"),Seismic=Color("#B3261E");
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
        // Spazi riservati ai diagrammi: passiva a sinistra, spinte a destra, pressione sul terreno sotto la base.
        var diagrams=ShowDiagrams&&Diagrams is not null;
        double marginLeft=diagrams?120:60,marginRight=diagrams?150:90,marginBottom=diagrams?110:62;
        var scale=Math.Min((width-marginLeft-marginRight)/(b+.7),(height-40-marginBottom)/Math.Max(.1,maxY-minY));
        if(!double.IsFinite(scale)||scale<=0)return;
        var left=marginLeft+.35*scale+(width-marginLeft-marginRight-(b+.7)*scale)/2;var top=40+(height-40-marginBottom-(maxY-minY)*scale)/2;
        view=frozen??new View(left,top,maxY,scale);var v=view.Value;
        scale=v.Scale;left=v.Left;
        Point Map(WallPoint p)=>v.Map(p);
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
        H(g.Wall[0],g.Wall[9],bottom+(diagrams?86:29),"Bf "+N(b)+" m");
        H(g.Wall[4],g.Wall[5],Map(g.Wall[4]).Y-23,"S1 "+N(g.Wall[5].X-g.Wall[4].X));
        V(g.Wall[1],g.Wall[4],left-17,"H1 "+N(g.Wall[4].Y-g.Wall[1].Y));
        // Dimensioni secondarie inserite nel disegno senza sovrapporre il diagramma di pressione.
        var basePoint=Map(g.Wall[2]);Text("Ba "+N(g.Wall[2].X),left+5,basePoint.Y+4,Navy,10);
        var tooth=Map(g.Wall[12]);Text("H4 "+N(g.Wall[13].Y-g.Wall[12].Y),tooth.X-58,tooth.Y+5,Navy,10);
        var crest=Map(g.Wall[6]);Text("Terreno a monte",crest.X+12,crest.Y+18,Color("#7B6341"),10);
        if(diagrams)DrawDiagrams(dc,g,v,Diagrams!,bottom,Text);
        if(ShowForces&&Forces is {Count:>0} forces)DrawForces(dc,v,forces,Diagrams?.Base,Text);
        DrawGrips(dc,g,v,Text);
        if(!diagrams&&Pressures is {} rows)
        {
            // Diagramma statico σ = KA (σ′v + q) del motore selezionato, aggiornato a ogni ricalcolo.
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

    /// <summary>
    /// Diagrammi di pressione con un'unica scala orizzontale [kPa → px] per spinta attiva, acqua e passiva,
    /// così le intensità sono confrontabili; la pressione sul terreno ha una propria scala verticale.
    /// </summary>
    private void DrawDiagrams(DrawingContext dc,WallGeometry g,View v,WallDiagrams d,double bottom,Action<string,double,double,Brush,double> text)
    {
        var b=g.Wall[8].X;
        var max=new[]{d.Active,d.WaterBack,d.Passive,d.WaterFront}.SelectMany(x=>x).Select(p=>p.Value).DefaultIfEmpty(0).Max();
        if(max>1e-9)
        {
            var k=62/max; // px per kPa
            double Vertical(IReadOnlyList<WallPressurePoint> rows,double x0,int direction,Brush stroke,string fill,string label)
            {
                if(rows.Count<2||rows.Max(p=>p.Value)<=1e-9)return x0;
                var shape=new StreamGeometry();using(var c=shape.Open())
                {
                    c.BeginFigure(new(x0,v.Map(new(0,rows[0].Elevation)).Y),true,true);
                    foreach(var p in rows)c.LineTo(new(x0+direction*k*p.Value,v.Map(new(0,p.Elevation)).Y),true,false);
                    c.LineTo(new(x0,v.Map(new(0,rows[^1].Elevation)).Y),true,false);
                }
                shape.Freeze();dc.DrawGeometry(Color(fill),new Pen(stroke,1.3),shape);
                var peak=rows.MaxBy(p=>p.Value)!;var py=v.Map(new(0,peak.Elevation)).Y;
                text($"{label} {N(peak.Value)} kPa",direction>0?x0+4:x0-k*peak.Value-4,py+4,stroke,10);
                return x0+direction*(k*rows.Max(p=>p.Value)+10);
            }
            // A monte: terreno + sovraccarico, poi acqua affiancata; a valle: passiva e acqua verso sinistra.
            var right=Vertical(d.Active,v.Map(new(b,0)).X+2,1,Amber,"#F4DBC0","σa");
            Vertical(d.WaterBack,right,1,Water,"#D6E6F2","u");
            var leftEdge=Vertical(d.Passive,v.Map(new(0,0)).X-2,-1,Passive,"#D8EBDD","σp");
            Vertical(d.WaterFront,leftEdge,-1,Water,"#D6E6F2","u");
        }
        // Pressione sul terreno di fondazione sotto il piano più basso del muro.
        var basePressure=d.Base;var y0=bottom+10;
        if(basePressure.Points.Count>=2&&basePressure.SigmaMax is double smax&&smax>1e-9)
        {
            var kb=44/smax;var shape=new StreamGeometry();using(var c=shape.Open())
            {
                c.BeginFigure(new(v.Map(new(0,0)).X,y0),true,true);
                foreach(var p in basePressure.Points)c.LineTo(new(v.Map(new(p.X,0)).X,y0+kb*p.Value),true,false);
                c.LineTo(new(v.Map(new(b,0)).X,y0),true,false);
            }
            shape.Freeze();dc.DrawGeometry(Color("#E7DFF0"),new Pen(Bearing,1.3),shape);
            text($"σ terreno: valle {N(basePressure.SigmaToe!.Value)} · monte {N(basePressure.SigmaHeel!.Value)} kPa · e = {N(basePressure.Eccentricity)} m{(basePressure.InCore?"":" (base parzializzata)")}",v.Map(new(0,0)).X,y0+kb*smax+4,Bearing,10);
        }
        else text("Pressione sul terreno non definita: risultante fuori dalla base",v.Map(new(0,0)).X,y0+4,Seismic,10);
    }

    /// <summary>Sigle brevi delle forze, come nella relazione di calcolo.</summary>
    private static string Symbol(string name) => name switch
    {
        "Peso del muro"=>"Wm","Peso del terreno a valle"=>"Wv","Peso del terreno a monte"=>"Wt","Forze d'inerzia (kh·W)"=>"kh·W",
        "Spinta del terreno"=>"Sa","Spinta del sovraccarico"=>"Sq","Spinta dell'acqua a monte"=>"Sw","Spinta dell'acqua a valle"=>"Sw,v",
        "Sottospinta"=>"U",_=>"Sw,d"
    };
    /// <summary>Frecce con la punta nel punto di applicazione e lunghezza proporzionale all'intensità; reazione N del terreno in viola.</summary>
    private void DrawForces(DrawingContext dc,View v,IReadOnlyList<WallForce> forces,WallBasePressure? basePressure,Action<string,double,double,Brush,double> text)
    {
        var max=forces.Max(f=>f.Magnitude);if(max<=1e-9)return;
        void Arrow(Point head,double dx,double dy,double magnitude,Brush brush,string label)
        {
            // dx, dy: direzione nel piano del disegno (y verso l'alto), convertita in pixel (y verso il basso).
            var length=20+70*magnitude/max;var u=new Vector(dx,-dy);u.Normalize();
            var tail=head-u*length;var pen=new Pen(brush,1.8);dc.DrawLine(pen,tail,head);
            var n=new Vector(-u.Y,u.X);var tip=new StreamGeometry();
            using(var c=tip.Open()){c.BeginFigure(head,true,true);c.LineTo(head-u*9+n*4,true,false);c.LineTo(head-u*9-n*4,true,false);}
            tip.Freeze();dc.DrawGeometry(brush,null,tip);
            text(label,tail.X+(u.X>0?-46:4),tail.Y+(u.Y>0?-16:2),brush,10);
        }
        foreach(var f in forces)
        {
            if(f.Magnitude<.01*max)continue; // forze trascurabili omesse per leggibilità
            var brush=f.Kind=="E"?Seismic:f.Kind=="Q"?Variable:f.Name.Contains("acqua")||f.Name=="Sottospinta"?Water:Navy;
            Arrow(v.Map(new(f.X,f.Y)),f.Fx,f.Fy,f.Magnitude,brush,$"{Symbol(f.Name)} {N(Math.Round(f.Magnitude,1))}");
        }
        // Reazione del terreno: risultante N applicata a distanza u dal piede.
        if(basePressure is {SigmaToe:not null} bp)Arrow(v.Map(new(bp.U,0)),0,1,Math.Min(bp.N,max),Bearing,$"N {N(Math.Round(bp.N,1))}");
    }

    /// <summary>Grip: quadrato bianco bordato; il grip sotto il mouse o trascinato è pieno e mostra il valore.</summary>
    private void DrawGrips(DrawingContext dc,WallGeometry g,View v,Action<string,double,double,Brush,double> text)
    {
        if(Grips is not {} grips||GripCommand is null)return;
        var accent=Color("#C3872A");var pen=new Pen(Navy,1.2);
        foreach(var grip in grips)
        {
            var p=v.Map(grip.Anchor(g));
            if(p.X<0||p.Y<0||p.X>ActualWidth||p.Y>ActualHeight)continue;
            var on=grip.Id==active||(active is null&&grip.Id==hover);
            var r=on?GripRadius+1:GripRadius-1;
            dc.DrawRectangle(on?accent:Brushes.White,pen,new Rect(p.X-r,p.Y-r,2*r,2*r));
            if(!on)continue;
            var value=Parameters is {} fields&&fields.TryGetValue(grip.Key,out var f)?f.Value:"";
            var arrow=grip.Axis==WallGripAxis.Horizontal?"↔":"↕";
            text($"{arrow} {grip.Key} = {value} {grip.Unit}",p.X+10,p.Y-24,accent,12);
        }
    }
    private string? HitGrip(Point mouse)
    {
        if(Grips is not {} grips||Geometry is not {} g||view is not {} v)return null;
        // Il grip più vicino entro una tolleranza leggermente più ampia del disegno, comoda anche su schermi ad alta risoluzione.
        return grips.Select(grip=>(grip.Id,Distance:(v.Map(grip.Anchor(g))-mouse).Length))
            .Where(x=>x.Distance<=GripRadius+4).OrderBy(x=>x.Distance).Select(x=>x.Id).FirstOrDefault();
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);var mouse=e.GetPosition(this);
        if(active is not null&&frozen is {} v)
        {
            var step=Keyboard.Modifiers.HasFlag(ModifierKeys.Control)?.01:.05;
            var request=new WallGripDrag(active,v.Unmap(mouse),step);
            if(GripCommand?.CanExecute(request)==true)GripCommand.Execute(request);
            InvalidateVisual();return;
        }
        var hit=HitGrip(mouse);
        if(hit!=hover){hover=hit;InvalidateVisual();}
        var axis=Grips?.FirstOrDefault(x=>x.Id==hit)?.Axis;
        Cursor=axis switch{WallGripAxis.Horizontal=>Cursors.SizeWE,WallGripAxis.Vertical=>Cursors.SizeNS,_=>null};
        ToolTip=hit is null?null:$"Trascinare per modificare {hit} · Ctrl: passo 1 cm · Esc: annulla";
    }
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        var hit=HitGrip(e.GetPosition(this));
        if(hit is null||view is null)return;
        active=hit;frozen=view;Focus();CaptureMouse();
        before=Parameters?.ToDictionary(p=>p.Key,p=>p.Value.Value);
        e.Handled=true;InvalidateVisual();
    }
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e){base.OnMouseLeftButtonUp(e);EndDrag();}
    protected override void OnLostMouseCapture(MouseEventArgs e){base.OnLostMouseCapture(e);EndDrag();}
    protected override void OnMouseLeave(MouseEventArgs e){base.OnMouseLeave(e);if(active is null&&hover is not null){hover=null;InvalidateVisual();}}
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if(e.Key!=Key.Escape||active is null)return;
        // Annulla: ripristina i valori testuali come erano al momento della pressione del mouse.
        if(before is not null&&Parameters is {} fields)foreach(var (key,value) in before)if(fields[key].Value!=value)fields[key].Value=value;
        EndDrag();e.Handled=true;
    }
    private void EndDrag()
    {
        if(active is null)return;
        active=null;frozen=null;before=null;
        if(IsMouseCaptured)ReleaseMouseCapture();
        InvalidateVisual();
    }
}
