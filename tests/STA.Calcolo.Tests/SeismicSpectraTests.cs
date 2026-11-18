using STA.Calcolo.Application;
using STA.Carichi.Sisma;

/// <summary>Spettri NTC 2018: parità con il motore storico, formule indipendenti, topografia, smorzamento, verticale e progetto.</summary>
internal static class SeismicSpectraTests
{
    private static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    private static void Near(double a,double b,double tol=1e-9)=>Check(double.IsFinite(a)&&Math.Abs(a-b)<=tol*Math.Max(1,Math.Abs(b)),$"Spettri: {a:R} ≠ {b:R}");
    public static void Run(Action<string,Action> test)
    {
        var site=new SeismicSiteInput(12.19944,44.41778,50,"II",TipiTerreni.C);
        test("Spettri · orizzontale con ST = 1 e ξ = 5 % uguale al motore storico",()=>
        {
            var r=SeismicSpectra.Calculate(site);
            var legacy=new Sisma(site.Longitude,site.Latitude);legacy.TipoCostruzione=legacy.ElencoViteNominali.Single(v=>v.Vn==50);
            legacy.ClasseUso=legacy.ElencoClassiUso.Single(v=>v.ID=="II");legacy.CategoriaSottosuolo=legacy.ElencoCategorieSottosuolo.Single(v=>v.Categoria==TipiTerreni.C);legacy.FattoreStruttura=1;legacy.creaSpettri();
            foreach(var s in legacy.spettri)
            {
                var state=r.State(s.statoLimite.ToString());Near(state.Ag,s.numAgMedia);Near(state.AgG,s.numAgMedia/9.81);Near(state.Tc,s.numTc);
                foreach(var v in s.valoriSpettrali)Near(SeismicSpectra.Horizontal(state,v.periodo,1)*9.81,v.risposta,1e-9);
            }
            Near(r.ReferencePeriod,50);Near(r.Eta,1);Check(r.Curves.Count==9,"Curve mancanti");
        });
        test("Spettri · formule NTC 2018 indipendenti: plateau, ST, η, verticale e limite del progetto",()=>
        {
            var r=SeismicSpectra.Calculate(site with {Topography=TopographyCategory.T4,Damping=10,Q=4});var s=r.State("SLV");
            Near(s.St,1.4);Near(s.S,s.Ss*1.4);Near(s.AmaxG,s.S*s.AgG);Near(s.Amax,s.AmaxG*9.81);
            var eta=Math.Sqrt(10/15d);Near(r.Eta,eta);
            Near(SeismicSpectra.Horizontal(s,(s.Tb+s.Tc)/2,eta),s.AgG*s.S*eta*s.F0);Near(SeismicSpectra.Horizontal(s,0,eta),s.AgG*s.S);
            Near(SeismicSpectra.Horizontal(s,2*s.Td,eta),s.AgG*s.S*eta*s.F0*s.Tc*s.Td/(4*s.Td*s.Td));
            Near(s.Fv,1.35*s.F0*Math.Sqrt(s.AgG));Near(SeismicSpectra.Vertical(s,.1,eta),s.AgG*1.4*eta*s.Fv);Near(SeismicSpectra.Vertical(s,0,eta),s.AgG*1.4);
            Near(SeismicSpectra.Design(s,(s.Tb+s.Tc)/2,4),s.AgG*s.S*s.F0/4);Near(SeismicSpectra.Design(s,10,4),.2*s.AgG);
            Check(SeismicSpectra.Eta(50)==.55,"η minimo 0,55");
            var curve=r.Curves.Single(c=>c.Name=="SLV H");Near(SeismicSpectra.At(curve.Points,s.Tc),SeismicSpectra.Horizontal(s,s.Tc,eta),1e-9);
            Check(curve.Points.Any(p=>p.X==s.Tb)&&curve.Points.Any(p=>p.X==s.Tc),"Punti d'angolo non campionati");
            Check(r.States.Select(x=>x.Tr).SequenceEqual(new[]{30,50,475,975}),"Periodi di ritorno errati: "+string.Join(",",r.States.Select(x=>x.Tr)));
        });
        test("Spettri · input fuori dominio rifiutati, ZIP con tutte le curve",()=>
        {
            foreach(var bad in new[]{site with {Longitude=5},site with {Latitude=49},site with {NominalLife=30},site with {UseClass="V"},site with {Q=0.5},site with {Damping=0}})
            {try{SeismicSpectra.Calculate(bad);throw new Exception("Input non valido accettato");}catch(ArgumentException){}}
            var zip=new System.IO.Compression.ZipArchive(new MemoryStream(SeismicSpectra.Zip(SeismicSpectra.Calculate(site),"{}")));
            Check(zip.Entries.Count==11&&zip.Entries.Any(e=>e.Name=="SLV-progetto-H.txt"),"ZIP incompleto");
        });
        test("Ricerca indirizzo · risposta Nominatim interpretata senza rete",()=>
        {
            var json="[{\"lat\":\"44.4177800\",\"lon\":\"12.1994400\",\"display_name\":\"Ravenna, Emilia-Romagna, Italia\"},{\"lat\":\"x\",\"lon\":\"1\",\"display_name\":\"scartato\"}]";
            var places=GeocodingResults.Parse(json);Check(places.Count==1&&places[0].Name.StartsWith("Ravenna"),"Risultati non letti");Near(places[0].Point.Latitude,44.41778);Near(places[0].Point.Longitude,12.19944);
            Check(GeocodingResults.SearchUri("Via Roma 1, Ravenna").AbsoluteUri.Contains("q=Via%20Roma%201%2C%20Ravenna")&&GeocodingResults.SearchUri("x").AbsoluteUri.Contains("countrycodes=it"),"URI di ricerca errato");
        });
    }
}
