namespace STA.Calcolo.Application;

/// <summary>Associazione provincia-zona trascritta dal DM 17/01/2018, §3.4.2.
/// Le denominazioni sono quelle della norma, non un servizio di confini amministrativi aggiornati.</summary>
public static class SnowGeography
{
    public const string Manual = "Zona manuale";
    private static readonly string[][] Groups =
    [
        "Aosta|Belluno|Bergamo|Biella|Bolzano|Brescia|Como|Cuneo|Lecco|Pordenone|Sondrio|Torino|Trento|Udine|Verbano-Cusio-Ossola|Vercelli|Vicenza".Split('|'),
        "Alessandria|Ancona|Asti|Bologna|Cremona|Forlì-Cesena|Lodi|Milano|Modena|Monza Brianza|Novara|Parma|Pavia|Pesaro e Urbino|Piacenza|Ravenna|Reggio Emilia|Rimini|Treviso|Varese".Split('|'),
        "Arezzo|Ascoli Piceno|Avellino|Bari|Barletta-Andria-Trani|Benevento|Campobasso|Chieti|Fermo|Ferrara|Firenze|Foggia|Frosinone|Genova|Gorizia|Imperia|Isernia|L'Aquila|La Spezia|Lucca|Macerata|Mantova|Massa Carrara|Padova|Perugia|Pescara|Pistoia|Prato|Rieti|Rovigo|Savona|Teramo|Trieste|Venezia|Verona".Split('|'),
        "Agrigento|Brindisi|Cagliari|Caltanissetta|Carbonia-Iglesias|Caserta|Catania|Catanzaro|Cosenza|Crotone|Enna|Grosseto|Latina|Lecce|Livorno|Matera|Medio Campidano|Messina|Napoli|Nuoro|Ogliastra|Olbia-Tempio|Oristano|Palermo|Pisa|Potenza|Ragusa|Reggio Calabria|Roma|Salerno|Sassari|Siena|Siracusa|Taranto|Terni|Trapani|Vibo Valentia|Viterbo".Split('|')
    ];
    public static string[] Provinces => [Manual, ..Groups.SelectMany(g => g).OrderBy(p => p, StringComparer.Ordinal)];
    public static string Zone(string province)
    {
        int index = Array.FindIndex(Groups, g => g.Contains(province));
        if (index < 0) throw new ArgumentException("Provincia non presente nella zonazione NTC 2018.");
        return new STA.Carichi.Neve.CaricoNeve().ListaZone[index].Nome;
    }
}
