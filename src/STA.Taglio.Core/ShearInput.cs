namespace STA.Taglio.Core;

/// <summary>Input in mm, mm², MPa and kN. N is positive in compression.
/// Cover is the distance to the centroid of the tensile reinforcement, not clear cover.</summary>
public sealed record ShearInput
{
    public bool Eurocode { get; init; }
    public bool Wall { get; init; }
    public double H { get; init; } = 500;
    public double B { get; init; } = 300;
    public double Cover { get; init; } = 40;
    public double Asl { get; init; } = 1200;
    public double Fck { get; init; } = 30;
    public double Fyk { get; init; } = 450;
    public double GammaC { get; init; } = 1.5;
    public double GammaS { get; init; } = 1.15;
    public double AlphaCc { get; init; } = .85;
    public double N { get; init; }
    public double V { get; init; } = 50;
    public double Diameter { get; init; } = 8;
    public double Spacing { get; init; } = 200;
    public double Legs { get; init; } = 2;
    public double Alpha { get; init; } = 90;
    public double BentDiameter { get; init; } = 10;
    public double BentSpacing { get; init; } = 400;
    public double BentCount { get; init; }
    public double BentAlpha { get; init; } = 45;
    public double Theta { get; init; } = 45;
}
