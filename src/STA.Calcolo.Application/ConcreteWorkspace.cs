using System.Globalization;
namespace STA.Calcolo.Application;

/// <summary>Ogni formula illustrata usa gli stessi parametri e risultati del motore materiale.</summary>
public sealed class ConcreteWorkspace : ICalculationWorkspace
{
    public string Id => "calcestruzzo";
    public string Title => "Calcestruzzo";
    public string Description => "Formule del modello storico NTC 2008. fck = 0,83 Rck (conversione storica), αcc = 0,85; γc = 1,5.";
    public IReadOnlyList<ModuleField> Fields => [new("classe", "Classe", "C25_30", Enum.GetNames<Calcestruzzo.ClassiNormale>())];
    public string DataHint => "";
    public string ExampleData => "";
    public IReadOnlyList<ResultRow> Calculate(ModuleDocument document)
    {
        var input = new ModuleInput(document, this); input.Validate();
        var m = new Calcestruzzo.Calcestruzzo(input.Choice<Calcestruzzo.ClassiNormale>("classe"));
        string F(double v) => v.ToString("0.########", CultureInfo.GetCultureInfo("it-IT"));
        bool high = m.Classe > Calcestruzzo.ClassiNormale.C50_60;
        // La soglia segue la classe nominale, come nel motore storico; ln indica il logaritmo naturale.
        return [
            new("Rck", m.Rck, "MPa", Formula: "Rck dalla classe cubica", Sostituzione: F(m.Rck)),
            new("fck", m.fck, "MPa", Formula: "fck = 0,83 · Rck", Sostituzione: $"0,83 · {F(m.Rck)}"),
            new("fcd", m.fcd, "MPa", Formula: "fcd = αcc · fck / γc", Sostituzione: $"0,85 · {F(m.fck)} / 1,5"),
            new("fcm", m.fcm, "MPa", Formula: "fcm = fck + 8", Sostituzione: $"{F(m.fck)} + 8"),
            new("fctm", m.fctm, "MPa", Formula: high ? "fctm = 2,12 · ln(1 + fcm / 10)" : "fctm = 0,30 · fck^(2/3)",
                Sostituzione: high ? $"2,12 · ln(1 + {F(m.fcm)} / 10)" : $"0,30 · {F(m.fck)}^(2/3)"),
            new("fcfm", m.fcfm, "MPa", Formula: "fcfm = 1,20 · fctm", Sostituzione: $"1,20 · {F(m.fctm)}"),
            new("Ecm", m.Ecm, "MPa", Formula: "Ecm = 22000 · (fcm / 10)^0,3", Sostituzione: $"22000 · ({F(m.fcm)} / 10)^0,3")];
    }
}
