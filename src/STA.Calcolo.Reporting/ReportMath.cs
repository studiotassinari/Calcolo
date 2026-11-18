using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using PdfSharp.Drawing;
namespace STA.Calcolo.Reporting;

/// <summary>
/// Formule strutturate della relazione, scritte in una notazione ridotta simile a LaTeX:
/// <c>\frac{num}{den}</c>, <c>\sqrt{x}</c>, apici <c>x^{2}</c>, pedici <c>x_{d}</c>, gruppi <c>{…}</c>; il resto è testo Unicode.
/// La stessa formula diventa matematica nativa di Word (OMML: m:f, m:rad, m:sSub, m:sSup) e viene impaginata
/// nel PDF con righe di frazione lunghe quanto il termine più largo.
/// </summary>
public static class ReportMath
{
    /// <summary>Nodi della formula: testo, riga, frazione, radice, indici.</summary>
    public abstract record Node;
    public sealed record Text(string Value) : Node;
    public sealed record Row(IReadOnlyList<Node> Items) : Node;
    public sealed record Fraction(Node Numerator, Node Denominator) : Node;
    public sealed record Root(Node Body) : Node;
    public sealed record Script(Node Base, Node? Sub, Node? Sup) : Node;

    // ── Analisi della notazione ───────────────────────────────────────────────
    public static Node Parse(string formula) { var i = 0; var row = ParseRow(formula, ref i, false); return row; }

    private static Row ParseRow(string s, ref int i, bool inGroup)
    {
        var items = new List<Node>(); var text = new StringBuilder();
        void Flush() { if (text.Length > 0) { items.Add(new Text(text.ToString())); text.Clear(); } }
        while (i < s.Length)
        {
            var c = s[i];
            if (c == '}' && inGroup) { i++; break; }
            if (c == '{') { Flush(); i++; items.Add(ParseRow(s, ref i, true)); continue; }
            if (s.AsSpan(i).StartsWith("\\frac")) { Flush(); i += 5; var num = Group(s, ref i); var den = Group(s, ref i); items.Add(new Fraction(num, den)); continue; }
            if (s.AsSpan(i).StartsWith("\\sqrt")) { Flush(); i += 5; items.Add(new Root(Group(s, ref i))); continue; }
            if (c is '^' or '_')
            {
                Flush(); i++; var arg = Group(s, ref i);
                // Base dell'indice: il nodo precedente, oppure l'ultima parola (lettere, cifre, apice ′) del testo precedente.
                Node baseNode = new Text("");
                if (items.Count > 0)
                {
                    var last = items[^1]; items.RemoveAt(items.Count - 1);
                    if (last is Text t)
                    {
                        var m = Regex.Match(t.Value, @"[\p{L}\p{N}′]+$");
                        var split = m.Success ? m.Index : Math.Max(0, t.Value.Length - 1);
                        if (split > 0) items.Add(new Text(t.Value[..split]));
                        baseNode = new Text(t.Value[split..]);
                    }
                    else baseNode = last;
                }
                // Pedice seguito da apice (o viceversa) sullo stesso termine: un solo nodo con entrambi gli indici.
                if (baseNode is Script sc && (c == '^' ? sc.Sup is null : sc.Sub is null))
                    items.Add(c == '^' ? sc with { Sup = arg } : sc with { Sub = arg });
                else items.Add(c == '^' ? new Script(baseNode, null, arg) : new Script(baseNode, arg, null));
                continue;
            }
            text.Append(c); i++;
        }
        Flush();
        return new Row(items);
    }
    private static Node Group(string s, ref int i)
    {
        if (i < s.Length && s[i] == '{') { i++; return ParseRow(s, ref i, true); }
        // Argomento senza graffe: un solo carattere.
        return i < s.Length ? new Text(s[i++].ToString()) : new Text("");
    }

    // ── Word (OMML) ───────────────────────────────────────────────────────────
    /// <summary>Contenuto di m:oMath. Le parole di due o più lettere (sin, cos, tan, stab…) sono in tondo; le singole lettere restano in corsivo matematico.</summary>
    public static string Omml(string formula) => Omml(Parse(formula));
    private static string Omml(Node node) => node switch
    {
        Row r => string.Concat(r.Items.Select(Omml)),
        Fraction f => $"<m:f><m:num>{Omml(f.Numerator)}</m:num><m:den>{Omml(f.Denominator)}</m:den></m:f>",
        Root q => $"<m:rad><m:radPr><m:degHide m:val=\"1\"/></m:radPr><m:deg/><m:e>{Omml(q.Body)}</m:e></m:rad>",
        Script { Sub: not null, Sup: not null } s => $"<m:sSubSup><m:e>{Omml(s.Base)}</m:e><m:sub>{Omml(s.Sub)}</m:sub><m:sup>{Omml(s.Sup)}</m:sup></m:sSubSup>",
        Script { Sub: not null } s => $"<m:sSub><m:e>{Omml(s.Base)}</m:e><m:sub>{Omml(s.Sub)}</m:sub></m:sSub>",
        Script s => $"<m:sSup><m:e>{Omml(s.Base)}</m:e><m:sup>{Omml(s.Sup!)}</m:sup></m:sSup>",
        Text t => Runs(t.Value),
        _ => ""
    };
    private static string Runs(string text)
    {
        if (text.Length == 0) return "";
        var sb = new StringBuilder();
        foreach (Match m in Regex.Matches(text, @"\p{L}{2,}|[^\p{L}]+|\p{L}"))
        {
            var plain = m.Value.Length >= 2 && m.Value.All(char.IsLetter);
            sb.Append("<m:r>").Append(plain ? "<m:rPr><m:sty m:val=\"p\"/></m:rPr>" : "")
              .Append("<m:t xml:space=\"preserve\">").Append(SecurityElement.Escape(m.Value)).Append("</m:t></m:r>");
        }
        return sb.ToString();
    }

