using STA.Calcolo.Contracts;
using STA.Plinti.Core;

/// <summary>Motore rivisto dei plinti: confronti con soluzioni in forma chiusa indipendenti dal motore.</summary>
internal static class FootingDesignTests
{
    private static void Check(bool ok,string m){if(!ok)throw new Exception(m);}
    private static void Near(double a,double b,double tol=1e-9)=>Check(double.IsFinite(a)&&Math.Abs(a-b)<=tol*Math.Max(1,Math.Abs(b)),$"Plinti: {a:R} ≠ {b:R}");
    public static void Run(Action<string,Action> test,FootingParameters p,NodeReaction r)
    {
        // Esempio: B = L = 3 m, h = 0,5 m, pilastro 0,4×0,4, FZ = 900 kN, γG = 1,3 → N = 900 + 1,3·112,5 = 1046,25 kN.
        FootingDesignResult Run(NodeReaction row,FootingParameters? q=null)=>FootingDesignCalculator.Calculate([row],q??p)[0];
        FootingCheck C(FootingDesignResult x,string name)=>x.Checks.Single(c=>c.Name==name);
        test("Plinti rivisto · carico centrato: pressione, flessione, taglio e punzonamento in forma chiusa",()=>
        {
            var x=Run(r);Near(x.N,1046.25);Near(x.Pressure.SigmaMax,1046.25/9);Near(x.Pressure.ContactRatio,1);
            // Pressione netta 116,25 − 1,3·0,5·25 = 100 kPa; mensola a = 1,3 m → m = 100·1,3²/2.
            Near(x.MomentX,100*1.3*1.3/2,1e-9);Near(x.MomentY,x.MomentX);
            var d=.5-.05-.024;Near(x.D,d);Near(C(x,"Taglio direzione X").Demand,100*(1.3-d)/(d*1000),1e-9);
            // Punzonamento: min sui perimetri a = k·d/10 della formula EC2 con pressione netta uniforme (tolleranza della discretizzazione).
            var rho=Math.Min(1000/150d*Math.PI*64/(1000*d*1000),.02);var k=Math.Min(1+Math.Sqrt(200/(d*1000)),2);var vrdc=Math.Max(.18/1.5*k*Math.Cbrt(100*rho*30),.035*Math.Pow(k,1.5)*Math.Sqrt(30));
            var best=Enumerable.Range(1,20).Select(i=>i*d/10).Select(a=>{var u=1.6+2*Math.PI*a;var ain=.16+2*a*.8+Math.PI*a*a;var v=1.5*(900-100*ain)/(u*d*1000);return vrdc*2*d/a/v;}).Min();
            Near(C(x,"Punzonamento").Ratio!.Value,best,1e-2);
            Near(C(x,"Capacità portante").Capacity,250*9);Check(C(x,"Scorrimento").Ratio is null&&x.Ok,"Esito centrato errato");
            // Armatura: blocco rettangolare 0,8x, fcd = 0,85·30/1,5 = 17 MPa, fyd = 450/1,15.
            double dm=d*1000,m=84.5e6,kk=m/(17*1000*dm*dm),z=dm-dm*(1-Math.Sqrt(1-2*kk))/2;Near(x.AsReqX,Math.Max(m/(z*450/1.15),Math.Max(.26*.3*Math.Pow(30,2d/3)/450,.0013)*1000*dm),1e-9);
        });
        test("Plinti rivisto · base parzializzata uniassiale e biassiale nel nocciolo",()=>
        {
            var x=Run(r with {MY=-700});var e=700/1046.25;Check(e>.5&&x.Pressure.ContactRatio<1,"Parzializzazione non rilevata");
            Near(x.Pressure.SigmaMax,2*1046.25/(3*3*(1.5-e)),5e-3);Near(x.Pressure.ContactRatio,3*(1.5-e)/3,2e-2);
            // Nel nocciolo il piano coincide con Navier: σ = N/A ± My/Wy ± Mx/Wx.
            var y=Run(r with {MX=40,MY=-60});var n=1046.25;Near(y.Pressure.SigmaMax,n/9+60*6/27d+40*6/27d);Near(y.Pressure.SigmaMin,n/9-60*6/27d-40*6/27d);
            var z=Run(r with {MY=-2000});Check(!z.Pressure.Valid&&!z.Ok&&z.Issues.Any(i=>i.Contains("ribaltamento")),"Ribaltamento non rilevato");
        });
        test("Plinti rivisto · capacità portante EC7 Annex D e scorrimento con γR",()=>
        {
            var q=p with {Bearing="computed",Phi=30,Cohesion=0,SoilWeight=19,Depth=1};var x=Run(r,q);
            var phi=Math.PI/6;var nq=Math.Exp(Math.PI*Math.Tan(phi))*Math.Pow(Math.Tan(Math.PI/4+phi/2),2);var ng=2*(nq-1)*Math.Tan(phi);
            Near(nq,18.401,1e-4);Near(ng,20.093,1e-4);
            var qlim=19*1*nq*(1+Math.Sin(phi))+.5*19*3*ng*.7;Near(x.QLim,qlim);
            // Rinterro sopra la soletta: (9 − 0,16)·0,5·19; N = 900 + 1,3·(112,5 + 83,98).
            var w=112.5+(9-.16)*.5*19;Near(x.N,900+1.3*w);Near(C(x,"Capacità portante").Capacity,qlim*9/2.3);
            var s=Run(r with {FX=100});Near(C(s,"Scorrimento").Capacity,.4*(900+112.5)/1.1);Near(C(s,"Scorrimento").Demand,100);
            Check(Run(r with {FX=100},q).QLim<qlim,"Inclinazione del carico non applicata");
            var sls=Run(r,p with {LimitState="SLS"});Check(sls.Checks.Count==1&&sls.Checks[0].Name.StartsWith("Pressione")&&Math.Abs(sls.N-1012.5)<1e-9,"SLE errato");
            try{FootingDesignCalculator.Calculate([r],q with {Phi=0});throw new Exception("φ = 0 accettato");}catch(ArgumentException){}
            Check(p.Bearing=="assigned"&&p.GammaRSliding==1.1&&p.Depth==0,"Valori predefiniti dei progetti precedenti errati");
        });
    }
}
