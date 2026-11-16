namespace STA.Taglio.Core;

public sealed record ShearResult(
    double D, double Fcd, double Fyd, double Rho, double K,
    double SigmaCp, double AlphaCw, double WallFactor,
    double Concrete, double Reinforcement, double Strut, double Capacity,
    double? Utilization, bool Pass, double SuggestedTheta, double SuggestedCapacity);
