using System.Globalization;
namespace STA.Calcolo.Application;

/// <summary>Versioned native projects and read-only import of original .tgl inputs.</summary>
public static class ShearFiles
{
    public static ModuleDocument Read(string content)
    {
        if (content.TrimStart('\uFEFF', ' ', '\r', '\n', '\t').StartsWith("{"))
        {
            var doc = ModuleFiles.Read<ModuleDocument>(content.TrimStart('\uFEFF'));
            new ShearWorkspace().Input(doc);
            return doc;
        }
        return ImportLegacy(content);
    }
    public static string Write(ModuleDocument doc)
    {
        new ShearWorkspace().Input(doc);
        return ModuleFiles.Write(doc);
    }
    public static ModuleDocument ImportLegacy(string content)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var raw in content.TrimStart('\uFEFF').Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith('[')) continue;
            int at = line.IndexOf('=');
            if (at <= 0 || !values.TryAdd(line[..at].Trim(), line[(at + 1)..].Trim()))
                throw new ArgumentException("File .tgl non valido: riga o chiave duplicata.");
        }
        string Text(string key) => values.TryGetValue(key, out var s) ? s : throw new ArgumentException("File .tgl incompleto: " + key);
        bool Flag(string key) => bool.TryParse(Text(key), out var value) ? value : throw new ArgumentException("Valore booleano non valido: " + key);
        int Index(string key, int length)
        {
            if (!int.TryParse(Text(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) || index < 0 || index >= length)
                throw new ArgumentException("Indice .tgl non valido: " + key);
            return index;
        }
        var doc = new ShearWorkspace().Example();
        var map = new Dictionary<string, string>
        {
            ["H"]="H", ["B"]="B", ["Cover"]="Ricoprimento", ["Asl"]="ArmaturaLongitudinale",
            ["N"]="NCompressione", ["V"]="Ved", ["Fck"]="fck", ["Fyk"]="fyk",
            ["GammaC"]="GammaC", ["GammaS"]="GammaS", ["Spacing"]="InterasseStaffe",
            ["Legs"]="NumeroBracci", ["Alpha"]="AlfaStaffe", ["BentSpacing"]="InterassePiegati",
            ["BentCount"]="NumeroPiegati", ["BentAlpha"]="AlfaPiegati", ["Theta"]="Theta"
        };
        foreach (var (target, source) in map) doc.Fields[target] = Text(source);
        int[] diameters = [6,8,10,12,14,16,18,20,22,24,26,28,30];
        doc.Fields["Diameter"] = diameters[Index("DiametroStaffe", diameters.Length)].ToString(CultureInfo.InvariantCulture);
        doc.Fields["BentDiameter"] = diameters[Index("DiametroPiegati", diameters.Length)].ToString(CultureInfo.InvariantCulture);
        doc.Fields["AlphaCc"] = Index("AlfaCC", 2) == 0 ? "0.85" : "1";
        bool ntc = Flag("NTC2018"), en = Flag("EN1992"), beam = Flag("Trave"), wall = Flag("Parete");
        if (ntc == en || beam == wall) throw new ArgumentException("Normativa o tipologia .tgl ambigua.");
        doc.Fields["Standard"] = ntc ? "NTC 2018" : "EN 1992"; doc.Fields["Kind"] = wall ? "Parete" : "Trave";
        doc = doc with { Data = Text("Titolo") };
        new ShearWorkspace().Input(doc);
        return doc;
    }
}
