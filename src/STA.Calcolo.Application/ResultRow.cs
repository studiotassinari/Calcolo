namespace STA.Calcolo.Application;

/// <summary>Risultato numerico con spiegazione opzionale; i moduli esistenti restano compatibili.</summary>
public sealed record ResultRow(string Grandezza, double Valore, string Unità, double? Periodo = null,
    string? Formula = null, string? Sostituzione = null, string? Riferimento = null)
{
    public string Equazione => string.IsNullOrEmpty(Formula) ? "" :
        $"{Formula} = {Sostituzione} = {Valore.ToString("0.###", System.Globalization.CultureInfo.GetCultureInfo("it-IT"))} {Unità}";
}
