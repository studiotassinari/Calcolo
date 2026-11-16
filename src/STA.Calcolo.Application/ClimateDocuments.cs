namespace STA.Calcolo.Application;

/// <summary>Migra solo i contratti storici esatti; non maschera file incompleti o campi sconosciuti.</summary>
public static class ClimateDocuments
{
    public static ModuleDocument Normalize(ModuleDocument doc, ICalculationWorkspace module)
    {
        if (doc?.Fields == null) throw new ArgumentException("Progetto incompleto.");
        string[] oldKeys = module.Id == "neve" ? ["zona", "topografia", "quota", "falda"] : ["zona", "rugosita", "distanza", "quota", "altezza"];
        if (doc.Fields.Keys.Order().SequenceEqual(oldKeys.Order()))
        {
            var fields = module.Fields.ToDictionary(f => f.Key, f => f.Default);
            foreach (var pair in doc.Fields) fields[pair.Key] = pair.Value;
            // I file vento precedenti conservano esplicitamente il modello numerico storico.
            if (module.Id == "vento") fields["modello"] = WindWorkspace.Legacy;
            doc = doc with { Fields = fields };
        }
        new ModuleInput(doc, module).Validate();
        return doc;
    }
}
