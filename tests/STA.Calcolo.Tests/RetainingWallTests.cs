using System.Text.Json;
using STA.MuroSostegno.Core;

/// <summary>Parità con fixture del sorgente ZIP, casi analitici indipendenti e import senza sostituzioni silenziose.</summary>
internal static class RetainingWallTests
{
    private static void Check(bool condition,string message) {if(!condition)throw new Exception(message);}
    private static void Near(double a,double b) => Check(double.IsFinite(a)&&Math.Abs(a-b)<=1e-9*Math.Max(1,Math.Abs(b)), $"Muro: {a:R} ≠ {b:R}");
    private static void Reject(Action action)
    {
        try {action();}catch(Exception ex) when(ex is ArgumentException or InvalidDataException or JsonException){return;}
        throw new Exception("Input muro non valido accettato");
    }
    public static void Run(Action<string,Action> test)
    {
        test("Muro · 13 casi originali, 29 risultati e 14 vertici per caso",()=>
        {
            using var fixtures=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"retaining-wall-original.json")));
            Check(fixtures.RootElement.GetArrayLength()==13,"Fixture mancanti");
            foreach(var fixture in fixtures.RootElement.EnumerateArray())
            {
                var input=fixture.GetProperty("input").EnumerateObject().ToDictionary(p=>p.Name,p=>p.Value.GetDouble());
                var r=WallCalculator.Calculate(input);
                double[] actual=[r.Wall.Area,r.Wall.Centroid.X,r.Wall.Centroid.Y,r.FrontSoil.Area,r.FrontSoil.Centroid.X,r.FrontSoil.Centroid.Y,
                    r.BackSoil.Area,r.BackSoil.Centroid.X,r.BackSoil.Centroid.Y,r.Seismic.Active,r.Seismic.Passive,r.Seismic.PsiActive*Math.PI/180,
                    r.Seismic.PsiPassive*Math.PI/180,r.Seismic.Theta*Math.PI/180,r.Static.Active,r.Static.Passive,r.Seismic.AlphaH,
                    r.Stabilizing,r.StabilizingNominal,r.Overturning,r.OverturningNominal,r.OverturningStatic,r.OverturningStaticNominal,
                    r.SlidingThrust.G1,r.SlidingThrust.G2,r.SlidingThrust.Q,r.SlidingResistance.G1,r.SlidingResistance.G2,r.SlidingResistance.Q];
                var expected=fixture.GetProperty("expected").EnumerateArray().Select(e=>e.GetDouble()).ToArray();
                for(var i=0;i<actual.Length;i++)Near(actual[i],expected[i]);
                var wall=fixture.GetProperty("wall");for(var i=0;i<14;i++){Near(r.Geometry.Wall[i].X,wall[i].GetProperty("x").GetDouble());Near(r.Geometry.Wall[i].Y,wall[i].GetProperty("y").GetDouble());}
            }
        });
        test("Muro · Rankine su terreno orizzontale e geometria analitica",()=>
        {
            var d=WallParameters.Defaults();d["Delta_T_M"]=0;d["Itp"]=0;d["PGA"]=0;d["Psi_Sovr"]=1;
            var r=WallCalculator.Calculate(d);Near(r.Static.Active,1d/3);Near(r.Static.Passive,3);Near(r.Seismic.Active,r.Static.Active);
            Near(r.Overturning,r.OverturningStatic);Near(r.Wall.Area,4.5*.6+(.4+.6)*6/2+.5*.5);
            var rectangle=WallGeometry.Properties([new(0,0),new(2,0),new(2,3),new(0,3)]);Near(rectangle.Area,6);Near(rectangle.Centroid.X,1);Near(rectangle.Centroid.Y,1.5);
        });
        test("Muro · JSON e MDST, decimali italiani, rifiuto file incompleti",()=>
        {
            var doc=WallFiles.Example();var json=WallFiles.Write(doc);Check(WallFiles.Write(WallFiles.Read(json))==json,"JSON non reversibile");
            var text=WallFiles.WriteMdst(doc);var imported=WallFiles.Read(text.Replace("0.75","0,75"));
            foreach(var p in WallParameters.All)Near(doc.Parameters[p.Key],imported.Parameters[p.Key]);
            Reject(()=>WallFiles.Read("{}"));Reject(()=>WallFiles.Read("H1=6\n"));Reject(()=>WallFiles.Read(text+"\nH1=5"));
            Reject(()=>WallFiles.Read(text.Replace("H1=6","H1=NaN")));Reject(()=>WallFiles.Read(json.Replace("muro-sostegno","muratura")));
            Reject(()=>WallFiles.Read(json.Replace("\"schemaVersion\": 1","\"schemaVersion\": 2")));
        });
        test("Muro · validazione geometrica, dominio sismico e numeri non finiti",()=>
        {
            foreach(var (key,value) in new (string,double)[]{("H1",0),("Bf",1),("Xd",5),("Hta",7),("Vs",0),("Tm",0),("PGA",-1),("FI_T",0),("Delta_T_M",40),("It",300),("Psi_Sovr",2),("GammaTerreno",double.NaN)})
            {var d=WallParameters.Defaults();d[key]=value;Reject(()=>WallCalculator.Calculate(d));}
            var extreme=WallParameters.Defaults();extreme["Vs"]=209;extreme["PGA"]=3;Reject(()=>WallCalculator.Calculate(extreme));
        });
        test("Muro · falda e Vs: limiti visibili, nessun esito normativo inventato",()=>
        {
            var d=WallParameters.Defaults();var r=WallCalculator.Calculate(d);
            Check(r.Notes.Any(n=>n.Contains("anomalia"))&&r.Notes.Any(n=>n.Contains("0,209")),"Mancano note per i dati originali");
            d["Qfp"]=-1;d["Vs"]=209;r=WallCalculator.Calculate(d);Check(!r.Notes.Any(n=>n.Contains("anomalia")||n.Contains("0,209")),"Note non aggiornate");
            Check(r.Pressures.Count==4,"Diagramma incompleto");Near(r.Pressures[0].Value,r.Static.Active*d["Sovraccarico"]);
        });
        test("Muro rivisto · caso Rankine: spinte, ribaltamento EQU+M2 e scorrimento A1+M1+R3 calcolati a mano",()=>
        {
            // Terreno orizzontale, δ = 0, niente dente, acqua, sovraccarico e sisma: formule chiuse indipendenti dal motore.
            var d=WallParameters.Defaults();d["Delta_T_M"]=0;d["Itp"]=0;d["It"]=0;d["PGA"]=0;d["Sovraccarico"]=0;d["Qfp"]=-10;d["Qfa"]=-10;d["H4"]=0;
            var r=WallDesignCalculator.Calculate(d);var g=r.Geometry;const double gamma=19;
            var h=d["H2"]+d["Htp"];var hp=d["H2"]+d["Hta"]; // spinta da y = 6,6 a 0; passiva da y = 1,6 a 0
            var phiD=Math.Atan(Math.Tan(Math.PI/6)/1.25);var kaD=(1-Math.Sin(phiD))/(1+Math.Sin(phiD));
            var sumWx=r.Wall.Area*25*r.Wall.Centroid.X+r.FrontSoil.Area*gamma*r.FrontSoil.Centroid.X+r.BackSoil.Area*gamma*r.BackSoil.Centroid.X;
            var o=r.StaticOverturning;Near(o.KaOverturning,kaD);Near(o.OverturningMoment,1.1*kaD*gamma*Math.Pow(h,3)/6);Near(o.StabilizingMoment,.9*sumWx);
            Near(o.Overturning.Ratio!.Value,.9*sumWx/(1.15*1.1*kaD*gamma*Math.Pow(h,3)/6));
            var s=r.StaticSliding;Near(s.KaSliding,1d/3);Near(s.Kp,3);
            var weight=r.Wall.Area*25+(r.FrontSoil.Area+r.BackSoil.Area)*gamma;
            Near(s.Driving,1.3*gamma*h*h/6);Near(s.Normal,weight);Near(s.Passive,3*gamma*hp*hp/2);
            Near(s.Sliding.Resistance,weight*Math.Tan(Math.PI/6)+.5*s.Passive*1.1/1.4);
            Check(r.SeismicSliding.Kh==0&&Math.Abs(r.SeismicSliding.Driving-gamma*h*h/6)<1e-9,"Sisma nullo non coincidente con la statica a γ = 1");
        });
        test("Muro rivisto · acqua idrostatica, sovraccarico come sola azione, KP con pendenza a valle, sisma",()=>
        {
            var d=WallParameters.Defaults();d["Delta_T_M"]=0;d["Itp"]=0;d["It"]=0;d["PGA"]=0;d["Sovraccarico"]=0;d["H4"]=0;d["Qfp"]=1;d["Qfa"]=1;
            var r=WallDesignCalculator.Calculate(d).StaticSliding;const double gw=WallDesignCalculator.GammaW;
            // Falda a quota 1 m su entrambi i lati, base a y = 0: spinte d'acqua γw·h²/2 uguali e sottospinta uniforme γw·h·Bf.
            Near(r.WaterBack,gw/2);Near(r.WaterFront,gw/2);Near(r.Uplift,gw*1*4.5);
            // Terreno a monte: γ·(6,6−1) sopra falda + γ′ sotto falda.
            var sigma=19*5.6;Near(r.EarthThrust,(sigma*5.6/2+sigma*1+(19-gw)*.5)/3);
            // Il sovraccarico aumenta la spinta ma non la resistenza (correzione dell'anomalia del sorgente).
            var noQ=WallDesignCalculator.Calculate(d).StaticSliding;d["Sovraccarico"]=10;var withQ=WallDesignCalculator.Calculate(d).StaticSliding;
            Near(withQ.SurchargeThrust,10*6.6/3);Check(withQ.Driving>noQ.Driving&&withQ.Sliding.Resistance>=noQ.Sliding.Resistance,"Sovraccarico trattato come resistenza");
            // KP dipende dalla pendenza a valle It, non da quella a monte Itp.
            d["Itp"]=20;var kpBack=WallDesignCalculator.Calculate(d).StaticSliding.Kp;d["It"]=20;var kpFront=WallDesignCalculator.Calculate(d).StaticSliding.Kp;
            Near(kpBack,3);Check(kpFront<3,"KP non ridotto dalla pendenza a valle");
            // Sisma: kv in entrambi i versi, inerzie presenti; αH senza fattore g e B_H continuo.
            d=WallParameters.Defaults();d["Vs"]=200;var s=WallDesignCalculator.Calculate(d);
            var ratio=6/(.3*200);Near(s.BH,1-(1-.4)/(.4-.05)*(ratio-.05));Near(s.Kh,s.BH*.2);
            Check(s.Cases.Count(c=>c.Kv!=0)==2&&s.SeismicSliding.Inertia>0,"Combinazioni sismiche incomplete");
            d["Vs"]=6/(.3*.4);Near(WallDesignCalculator.SeismicCoefficient(d).BH,.4);d["Vs"]=6/(.3*.3999999);Check(Math.Abs(WallDesignCalculator.SeismicCoefficient(d).BH-.4)<1e-5,"B_H discontinuo");
            d["PGA"]=3;Reject(()=>WallDesignCalculator.Calculate(d));
        });
        test("Muro · grip del disegno: posizione ↔ parametri, aggancio e rifiuto geometrie non ammesse",()=>
        {
            var d=WallParameters.Defaults();var g=WallGeometry.Create(d);
            // Ogni grip, rilasciato nella propria posizione, deve restituire gli stessi parametri (andata e ritorno).
            foreach(var grip in WallEditing.Grips)
            {
                var next=WallEditing.Drag(d,grip.Id,grip.Anchor(g),.001);Check(next is not null,$"Grip {grip.Id} rifiutato nella posizione iniziale");
                foreach(var (key,value) in d)Check(Math.Abs(next![key]-value)<1e-3,$"Grip {grip.Id} altera {key}: {next[key]} ≠ {value}");
            }
            // Spostamento con aggancio a 5 cm lungo il solo asse del grip.
            var h1=WallEditing.Drag(d,"H1",new(9,7.62),.05)!;Near(h1["H1"],7);Check(h1.Count(p=>p.Value!=d[p.Key])==1,"H1 modifica altri parametri");
            var s2=WallEditing.Drag(d,"S2",new(2.03,99),.05)!;Near(s2["S2"],.85);
            // Bf trascina con sé il dente quando è allineato al bordo posteriore (Xd + S3 = Bf nei dati di esempio).
            var bf=WallEditing.Drag(d,"Bf",new(5,0),.05)!;Near(bf["Bf"],5);Near(bf["Xd"],4.5);
            // H2 = H3: la base resta orizzontale.
            var h2=WallEditing.Drag(d,"H2",new(0,.8),.05)!;Near(h2["H2"],.8);Near(h2["H3"],.8);
            // Pendenza posteriore dal punto estremo della superficie.
            var itp=WallEditing.Drag(d,"Itp",new(4.5,6.6+.54),.01)!;Near(itp["Itp"],20);
            // Geometrie non ammesse: nessun valore restituito.
            Check(WallEditing.Drag(d,"Bf",new(.5,0),.05) is null,"Bf minore di Ba + S2 accettato");
            Check(WallEditing.Drag(d,"H1",new(0,-3),.05) is null,"H1 negativa accettata");
            Check(WallEditing.Drag(d,"Sconosciuto",new(0,0),.05) is null,"Grip inesistente accettato");
        });
    }
}
