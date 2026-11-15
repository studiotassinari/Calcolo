using STA.Muratura.Core;
namespace STA.Calcolo.Desktop.ViewModels;
public sealed record MasonrySummaryViewModel(string StiffnessDelta,string ResistanceDelta,string StiffnessValues,string ResistanceValues,string StiffnessOutcome,string ResistanceOutcome)
{
    public string WidthReduction {get;init;}="—";
    public string WidthValues {get;init;}="Completare i dati.";
    public bool HasFrames {get;init;}
    public string FrameIncrease {get;init;}="";
    public static MasonrySummaryViewModel Empty {get;}=new("—","—","Completare i dati per il confronto.","Completare i dati per il confronto.","Dati non validi o incompleti","Dati non validi o incompleti");
    public static MasonrySummaryViewModel Create(MasonryProject p)
    {
        var a=MasonryCalculator.Total(p.Before);var b=MasonryCalculator.Total(p.After);
        var f=FrameGroupCalculator.Calculate(p.AllFrames,a,b);double k=f.FinalK,v=f.FinalV,dk=(k/a.K-1)*100,dv=(v/a.Vt-1)*100;
        string end=f.Frames.All(r=>r==null)?"Dopo":"Dopo + cerchiature";
        double beforeWidth=p.Before.Sum(pier=>pier.L),afterWidth=p.After.Sum(pier=>pier.L);
        double reduction=beforeWidth-afterWidth;
        static string Increase(double contribution,double baseline)=>baseline>0?$"+{contribution/baseline*100:N2} %":"n.d. (base nulla)";
        return new($"{dk:N2} %",$"{dv:N2} %",$"Prima {a.K:N2} N/cm\n{end} {k:N2} N/cm",$"Prima {a.Vt:N2} kN\n{end} {v:N2} kN",FrameCalculator.StiffnessAccepts(dk)?"Entro ±15%":"Oltre ±15%",dv>=0?"Non diminuita":"Diminuita")
        {
            WidthReduction=reduction>=0?$"Riduzione {reduction:N1} cm":$"Aumento {-reduction:N1} cm",
            WidthValues=$"Prima {beforeWidth:N1} cm · Dopo {afterWidth:N1} cm",
            HasFrames=f.Frames.Any(r=>r!=null),
            FrameIncrease=$"K {Increase(f.K,b.K)} · V {Increase(f.Veff,b.Vt)}"
        };
    }
}
