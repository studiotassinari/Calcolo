namespace STA.Calcolo.Reporting;

/// <summary>Blocco di un capitolo di relazione, nell'ordine in cui compare nel documento.</summary>
public abstract record ChapterBlock;
/// <summary>Titolo di livello 1–3: usa gli stili Titolo 1, 2, 3 del modello, con la loro numerazione automatica.</summary>
public sealed record ChapterHeading(int Level, string Text) : ChapterBlock;
/// <summary>Paragrafo di testo nello stile Normale del modello.</summary>
public sealed record ChapterParagraph(string Text) : ChapterBlock;
/// <summary>Voce di elenco nello stile "Paragrafo elenco" del modello.</summary>
public sealed record ChapterBullet(string Text) : ChapterBlock;
/// <summary>Formula in notazione <see cref="ReportMath"/>, resa come equazione nativa di Word, con una riga di spiegazione.</summary>
public sealed record ChapterFormula(string Formula, string Explanation = "") : ChapterBlock;
/// <summary>
/// Tabella con didascalia. Widths sono i pesi relativi delle colonne (null = uguali);
/// le prime TextColumns colonne sono allineate a sinistra, le altre a destra perché numeriche.
/// </summary>
public sealed record ChapterTable(string Caption, string[] Headers, IReadOnlyList<string[]> Rows, double[]? Widths = null, int TextColumns = 1) : ChapterBlock;
/// <summary>Figura PNG centrata con didascalia; Scale (0–1] è la frazione della larghezza utile.</summary>
public sealed record ChapterFigure(byte[] Png, string Caption, double Scale = 1) : ChapterBlock;

/// <summary>
/// Capitolo di relazione da inserire in un documento più ampio: un titolo di primo livello seguito dai blocchi.
/// Non ha frontespizio né sommario.
/// </summary>
public sealed record ReportChapter(string Title, IReadOnlyList<ChapterBlock> Blocks);
