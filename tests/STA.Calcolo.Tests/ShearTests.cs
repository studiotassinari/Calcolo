using STA.Calcolo.Application;
using STA.Taglio.Core;
using TaglioClsRect;
internal static class ShearTests
{
    private static void Assert(bool value) { if (!value) throw new Exception("Shear assertion failed"); }
    private static void Near(double a,double b,double tolerance=1e-7) { if(!double.IsFinite(a)||Math.Abs(a-b)>tolerance*Math.Max(1,Math.Abs(b)))throw new Exception($"Taglio: {a} != {b}"); }
    private static void Reject(Action action) { try{action();}catch(ArgumentException){return;}throw new Exception("Invalid shear input accepted"); }
    public static void Run(Action<string,Action> test)
    {
        test("taglio: parità con sorgente originale su 288 casi", () =>
        {
            int count=0;
            foreach(bool en in new[]{false,true}) foreach(bool wall in new[]{false,true})
            foreach(double fck in new[]{30d,70d,90d}) foreach(double n in new[]{0d,200d})
            foreach(double theta in new[]{22.5,30,45}) foreach(double alpha in new[]{45d,90d})
            foreach(double bent in new[]{0d,2d})
            {
                var p=new ShearInput{Eurocode=en,Wall=wall,Fck=fck,N=n,Theta=theta,Alpha=alpha,BentCount=bent,AlphaCc=en?1:.85};
                var r=ShearCalculator.Calculate(p);
                var legacy=new ClsTaglio{H=p.H,bw=p.B,ric=p.Cover,Asl=p.Asl,fck=p.Fck,fyk=p.Fyk,gc=p.GammaC,ga=p.GammaS,
                    acc=p.AlphaCc,fcd=p.Fck*p.AlphaCc/p.GammaC,Ned=p.N*1000,Ved=p.V,Dia=p.Diameter,IntSt=p.Spacing,
                    nBr=p.Legs,Alfa=p.Alpha,DiaPieg=p.BentDiameter,IntPieg=p.BentSpacing,nPieg=p.BentCount,AlfaPieg=p.BentAlpha,Theta=p.Theta};
                legacy.VRD();Near(r.Concrete,legacy.VRd/1000);
                Near(r.Reinforcement,legacy.Vr_staffe(theta));
                Near(r.Strut,(en?legacy.Vr_biella1992(theta):legacy.Vr_biella(theta))*(wall?.4:1));
                Near(r.Capacity,Math.Min(r.Reinforcement,r.Strut));
                double proposed=wall&&!en?45:en?legacy.Theta_Ott_EN_1992_1_1(wall?.4:1):legacy.Theta_Ott();
                Near(r.SuggestedTheta,proposed,1e-6);
                Near(r.SuggestedCapacity,Math.Min(legacy.Vr_staffe(proposed),(en?legacy.Vr_biella1992(proposed):legacy.Vr_biella(proposed))*(wall?.4:1)),1e-6);
                count++;
            }
            Assert(count==288);
        });
        test("taglio: controllo indipendente a 45 gradi e unità", () =>
        {
            var p=new ShearInput{H=500,B=300,Cover=50,Diameter=10,Spacing=100,Legs=2,Fck=30,Fyk=450,Theta=45};
            var r=ShearCalculator.Calculate(p);
            Near(r.D,450); Near(r.Fcd,17); Near(r.Fyd,450/1.15);
            Near(r.Strut,516.375);
            Near(r.Reinforcement,.9*450*(2*Math.PI*25)*(450/1.15)/100/1000);
            var wall=ShearCalculator.Calculate(p with{Wall=true});Near(wall.Strut,206.55);Near(wall.SuggestedTheta,45);
            var en=ShearCalculator.Calculate(p with{Eurocode=true,AlphaCc=1});Near(en.Strut,729);
        });
        test("taglio: ricerca aggiorna entrambe le resistenze senza mutazioni", () =>
        {
            var p=new ShearInput{Diameter=30,Spacing=70,Theta=30};
            var r=ShearCalculator.Calculate(p);
            var proposed=ShearCalculator.Calculate(p with{Theta=r.SuggestedTheta});
            Near(r.SuggestedCapacity,proposed.Capacity);
            Near(ShearCalculator.Calculate(p).Capacity,r.Capacity);
            var wall=ShearCalculator.Calculate(p with{Wall=true,Eurocode=true});
            Near(wall.SuggestedCapacity,ShearCalculator.Calculate(p with{Wall=true,Eurocode=true,Theta=wall.SuggestedTheta}).Capacity);
        });
        test("taglio: assenza armature, capacità nulla e dominio numerico", () =>
        {
            var p=new ShearInput{Legs=0,Diameter=0,Spacing=0,BentCount=0,BentDiameter=0,BentSpacing=0};
            var r=ShearCalculator.Calculate(p);Near(r.Capacity,r.Concrete);Near(r.Reinforcement,0);
            r=ShearCalculator.Calculate(new ShearInput{N=100000,V=50});
            Near(r.Strut,0);Assert(!r.Pass&&r.Utilization is null);
            foreach(var bad in new[]{new ShearInput{H=0},new ShearInput{Cover=500},new ShearInput{B=double.NaN},
                new ShearInput{Spacing=0},new ShearInput{GammaC=0},new ShearInput{Legs=1.5},
                new ShearInput{N=-1},new ShearInput{V=double.PositiveInfinity},new ShearInput{Theta=0},new ShearInput{Fck=100}})
                Reject(()=>ShearCalculator.Calculate(bad));
        });
        test("taglio: importazione tgl, indici, virgole e JSON versionato", () =>
        {
            var text=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"taglio-parete.tgl"));
            var doc=ShearFiles.Read(text);var module=new ShearWorkspace();var p=module.Input(doc);
            Near(p.N,200.5);Near(p.Diameter,8);Near(p.BentDiameter,10);Assert(p.Wall&&doc.Data=="Parete = prova");
            var copy=ShearFiles.Read(ShearFiles.Write(doc));Assert(copy.Fields.SequenceEqual(doc.Fields));
            Near(module.Compute(copy).Capacity,module.Compute(doc).Capacity);
            Reject(()=>ShearFiles.Read(text.Replace("DiametroStaffe=1","DiametroStaffe=99")));
            Reject(()=>ShearFiles.Read(text.Replace("EN1992=False","EN1992=True")));
            Reject(()=>ShearFiles.Read(text.Replace("Ved=120","")));
            Reject(()=>ShearFiles.Read(text+"\nH=400"));
            Reject(()=>ShearFiles.Read(ShearFiles.Write(doc).Replace("\"schemaVersion\": 1","\"schemaVersion\": 2")));
            Reject(()=>ShearFiles.Read("{}"));
        });
    }
}
