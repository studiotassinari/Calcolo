namespace STA.MuroSostegno.Core;

/// <summary>Direzione in cui un grip del disegno può essere trascinato.</summary>
public enum WallGripAxis { Horizontal, Vertical }

/// <summary>Richiesta inviata dal disegno: grip trascinato, posizione in metri e passo di aggancio (m).</summary>
public sealed record WallGripDrag(string Id, WallPoint Target, double Step);

/// <summary>
/// Maniglia grafica collegata a uno o più parametri del muro.
/// Anchor restituisce la posizione del grip in metri (stesso sistema di <see cref="WallGeometry"/>);
/// Apply converte la posizione trascinata nei nuovi valori dei parametri, partendo dai valori correnti.
/// </summary>
public sealed record WallGrip(string Id, string Key, string Unit, WallGripAxis Axis,
    Func<WallGeometry,WallPoint> Anchor,
    Func<IReadOnlyDictionary<string,double>,WallPoint,IReadOnlyDictionary<string,double>> Apply);

/// <summary>
/// Modifica grafica dei parametri: ogni grip traduce una posizione del disegno in valori d'ingresso.
/// Il motore numerico non cambia; un trascinamento che produce una geometria non ammessa viene rifiutato
/// e i parametri restano invariati (nessun valore corretto in modo silenzioso).
/// </summary>
public static class WallEditing
{
    // Arrotondamenti dei valori prodotti dal trascinamento: metri al millimetro, pendenze al decimo di punto.
    private static double M(double value) => Math.Round(value,3);
    private static double Percent(double value) => Math.Round(value,1);
    private static Dictionary<string,double> One(string key,double value) => new() { [key] = value };

    /// <summary>Elenco dei grip disponibili, nell'ordine in cui vengono disegnati e cercati dal mouse.</summary>
    public static IReadOnlyList<WallGrip> Grips { get; } = [
        // Paramento: altezza in sommità, spessore in testa e spiombamento del lato a valle.
        new("H1","H1","m",WallGripAxis.Vertical,g=>Mid(g.Wall[4],g.Wall[5]),(d,p)=>One("H1",M(p.Y-d["H2"]))),
        new("S1","S1","m",WallGripAxis.Horizontal,g=>g.Wall[5],(d,p)=>One("S1",M(p.X-d["Ba"]-d["Ds1"]))),
        new("Ds1","Ds1","m",WallGripAxis.Horizontal,g=>g.Wall[4],(d,p)=>One("Ds1",M(p.X-d["Ba"]))),
        // Base del paramento: lo sporto Ba sposta il muro, S2 ne varia lo spessore alla base.
        new("Ba","Ba","m",WallGripAxis.Horizontal,g=>g.Wall[2],(d,p)=>One("Ba",M(p.X))),
        new("S2","S2","m",WallGripAxis.Horizontal,g=>g.Wall[7],(d,p)=>One("S2",M(p.X-d["Ba"]))),
        // Fondazione: larghezza e spessori anteriore/posteriore.
        new("Bf","Bf","m",WallGripAxis.Horizontal,g=>g.Wall[8],ApplyBf),
        new("H2","H2","m",WallGripAxis.Vertical,g=>g.Wall[1],ApplyH2),
        new("H3","H3","m",WallGripAxis.Vertical,g=>g.Wall[9],(d,p)=>One("H3",M(d["H2"]-p.Y))),
        // Dente: profondità al centro della base, posizione e spessore sui lati verticali.
        new("H4","H4","m",WallGripAxis.Vertical,g=>Mid(g.Wall[11],g.Wall[12]),(d,p)=>One("H4",M(d["H2"]-d["H3"]-p.Y))),
        new("Xd","Xd","m",WallGripAxis.Horizontal,g=>Mid(g.Wall[12],g.Wall[13]),(d,p)=>One("Xd",M(p.X))),
        new("S3","S3","m",WallGripAxis.Horizontal,g=>Mid(g.Wall[10],g.Wall[11]),(d,p)=>One("S3",M(p.X-d["Xd"]))),
        // Terreni: altezza sul paramento e pendenza ricavata dal punto estremo della superficie.
        new("Hta","Hta","m",WallGripAxis.Vertical,g=>g.Wall[3],(d,p)=>One("Hta",M(p.Y-d["H2"]))),
        new("It","It","%",WallGripAxis.Vertical,g=>g.FrontSoil[3],ApplyIt),
        new("Htp","Htp","m",WallGripAxis.Vertical,g=>g.Wall[6],(d,p)=>One("Htp",M(p.Y-d["H2"]))),
        new("Itp","Itp","%",WallGripAxis.Vertical,g=>g.BackSoil[2],ApplyItp),
        // Falde: le quote sono assolute rispetto all'origine del disegno (spigolo anteriore inferiore).
        new("Qfa","Qfa","m",WallGripAxis.Vertical,g=>new WallPoint(-.35,g.FrontWater),(d,p)=>One("Qfa",M(p.Y))),
        new("Qfp","Qfp","m",WallGripAxis.Vertical,g=>new WallPoint(g.Wall[8].X+.35,g.BackWater),(d,p)=>One("Qfp",M(p.Y)))
    ];

