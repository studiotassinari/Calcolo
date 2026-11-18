namespace STA.Calcolo.Reporting;
/// <summary>
/// Figura esplicativa della relazione: immagine PNG (prodotta dall'applicazione, ad esempio il disegno del muro)
/// con titolo e didascalia. Scale (0–1] è la frazione della larghezza utile della pagina, centrata. AspectRatio = larghezza/altezza, usato da entrambi gli scrittori per l'impaginazione.
/// </summary>
public sealed record ReportFigure(string Title,byte[] Png,string Caption,double Scale=1)
{
    /// <summary>Rapporto larghezza/altezza letto dall'intestazione IHDR del PNG (byte 16–23, big-endian).</summary>
    public double AspectRatio
    {
        get
        {
            if(Png.Length<24)return 2;
            int Read(int offset)=>(Png[offset]<<24)|(Png[offset+1]<<16)|(Png[offset+2]<<8)|Png[offset+3];
            var width=Read(16);var height=Read(20);
            return width>0&&height>0?(double)width/height:2;
        }
    }
}
