using System.Globalization;
using Calcestruzzo;
namespace STA.Calcolo.Application;

/// <summary>Presenta formule, sostituzioni e riferimenti del motore NTC 2018, separato dalla vista.</summary>
public sealed class ConcreteWorkspace : ICalculationWorkspace
{
    public string Id => "calcestruzzo";
    public string Title => "Calcestruzzo";
    public string Description => "NTC 2018 · proprietà progettuali a 28 giorni, calcestruzzo di peso normale. αcc = 0,85; γc = 1,5.";
    public IReadOnlyList<ModuleField> Fields => [
        new("classe", "Classe di resistenza", "C25_30",
            Enum.GetValues<ClassiNormale>().Where(c => c != ClassiNormale.C100_115).OrderBy(c => new global::Calcestruzzo.Calcestruzzo(c).Rck).Select(c => c.ToString()).ToArray()),
        new("riciclo", "Aggregati grossi di riciclo (miscela qualificata, Tab. 11.2.III)", "No", ["No", "Sì"]),
        new("vapore", "Maturazione a vapore", "No", ["No", "Sì"]),
        new("ecm", "Ecm da prove [MPa] · vuoto = stima NTC", ""),
        new("elemento", "Elemento per le resistenze di progetto", "Ordinario", ["Ordinario", "Piano gettato in opera, spessore < 50 mm"])];
    public string DataHint => "";
    public string ExampleData => "";

    /// <summary>Migra solo il contratto storico completo a un campo; non nasconde file incompleti o sconosciuti.</summary>
    public ModuleDocument Normalize(ModuleDocument document)
    {
        if (document != null && document.SchemaVersion == 1 && document.Module == Id &&
            document.Fields != null && document.Fields.Count == 1 && document.Fields.ContainsKey("classe"))
        {
            var values = Fields.ToDictionary(f => f.Key, f => f.Default);
            values["classe"] = document.Fields["classe"];
            document = document with { Fields = values };
        }
        new ModuleInput(document!, this).Validate();
        return document!;
    }

    public IReadOnlyList<ResultRow> Calculate(ModuleDocument document)
    {
        document = Normalize(document);
        var input = new ModuleInput(document, this);
        bool recycled = input.Text("riciclo") == "Sì", steam = input.Text("vapore") == "Sì";
        double? measured = string.IsNullOrWhiteSpace(input.Text("ecm")) ? null : ModuleInput.Number(input.Text("ecm"));
        var m = new ConcreteNtc2018(input.Choice<ClassiNormale>("classe"), recycled, steam, measured,
            input.Text("elemento") != "Ordinario");
        string F(double v) => v.ToString("0.########", CultureInfo.GetCultureInfo("it-IT"));
        string tensile = m.Rck > 60 ? "2,12 · ln(1 + fcm / 10)" : "0,30 · fck^(2/3)";
        string values = m.Rck > 60 ? $"2,12 · ln(1 + {F(m.Fcm)} / 10)" : $"0,30 · {F(m.Fck)}^(2/3)";
        if (recycled) { tensile = "0,90 · (" + tensile + ")"; values = "0,90 · (" + values + ")"; }
        string factor = m.ElementFactor < 1 ? "0,80 · " : "";
        // Mantiene l'ordine dei primi sette risultati; aggiunge i frattili e la resistenza di progetto a trazione.
        return [
            new("Rck", m.Rck, "MPa", Formula: "Rck dalla classe cubica", Sostituzione: F(m.Rck), Riferimento: "NTC 2018 · Tab. 4.1.I"),
            new("fck", m.Fck, "MPa", Formula: "fck = 0,83 · Rck", Sostituzione: $"0,83 · {F(m.Rck)}", Riferimento: "NTC 2018 · [11.2.1]"),
            new("fcd", m.Fcd, "MPa", Formula: $"fcd = {factor}αcc · fck / γc", Sostituzione: $"{factor}0,85 · {F(m.Fck)} / 1,5", Riferimento: "NTC 2018 · §4.1.2.1.1.1"),
            new("fcm", m.Fcm, "MPa", Formula: "fcm = fck + 8", Sostituzione: $"{F(m.Fck)} + 8", Riferimento: "NTC 2018 · [11.2.2]"),
            new("fctm", m.Fctm, "MPa", Formula: "fctm = " + tensile, Sostituzione: values, Riferimento: "NTC 2018 · §11.2.10.2"),
            new("fcfm", m.Fcfm, "MPa", Formula: "fcfm = 1,20 · fctm", Sostituzione: $"1,20 · {F(m.Fctm)}", Riferimento: "NTC 2018 · [11.2.4]"),
            new("Ecm", m.Ecm, "MPa", Formula: measured.HasValue ? "Ecm da prove" : "Ecm = " + (recycled ? "0,80 · " : "") + "22000 · (fcm / 10)^0,3",
                Sostituzione: measured.HasValue ? F(measured.Value) : (recycled ? "0,80 · " : "") + $"22000 · ({F(m.Fcm)} / 10)^0,3", Riferimento: "NTC 2018 · §11.2.10.3"),
            new("fctk,0.05", m.Fctk05, "MPa", Formula: "fctk,0.05 = 0,70 · fctm", Sostituzione: $"0,70 · {F(m.Fctm)}", Riferimento: "NTC 2018 · §11.2.10.2"),
            new("fctk,0.95", m.Fctk95, "MPa", Formula: "fctk,0.95 = 1,30 · fctm", Sostituzione: $"1,30 · {F(m.Fctm)}", Riferimento: "NTC 2018 · §11.2.10.2"),
            new("fctd", m.Fctd, "MPa", Formula: $"fctd = {factor}fctk,0.05 / γc", Sostituzione: $"{factor}{F(m.Fctk05)} / 1,5", Riferimento: "NTC 2018 · §4.1.2.1.1.2")];
    }
}