    private static WallPoint Mid(WallPoint a,WallPoint b) => new((a.X+b.X)/2,(a.Y+b.Y)/2);

    /// <summary>Se il dente è allineato al bordo posteriore, lo segue quando cambia Bf.</summary>
    private static IReadOnlyDictionary<string,double> ApplyBf(IReadOnlyDictionary<string,double> d,WallPoint p)
    {
        var bf=M(p.X); var result=One("Bf",bf);
        if (Math.Abs(d["Xd"]+d["S3"]-d["Bf"])<1e-9) result["Xd"]=M(bf-d["S3"]);
        return result;
    }

    /// <summary>Con fondazione a spessore costante (H2 = H3) il trascinamento mantiene la base orizzontale.</summary>
    private static IReadOnlyDictionary<string,double> ApplyH2(IReadOnlyDictionary<string,double> d,WallPoint p)
    {
        var h2=M(p.Y); var result=One("H2",h2);
        if (Math.Abs(d["H2"]-d["H3"])<1e-9) result["H3"]=h2;
        return result;
    }

    /// <summary>It in percentuale: abbassamento della superficie fra il paramento (w3) e x = 0.</summary>
    private static IReadOnlyDictionary<string,double> ApplyIt(IReadOnlyDictionary<string,double> d,WallPoint p)
    {
        var x=d["Ba"]+d["Ds1"]*d["Hta"]/d["H1"]; // ascissa di w3, come in WallGeometry.Create
        if (x<=1e-9) return new Dictionary<string,double>();
        return One("It",Percent((d["H2"]+d["Hta"]-p.Y)/x*100));
    }

    /// <summary>Itp in percentuale: rialzo della superficie fra il paramento (w6) e x = Bf.</summary>
    private static IReadOnlyDictionary<string,double> ApplyItp(IReadOnlyDictionary<string,double> d,WallPoint p)
    {
        var x=d["Ba"]+d["S2"]+(d["Ds1"]+d["S1"]-d["S2"])*d["Htp"]/d["H1"]; // ascissa di w6
        var run=d["Bf"]-x;
        if (run<=1e-9) return new Dictionary<string,double>();
        return One("Itp",Percent((p.Y-d["H2"]-d["Htp"])/run*100));
    }

    /// <summary>
    /// Restituisce i parametri risultanti dal trascinamento del grip <paramref name="id"/> nel punto
    /// <paramref name="target"/>, già arrotondato al passo <paramref name="step"/> (m).
    /// Restituisce null se il grip non esiste o se la geometria risultante non è ammessa.
    /// </summary>
    public static Dictionary<string,double>? Drag(IReadOnlyDictionary<string,double> current,string id,WallPoint target,double step)
    {
        var grip=Grips.FirstOrDefault(g=>g.Id==id);
        if (grip is null || !(step>0)) return null;
        // Il passo di aggancio è applicato soltanto lungo l'asse del grip.
        double Snap(double v) => Math.Round(v/step)*step;
        var p=grip.Axis==WallGripAxis.Horizontal ? target with { X=Snap(target.X) } : target with { Y=Snap(target.Y) };
        var next=new Dictionary<string,double>(current);
        foreach (var (key,value) in grip.Apply(current,p)) next[key]=value;
        try { WallGeometry.Create(next); }
        catch (ArgumentException) { return null; }
        return next;
    }
}
