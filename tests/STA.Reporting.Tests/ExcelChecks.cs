using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using STA.Calcolo.Reporting;
using STA.Muratura.Core;

internal static class ExcelChecks
{
    public static void Verify(MasonryProject project,string name)
    {
        byte[] bytes=MasonryExcelWriter.Write(project);
        using var zip=new ZipArchive(new MemoryStream(bytes));
        XNamespace s="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        using var stream=zip.GetEntry("xl/worksheets/sheet1.xml")!.Open();var sheet=XDocument.Load(stream);
        var cells=sheet.Descendants(s+"c").ToDictionary(c=>c.Attribute("r")!.Value);
        double Value(string address)=>double.Parse(cells[address].Element(s+"v")!.Value,CultureInfo.InvariantCulture);
        void Near(string a,double expected){if(Math.Abs(Value(a)-expected)>1e-8*Math.Max(1,Math.Abs(expected)))throw new Exception("Excel result "+a);}
        var before=MasonryCalculator.Total(project.Before);var after=MasonryCalculator.Total(project.After);var group=FrameGroupCalculator.Calculate(project.AllFrames,before,after);
        Near("F84",before.Vt);Near("F85",after.Vt);Near("F88",before.K);Near("F89",after.K);Near("F95",group.Veff);Near("F96",group.K);Near("F97",group.FinalV);Near("F100",group.FinalK);Near("E51",before.Rows[0].Sigma/10);
        Near("E106",project.Before.Sum(p=>p.L)-project.After.Sum(p=>p.L));
        Near("E107",group.K/after.K);Near("E108",group.Veff/after.Vt);
        string xml=sheet.ToString();
        if(xml.Contains("#REF!")||xml.Contains("BONFIGLIOLI")||xml.Contains("CARICO SUPERFICIALE")||zip.Entries.Any(e=>e.FullName.Contains("externalLink")))throw new Exception("Unrelated source data in Excel");
        foreach(var formula in sheet.Descendants(s+"f"))if(formula.Value.Contains('[')||formula.Value.Contains("_xlfn"))throw new Exception("External or unsupported formula");
        if(!sheet.Descendants(s+"dataValidation").Any(v=>v.Element(s+"formula1")?.Value=="\"235,275,355\""))throw new Exception("Steel list missing");
        using var styles=zip.GetEntry("xl/styles.xml")!.Open();var styleXml=XDocument.Load(styles);
        int count=styleXml.Root!.Element(s+"cellXfs")!.Elements().Count();
        if(cells.Values.Any(c=>int.Parse(c.Attribute("s")?.Value??"0")>=count))throw new Exception("Missing style");
        Directory.CreateDirectory("artifacts/reports");File.WriteAllBytes($"artifacts/reports/test-{name}.xlsx",bytes);
        File.WriteAllText($"artifacts/reports/test-{name}.json",MasonryFiles.Write(project));
        Console.WriteLine("PASS Excel "+name+": results, pressure, disabled/multiple frames, styles and no external data");
    }
}
