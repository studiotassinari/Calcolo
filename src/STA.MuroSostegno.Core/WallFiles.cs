using System.Globalization;
using System.Text;
using System.Text.Json;
namespace STA.MuroSostegno.Core;

/// <summary>Progetto indipendente dalla finestra; JSON versionato e compatibilità con i file .mdst dello ZIP.</summary>
public sealed record WallDocument(int SchemaVersion, string Module, string Title, Dictionary<string,double> Parameters);
public static class WallFiles
{
    public const string ModuleId = "muro-sostegno";
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    public static WallDocument Example() => new(1, ModuleId, "Muro di sostegno", WallParameters.Defaults());
    public static void Validate(WallDocument doc)
    {
        if (doc is null || doc.SchemaVersion != 1 || doc.Module != ModuleId || doc.Parameters is null || doc.Title is null)
            throw new InvalidDataException("Formato progetto muro di sostegno non riconosciuto.");
        WallValidation.Validate(doc.Parameters);
    }
    public static string Write(WallDocument doc) { Validate(doc); return JsonSerializer.Serialize(doc,Json); }
    public static string WriteResults(WallDocument doc, WallResult result)
    { Validate(doc); return JsonSerializer.Serialize(new { Input=doc, Result=result },Json); }
    public static WallDocument Read(string text)
    {
        var trimmed = text.TrimStart('\uFEFF',' ','\t','\r','\n');
        if (trimmed.StartsWith('{'))
        {
            var doc = JsonSerializer.Deserialize<WallDocument>(trimmed,Json) ?? throw new InvalidDataException("Progetto vuoto.");
            Validate(doc); return doc;
        }
        var values = new Dictionary<string,double>();
        foreach (var raw in trimmed.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith('[')) continue;
            var eq = line.IndexOf('=');
            if (eq < 1) throw new InvalidDataException($"Riga MDST non valida: {line}");
            var key = line[..eq].Trim();
            if (!WallParameters.All.Any(p => p.Key == key)) continue;
            if (!double.TryParse(line[(eq+1)..].Trim().Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out var value) || !double.IsFinite(value))
                throw new InvalidDataException($"{key}: valore MDST non valido.");
            if (!values.TryAdd(key,value)) throw new InvalidDataException($"Parametro duplicato: {key}.");
        }
        // Un import incompleto non deve mescolare valori nuovi con quelli del progetto già aperto.
        var imported = new WallDocument(1,ModuleId,"Muro importato da MDST",values);
        Validate(imported); return imported;
    }
    public static string WriteMdst(WallDocument doc)
    {
        Validate(doc); var sb = new StringBuilder("# MuroDiSostegno - File dati .mdst\n");
        foreach (var group in WallParameters.All.GroupBy(p => p.Group))
        {
            sb.AppendLine($"\n[{group.Key}]");
            foreach (var p in group) sb.AppendLine($"{p.Key}={doc.Parameters[p.Key].ToString("R",CultureInfo.InvariantCulture)}");
        }
        return sb.ToString();
    }
}
