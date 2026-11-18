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