    // ── PDF ───────────────────────────────────────────────────────────────────
    /// <summary>Scatola tipografica: larghezza, ascendente e discendente rispetto alla linea di base, e disegno.</summary>
    private sealed record Box(double Width, double Ascent, double Descent, Action<XGraphics, double, double> Draw);

    /// <summary>Disegna la formula a partire da (x; y) in alto a sinistra; riduce il corpo se supera la larghezza disponibile. Restituisce l'altezza occupata.</summary>
    public static double DrawPdf(XGraphics g, string formula, double x, double y, double maxWidth, double size, XColor color)
    {
        var node = Parse(formula);
        var box = Layout(g, node, size, color);
        if (box.Width > maxWidth) box = Layout(g, node, size * maxWidth / box.Width, color);
        box.Draw(g, x, y + box.Ascent);
        return box.Ascent + box.Descent;
    }

    private static Box Layout(XGraphics g, Node node, double size, XColor color)
    {
        var brush = new XSolidBrush(color);
        switch (node)
        {
            case Text t:
            {
                var font = new XFont("Arial", size, XFontStyleEx.Regular);
                var width = t.Value.Length == 0 ? 0 : g.MeasureString(t.Value, font).Width;
                return new(width, size * .78, size * .24, (gr, x, baseline) => { if (t.Value.Length > 0) gr.DrawString(t.Value, font, brush, new XPoint(x, baseline)); });
            }
            case Row r:
            {
                var boxes = r.Items.Select(i => Layout(g, i, size, color)).ToArray();
                if (boxes.Length == 0) return new(0, size * .78, size * .24, (_, _, _) => { });
                return new(boxes.Sum(b => b.Width), boxes.Max(b => b.Ascent), boxes.Max(b => b.Descent), (gr, x, baseline) =>
                { foreach (var b in boxes) { b.Draw(gr, x, baseline); x += b.Width; } });
            }
            case Fraction f:
            {
                // Riga di frazione lunga quanto il termine più largo più un piccolo margine, centrata sull'asse matematico.
                var num = Layout(g, f.Numerator, size * .92, color); var den = Layout(g, f.Denominator, size * .92, color);
                double pad = size * .18, gap = size * .2, axis = size * .3, width = Math.Max(num.Width, den.Width) + 2 * pad;
                return new(width + size * .15, axis + gap + num.Ascent + num.Descent, gap + den.Ascent + den.Descent - axis, (gr, x, baseline) =>
                {
                    var line = baseline - axis;
                    gr.DrawLine(new XPen(color, size * .055), x, line, x + width, line);
                    num.Draw(gr, x + (width - num.Width) / 2, line - gap - num.Descent);
                    den.Draw(gr, x + (width - den.Width) / 2, line + gap + den.Ascent);
                });
            }
            case Root q:
            {
                var body = Layout(g, q.Body, size, color); double sign = size * .55, over = size * .14;
                return new(sign + body.Width + size * .12, body.Ascent + over + size * .06, body.Descent, (gr, x, baseline) =>
                {
                    var pen = new XPen(color, size * .055); var topLine = baseline - body.Ascent - over;
                    gr.DrawLines(pen, [new XPoint(x, baseline - body.Ascent * .35), new XPoint(x + sign * .3, baseline - body.Ascent * .5),
                        new XPoint(x + sign * .55, baseline + body.Descent), new XPoint(x + sign, topLine), new XPoint(x + sign + body.Width + size * .08, topLine)]);
                    body.Draw(gr, x + sign, baseline);
                });
            }
            case Script s:
            {
                var b = Layout(g, s.Base, size, color);
                var sub = s.Sub is null ? null : Layout(g, s.Sub, size * .68, color);
                var sup = s.Sup is null ? null : Layout(g, s.Sup, size * .68, color);
                double raise = Math.Max(b.Ascent * .55, size * .4), lower = size * .22;
                var width = b.Width + Math.Max(sub?.Width ?? 0, sup?.Width ?? 0) + size * .05;
                return new(width, Math.Max(b.Ascent, sup is null ? 0 : raise + sup.Ascent), Math.Max(b.Descent, sub is null ? 0 : lower + sub.Descent), (gr, x, baseline) =>
                {
                    b.Draw(gr, x, baseline);
                    sup?.Draw(gr, x + b.Width + size * .03, baseline - raise);
                    sub?.Draw(gr, x + b.Width + size * .03, baseline + lower);
                });
            }
            default: return new(0, 0, 0, (_, _, _) => { });
        }
    }
}
