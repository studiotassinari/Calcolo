using STA.MuroSostegno.Core.Legacy;
using STA.MuroSostegno.Core.Legacy.Geotecnica;
namespace STA.MuroSostegno.Core;

/// <summary>Risultati per metro di sviluppo del muro. Nessun esito normativo completo viene dedotto dai rapporti storici.</summary>
public sealed record WallPressure(double Active, double Passive, double PsiActive, double PsiPassive, double AlphaH, double Theta);
public sealed record WallActions(double G1, double G2, double Q);
public sealed record WallPressurePoint(double Elevation, double Value);
public sealed record WallResult(WallGeometry Geometry, PolygonProperties Wall, PolygonProperties FrontSoil,
    PolygonProperties BackSoil, WallPressure Seismic, WallPressure Static, double Stabilizing, double StabilizingNominal,
    double Overturning, double OverturningNominal, double OverturningStatic, double OverturningStaticNominal,
    WallActions SlidingThrust, WallActions SlidingResistance, IReadOnlyList<WallPressurePoint> Pressures, IReadOnlyList<string> Notes)
{
    public double? SeismicRatio => Overturning > 1e-12 ? Stabilizing / Overturning : null;
    public double? StaticRatio => OverturningStatic > 1e-12 ? Stabilizing / OverturningStatic : null;
}

/// <summary>Orchestra le stesse chiamate del pulsante Calcola originale, senza UI o stato statico condiviso.</summary>
public static class WallCalculator
{
    public const string Scope = "Motore originale TA-MuroDiSostegno: risultati di confronto, non verifica normativa completa. Scorrimento: componenti separate, senza esito complessivo.";
    public static WallResult Calculate(IReadOnlyDictionary<string,double> d)
    {
        var g = WallGeometry.Create(d);
        var w = WallGeometry.LegacyPoints(g.Wall); var f = WallGeometry.LegacyPoints(g.FrontSoil); var b = WallGeometry.LegacyPoints(g.BackSoil);
        double Rad(double degrees) => degrees * Math.PI / 180;
        var fi = Rad(d["FI_T"]); var delta = Rad(d["Delta_T_M"]);
        var gm = d["GammaMuro"]; var gt = d["GammaTerreno"]; var q = d["Sovraccarico"];
        // L'inclinazione è ricavata dalla pendenza percentuale, come nel programma consegnato.
        var beta = Math.Atan(d["Itp"] / 100);
        SpinteDelTerreno.EarthPressureSismaResult Pressure(double pga) => SpinteDelTerreno.Calculate(fi, delta, beta, gt,
            d["H1"], d["Vs"], pga, d["Csi_H"], 9.81, d["Tm"]);
        var se = Pressure(d["PGA"]); var st = Pressure(0);
        var checks = new Verifiche();
        checks.MommentoStabilizzante(w,f,b,gm,gt,q,out var stab,out var stabNom);
        void Overturn(double ka, double load, double pga, out double design, out double nominal) =>
            checks.MomentoRibaltamento(w,f,b,ka,gm,gt,fi,delta,d["C1k"],load,gt,d["H1"],d["Vs"],pga,d["Csi_H"],9.81,d["Tm"],d["Qfp"],out design,out nominal);
        Overturn(se.KAEy,q*d["Psi_Sovr"],d["PGA"],out var rib,out var ribNom);
        Overturn(st.KAEy,q,0,out var ribStatic,out var ribStaticNom);
        checks.SpintaScivolamnto(w,f,b,se.KAEy,gm,gt,fi,delta,d["C1k"],q*d["Psi_Sovr"],gt,d["H1"],d["Vs"],d["PGA"],d["Csi_H"],9.81,d["Tm"],d["Qfp"],out var t1,out var t2,out var tq);
        checks.ResistenzaScivolamnto(w,f,b,st.KAEy,st.KPEy,gm,gt,fi,delta,d["C1k"],q,gt,d["H1"],d["Vs"],0,d["Csi_H"],9.81,d["Tm"],d["Qfp"],out var r1,out var r2,out var rq);
        WallPressure ToPressure(SpinteDelTerreno.EarthPressureSismaResult p) => new(p.KAEy,p.KPEy,p.PsiA*180/Math.PI,p.PsiP*180/Math.PI,p.AlfaH,p.TethaEq*180/Math.PI);
        if (new[] { stab,stabNom,rib,ribNom,ribStatic,ribStaticNom,t1,t2,tq,r1,r2,rq,se.KAEy,se.KPEy,st.KAEy,st.KPEy }.Any(v => !double.IsFinite(v)))
            throw new ArgumentException("La combinazione dei parametri produce risultati non finiti: controllare geometria, falda e sisma.");
        List<string> notes = [Scope,
            "I coefficienti parziali A1 del sorgente sono applicati anche al caso con PGA; i rapporti Mstab/Mrib non sono un attestato di sicurezza.",
            "La coesione c′ₖ è conservata ma non utilizzata nelle formule; Qfa è utilizzata soltanto nel disegno.",
            "Capacità portante, stabilità globale e dimensionamento delle armature non sono inclusi nel motore originale."];
        if (d["Qfp"] > w[12].y)
            notes.Add("Falda sopra la base del dente: il ramo originale di scorrimento presenta un'anomalia di segno sul peso immerso (PesoMuro -= -10 × Area). Componenti riportate solo per confronto; modello idraulico da revisionare.");
        if (d["Vs"] < 10)
            notes.Add("Vs è inferiore a 10 m/s: il valore iniziale 0,209 è quello del sorgente. Verificare l'unità prima di usare i risultati; non è stata applicata una conversione automatica.");
        // Il diagramma riproduce le profondità locali Htp, Htp+H3 e Htp+H3+H4 del sorgente.
        var depths=new[]{0,d["Htp"],d["Htp"]+d["H3"],d["Htp"]+d["H3"]+d["H4"]};
        var pressures=depths.Select(z=>new WallPressurePoint(d["H2"]+d["Htp"]-z,st.KAEy*(gt*z+q))).ToArray();
        return new(g,WallGeometry.Properties(g.Wall),WallGeometry.Properties(g.FrontSoil),WallGeometry.Properties(g.BackSoil),
            ToPressure(se),ToPressure(st),stab,stabNom,rib,ribNom,ribStatic,ribStaticNom,new(t1,t2,tq),new(r1,r2,rq),pressures,notes);
    }
}
