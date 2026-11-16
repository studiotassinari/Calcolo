namespace STA.Taglio.Core;

/// <summary>
/// Dati immutabili: lunghezze in mm, aree in mm², resistenze in MPa e forze in kN.
/// N è positivo a compressione. Cover è la distanza al baricentro dell'armatura tesa,
/// non il copriferro netto; per le pareti H è la lunghezza resistente in pianta.
/// </summary>
public sealed record ShearInput
{
    // Selezioni del modello storico; le formule non sono aggiornate durante la migrazione.
    public bool Eurocode { get; init; }
    public bool Wall { get; init; }
    // Geometria resistente della sezione rettangolare.
    public double H { get; init; } = 500;
    public double B { get; init; } = 300;
    public double Cover { get; init; } = 40;
    public double Asl { get; init; } = 1200;
    // Proprietà dei materiali e coefficienti parziali, tutti espliciti nel progetto.
    public double Fck { get; init; } = 30;
    public double Fyk { get; init; } = 450;
    public double GammaC { get; init; } = 1.5;
    public double GammaS { get; init; } = 1.15;
    public double AlphaCc { get; init; } = .85;
    // Azioni già di progetto: il motore non genera combinazioni né maggiorazioni sismiche.
    public double N { get; init; }
    public double V { get; init; } = 50;
    // Staffe: Legs = 0 esclude il contributo anche se diametro e interasse sono nulli.
    public double Diameter { get; init; } = 8;
    public double Spacing { get; init; } = 200;
    public double Legs { get; init; } = 2;
    public double Alpha { get; init; } = 90;
    // Ferri piegati: contributo distinto e sommato a quello delle staffe.
    public double BentDiameter { get; init; } = 10;
    public double BentSpacing { get; init; } = 400;
    public double BentCount { get; init; }
    public double BentAlpha { get; init; } = 45;
    // Inclinazione delle bielle in gradi; tutti i risultati correnti usano questo angolo.
    public double Theta { get; init; } = 45;
}
