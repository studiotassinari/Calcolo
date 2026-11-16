namespace TaglioClsRect;
// Test-only equivalents of the MathNet calls in the reference source.
internal static class Trig
{
    public static double DegreeToRadian(double x) => x * System.Math.PI / 180;
    public static double Cot(double x) => 1 / System.Math.Tan(x);
    public static double Sin(double x) => System.Math.Sin(x);
    public static double Cos(double x) => System.Math.Cos(x);
    public static double Tan(double x) => System.Math.Tan(x);
}
internal static class Bisection
{
    public static double FindRoot(System.Func<double,double> f, double lo, double hi, double accuracy)
    {
        var flo=f(lo);
        if(flo*f(hi)>0)throw new System.ArgumentException("Root not bracketed");
        while(hi-lo>accuracy)
        {
            var mid=(lo+hi)/2;
            if(flo*f(mid)<=0)hi=mid;else{lo=mid;flo=f(mid);}
        }
        return (lo+hi)/2;
    }
}
