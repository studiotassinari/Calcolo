using STA.Taglio.Core;
namespace STA.Calcolo.Application;

/// <summary>Adatta il motore di taglio al catalogo e al contratto condiviso da desktop e CLI.</summary>
public sealed class ShearWorkspace : ICalculationWorkspace
{
    public const string ModuleId = "taglio-cls";
    public string Id => ModuleId;
    public string Title => "Taglio · sezioni in c.a.";
    public string Description => "Travi e pareti rettangolari, staffe e ferri piegati. Sezione con quote modificabili.";
    public string DataHint => "";
    public string ExampleData => "Verifica a taglio";
    public IReadOnlyList<ModuleField> Fields { get; } =
    [
        new("Standard", "Modello di calcolo", "NTC 2018", ["NTC 2018", "EN 1992"]),
        new("Kind", "Tipologia", "Trave", ["Trave", "Parete"]),
        new("H", "H · altezza sezione / lunghezza parete [mm]", "500"),
        new("B", "b · base / spessore parete [mm]", "300"),
        new("Cover", "Distanza al baricentro armatura tesa [mm]", "40"),
        new("Asl", "Armatura longitudinale Asl [mm²]", "1200"),
        new("N", "N · compressione positiva [kN]", "0"),
        new("V", "VEd · valore assoluto [kN]", "50"),
        new("Fck", "fck [MPa]", "30"),
        new("Fyk", "fyk [MPa]", "450"),
        new("GammaC", "γc", "1.5"),
        new("GammaS", "γs", "1.15"),
        new("AlphaCc", "αcc", "0.85"),
        new("Diameter", "Diametro staffe [mm]", "8"),
        new("Spacing", "Interasse staffe [mm]", "200"),
        new("Legs", "Numero bracci (0 = assenti)", "2"),
        new("Alpha", "α staffe [°]", "90"),
        new("BentDiameter", "Diametro piegati [mm]", "10"),
        new("BentSpacing", "Interasse piegati [mm]", "400"),
        new("BentCount", "Numero piegati (0 = assenti)", "0"),
        new("BentAlpha", "α piegati [°]", "45"),
        new("Theta", "θ bielle [°]", "45")
    ];
    public ModuleDocument Example() => new(1, Id, Fields.ToDictionary(f => f.Key, f => f.Default), ExampleData);
    /// <summary>Convalida il documento e converte i campi testuali, mantenendo le unità del core.</summary>
    public ShearInput Input(ModuleDocument doc)
    {
        var data = new ModuleInput(doc, this); data.Validate();
        var input = new ShearInput
        {
            Eurocode = data.Text("Standard") == "EN 1992", Wall = data.Text("Kind") == "Parete",
            H = ModuleInput.Number(data.Text("H")),
            B = ModuleInput.Number(data.Text("B")),
            Cover = ModuleInput.Number(data.Text("Cover")),
            Asl = ModuleInput.Number(data.Text("Asl")),
            N = ModuleInput.Number(data.Text("N")),
            V = ModuleInput.Number(data.Text("V")),
            Fck = ModuleInput.Number(data.Text("Fck")),
            Fyk = ModuleInput.Number(data.Text("Fyk")),
            GammaC = ModuleInput.Number(data.Text("GammaC")),
            GammaS = ModuleInput.Number(data.Text("GammaS")),
            AlphaCc = ModuleInput.Number(data.Text("AlphaCc")),
            Diameter = ModuleInput.Number(data.Text("Diameter")),
            Spacing = ModuleInput.Number(data.Text("Spacing")),
            Legs = ModuleInput.Number(data.Text("Legs")),
            Alpha = ModuleInput.Number(data.Text("Alpha")),
            BentDiameter = ModuleInput.Number(data.Text("BentDiameter")),
            BentSpacing = ModuleInput.Number(data.Text("BentSpacing")),
            BentCount = ModuleInput.Number(data.Text("BentCount")),
            BentAlpha = ModuleInput.Number(data.Text("BentAlpha")),
            Theta = ModuleInput.Number(data.Text("Theta"))
        };
        ShearCalculator.Validate(input);
        return input;
    }
    public ShearResult Compute(ModuleDocument doc) => ShearCalculator.Calculate(Input(doc));
    public IReadOnlyList<ResultRow> Calculate(ModuleDocument doc) => Rows(Compute(doc));
    // Le stesse righe alimentano l'interfaccia generica e l'esportazione della CLI.
    public static IReadOnlyList<ResultRow> Rows(ShearResult r) =>
    [
        new("Altezza utile d", r.D, "mm"), new("fcd", r.Fcd, "MPa"), new("fyd", r.Fyd, "MPa"),
        new("ρl (limitata)", r.Rho, "—"), new("k", r.K, "—"), new("σcp per VRd,c", r.SigmaCp, "MPa"),
        new("αcw", r.AlphaCw, "—"), new("Fattore bielle parete", r.WallFactor, "—"),
        new("VRd,c · senza armatura a taglio", r.Concrete, "kN"),
        new("VRd,s · staffe e piegati", r.Reinforcement, "kN"),
        new("VRd,max · bielle", r.Strut, "kN"), new("VRd · configurazione attuale", r.Capacity, "kN"),
        new("θ proposto", r.SuggestedTheta, "°"), new("VRd al θ proposto", r.SuggestedCapacity, "kN")
    ];
}
