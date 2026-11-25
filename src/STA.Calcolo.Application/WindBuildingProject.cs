using System.Globalization;
using Vento.Aerodinamica;
namespace STA.Calcolo.Application;

/// <summary>Dati completi del modulo Vento sulle costruzioni: vento del sito (campi di WindWorkspace), geometria e pressione interna.</summary>
public sealed record WindBuildingInput(IReadOnlyDictionary<string, string> Site, GeometriaCostruzione Geometry, CasoPressioneInterna InternalCase, FacciaEdificio DominantFace);

/// <summary>Esito del calcolo: vento del sito letto e validato, e azioni sulle superfici.</summary>
public sealed record WindBuildingResult(WindWorkspace.State Site, RisultatoAzioni Actions);

/// <summary>
/// Progetto del modulo Vento sulle costruzioni: conversione da e verso il documento JSON comune
/// (<see cref="ModuleDocument"/>, schema 1) e calcolo. Lunghezze in metri, angoli in gradi; i numeri
/// sono scritti con il punto decimale e letti con punto o virgola. Il documento viene validato per
/// intero prima di essere restituito, così un file errato non modifica il lavoro aperto.
/// </summary>
public static class WindBuildingProject
{
    public const string ModuleId = "vento-costruzione";
    /// <summary>Campi di WindWorkspace usati dal modulo: altezza e cp derivano dalla geometria e dai coefficienti calcolati.</summary>
    public static readonly string[] SiteKeys = ["modello", "zona", "rugosita", "distanza", "categoria", "quota", "ct", "cd"];
    private static readonly string[] BuildingKeys = ["tipologia", "direzione", "schema", "b", "d", "h", "alfa", "freccia", "tratti", "bloccaggio", "pressioneInterna", "facciaDominante"];
    private static readonly WindWorkspace Workspace = new();

    /// <summary>Campi del vento del sito, con etichette, valori predefiniti e scelte di WindWorkspace.</summary>
    public static IReadOnlyList<ModuleField> SiteFields => Workspace.Fields.Where(f => SiteKeys.Contains(f.Key)).ToArray();

    public static ModuleDocument Write(WindBuildingInput input)
    {
        var g = input.Geometry; var c = CultureInfo.InvariantCulture;
        var fields = SiteKeys.ToDictionary(k => k, k => input.Site[k]);
        fields["tipologia"] = g.Tipologia.ToString(); fields["direzione"] = g.Direzione.ToString(); fields["schema"] = g.SchemaMultiplo.ToString();
        fields["b"] = g.B.ToString("R", c); fields["d"] = g.D.ToString("R", c); fields["h"] = g.H.ToString("R", c);
        fields["alfa"] = g.Alfa.ToString("R", c); fields["freccia"] = g.Freccia.ToString("R", c);
        fields["tratti"] = g.NumeroTratti.ToString(c); fields["bloccaggio"] = g.GradoBloccaggio.ToString("R", c);
        fields["pressioneInterna"] = input.InternalCase.ToString(); fields["facciaDominante"] = input.DominantFace.ToString();
        return new(1, ModuleId, fields, "");
    }

    public static WindBuildingInput Read(ModuleDocument document)
    {
        if (document == null || document.SchemaVersion != 1 || document.Module != ModuleId || document.Fields == null || document.Fields.Values.Any(v => v == null))
            throw new ArgumentException("Il file non è un progetto del modulo Vento sulle costruzioni.");
        var expected = SiteKeys.Concat(BuildingKeys).Order();
        if (!document.Fields.Keys.Order().SequenceEqual(expected)) throw new ArgumentException("Parametri del progetto incompleti o sconosciuti.");
        string Text(string key) => document.Fields[key].Trim();
        T Choice<T>(string key) where T : struct, Enum => Enum.TryParse<T>(Text(key), out var value) && Enum.IsDefined(value) ? value : throw new ArgumentException($"{key}: scelta non valida.");
        double Number(string key) => ModuleInput.Number(Text(key));
        if (!int.TryParse(Text("tratti"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int spans) || spans < 1 || spans > 50) throw new ArgumentException("tratti: numero intero fra 1 e 50.");
        var geometry = new GeometriaCostruzione
        {
            Tipologia = Choice<TipologiaCostruzione>("tipologia"), Direzione = Choice<DirezioneVento>("direzione"), SchemaMultiplo = Choice<SchemaFaldeMultiple>("schema"),
            B = Number("b"), D = Number("d"), H = Number("h"), Alfa = Number("alfa"), Freccia = Number("freccia"), NumeroTratti = spans, GradoBloccaggio = Number("bloccaggio")
        };
        var input = new WindBuildingInput(SiteKeys.ToDictionary(k => k, Text), geometry, Choice<CasoPressioneInterna>("pressioneInterna"), Choice<FacciaEdificio>("facciaDominante"));
        // Anche il vento del sito deve essere leggibile: un valore fuori intervallo viene rifiutato qui, non dopo.
        SiteState(input);
        return input;
    }

    /// <summary>Documento per WindWorkspace: i campi non mostrati assumono valori neutri (cp = 1) e l'altezza della costruzione.</summary>
    private static WindWorkspace.State SiteState(WindBuildingInput input)
    {
        var fields = Workspace.Fields.ToDictionary(f => f.Key, f => f.Default);
        foreach (var key in SiteKeys) fields[key] = input.Site[key];
        double top = input.Geometry.AltezzaSommità;
        fields["altezza"] = Math.Clamp(double.IsFinite(top) ? top : 1, .01, 200).ToString(CultureInfo.InvariantCulture);
        fields["cp"] = "1";
        return Workspace.Read(new(1, Workspace.Id, fields, ""));
    }

    /// <summary>Calcola le azioni. Solleva ArgumentException se il vento del sito o la quota escono dal campo del modulo.</summary>
    public static WindBuildingResult Calculate(WindBuildingInput input)
    {
        var state = SiteState(input);
        var actions = CalcolatoreAzioni.Calcola(new(state.Qr, state.Category, state.Ct, state.Cd), input.Geometry, input.InternalCase, input.DominantFace);
        if (actions.Azioni.Any(r => !double.IsFinite(r.Pressione))) throw new ArgumentException("Risultati non finiti: controllare la geometria.");
        return new(state, actions);
    }
}
