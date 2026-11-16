namespace STA.Taglio.Core;

/// <summary>
/// Esito immutabile: D in mm; Fcd, Fyd e SigmaCp in MPa; resistenze in kN;
/// SuggestedTheta in gradi. Utilization è VEd/VRd e vale null se la capacità è nulla.
/// Pass confronta la sola resistenza a taglio, non tutte le verifiche della struttura.
/// </summary>
public sealed record ShearResult(
    double D, double Fcd, double Fyd, double Rho, double K,
    double SigmaCp, double AlphaCw, double WallFactor,
    double Concrete, double Reinforcement, double Strut, double Capacity,
    double? Utilization, bool Pass, double SuggestedTheta, double SuggestedCapacity);
