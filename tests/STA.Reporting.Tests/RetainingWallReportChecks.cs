using System.IO.Compression;
using System.Xml.Linq;
using PdfSharp.Pdf.IO;
using STA.Calcolo.Reporting;
using STA.MuroSostegno.Core;

/// <summary>Relazione del muro: pagine A4 senza sforamenti, figure incorporate, formule modificabili e sostituzioni numeriche.</summary>
internal static class RetainingWallReportChecks
{
    private static void Assert(bool condition,string message){if(!condition)throw new Exception(message);}
    /// <summary>PNG RGB valido di dimensioni date (riempito di bianco) per simulare le figure prodotte dall'applicazione.</summary>
    public static byte[] Png(int width,int height)
    {
        using var raw=new MemoryStream();
        for(var y=0;y<height;y++){raw.WriteByte(0);for(var x=0;x<width*3;x++)raw.WriteByte(255);}
        using var compressed=new MemoryStream();using(var z=new ZLibStream(compressed,CompressionLevel.Fastest,true))raw.WriteTo(z);
        using var png=new MemoryStream();png.Write([137,80,78,71,13,10,26,10]);
        void Chunk(string type,byte[] data)
        {
            var name=System.Text.Encoding.ASCII.GetBytes(type);
            png.Write(BitConverter.GetBytes(System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(data.Length)));png.Write(name);png.Write(data);
            var crc=System.IO.Hashing.Crc32.HashToUInt32([..name,..data]);png.Write(BitConverter.GetBytes(System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(crc)));
        }
        var header=new byte[13];System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header,width);System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4),height);header[8]=8;header[9]=2;
        Chunk("IHDR",header);Chunk("IDAT",compressed.ToArray());Chunk("IEND",[]);
        return png.ToArray();
    }
    public static void Run(byte[] logo,byte[] regular,byte[] bold)
    {
        var figures=new[]{"sezione","statica","ribaltamento","pressione","sisma"}.ToDictionary(k=>k,_=>Png(2460,1074));
        foreach(var (name,change) in new (string,Action<Dictionary<string,double>>)[]{("esempio",_=>{}),("vs-200",d=>d["Vs"]=200),("senza-falda",d=>{d["Qfp"]=-5;d["Qfa"]=-5;d["H4"]=0;})})
        {
            var doc=WallFiles.Example();change(doc.Parameters);var result=WallDesignCalculator.Calculate(doc.Parameters);
            var report=RetainingWallReportBuilder.Build(doc,result,figures,new(2026,11,18));
            Assert(report.Pages.Count==13&&report.Pages.Sum(p=>p.Figures?.Count??0)==5,"Pagine o figure mancanti");
            Assert(report.Pages[0].Tables[0].Rows.Any(r=>r[0].Contains("NTC 2018")),"Inquadramento normativo mancante");
            var equations=report.Pages.SelectMany(p=>p.Equations??[]).ToArray();
            Assert(equations.Length>=18&&equations.All(e=>e.Substitution.Length>0),"Formule senza sostituzione numerica");
            Assert(equations.Any(e=>e.Substitution.Contains(result.StaticSliding.Driving.ToString("N1",System.Globalization.CultureInfo.GetCultureInfo("it-IT")))),"Ed non riportata");
            var docx=WordReportWriter.Write(report,logo);using var zip=new ZipArchive(new MemoryStream(docx));
            Assert(zip.Entries.Count(e=>e.FullName.StartsWith("word/media/figure"))==5,"Figure non incorporate nel Word");
            using var xml=zip.GetEntry("word/document.xml")!.Open();var w=XDocument.Load(xml);XNamespace m="http://schemas.openxmlformats.org/officeDocument/2006/math";
            Assert(w.Descendants(m+"f").Count()>=10,"Frazioni non modificabili");
            // Il PDF fallisce se una pagina supera l'area A4 utile: la generazione stessa è la verifica d'impaginazione.
            var pdf=PdfReportWriter.Write(report,logo,regular,bold);using var opened=PdfReader.Open(new MemoryStream(pdf),PdfDocumentOpenMode.Import);
            Assert(opened.PageCount==13,"Pagine PDF errate");
            Directory.CreateDirectory("artifacts/reports");File.WriteAllBytes($"artifacts/reports/muro-{name}.pdf",pdf);File.WriteAllBytes($"artifacts/reports/muro-{name}.docx",docx);
            Console.WriteLine($"PASS relazione muro {name}: 13 pagine A4, 5 figure, {equations.Length} formule compilate");
        }
    }
}
