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
        using var summaryStream=zip.GetEntry("xl/worksheets/sheet2.xml")!.Open();var summary=XDocument.Load(summaryStream);
        var summaryCells=summary.Descendants(s+"c").ToDictionary(c=>c.Attribute("r")!.Value);
        double Value(string address)=>double.Parse((address=="E51"?cells:summaryCells)[address].Element(s+"v")!.Value,CultureInfo.InvariantCulture);
        void Near(string a,double expected){if(Math.Abs(Value(a)-expected)>1e-8*Math.Max(1,Math.Abs(expected)))throw new Exception("Excel result "+a);}
        var before=MasonryCalculator.Total(project.Before);var after=MasonryCalculator.Total(project.After);var group=FrameGroupCalculator.Calculate(project.AllFrames,before,after);
        Near("F4",before.Vt);Near("F5",after.Vt);Near("F8",before.K);Near("F9",after.K);Near("F15",group.Veff);Near("F16",group.K);Near("F17",group.FinalV);Near("F20",group.FinalK);Near("E51",before.Rows[0].Sigma/10);
        Near("E26",project.Before.Sum(p=>p.L)-project.After.Sum(p=>p.L));
        Near("E27",group.K/after.K);Near("E28",group.Veff/after.Vt);
        if(cells.ContainsKey("D83")||summaryCells["F4"].Element(s+"f")!.Value!="'Muratura'!E58")throw new Exception("Summary isolation/references");
        double Width(XDocument doc,int col)=>double.Parse(doc.Descendants(s+"col").Single(c=>(int)c.Attribute("min")! == col).Attribute("width")!.Value,CultureInfo.InvariantCulture);
        if(Width(sheet,7)!=3||Width(summary,7)<=3)throw new Exception("Summary changed masonry column spacing");
        if(Width(sheet,4)!=Width(sheet,8)||Width(sheet,5)!=Width(sheet,9)||Width(sheet,6)!=Width(sheet,10))throw new Exception("Masonry cards differ in width");
        if(sheet.Descendants(s+"row").Any(r=>(int)r.Attribute("r")!>=83))throw new Exception("Summary still below masonry");
        string xml=sheet.ToString()+summary.ToString();
        if(xml.Contains("#REF!")||xml.Contains("BONFIGLIOLI")||xml.Contains("CARICO SUPERFICIALE")||zip.Entries.Any(e=>e.FullName.Contains("externalLink")))throw new Exception("Unrelated source data in Excel");
        foreach(var formula in sheet.Descendants(s+"f").Concat(summary.Descendants(s+"f")))if(formula.Value.Contains('[')||formula.Value.Contains("_xlfn"))throw new Exception("External or unsupported formula");
        if(!sheet.Descendants(s+"dataValidation").Any(v=>v.Element(s+"formula1")?.Value=="\"235,275,355\""))throw new Exception("Steel list missing");
        using var styles=zip.GetEntry("xl/styles.xml")!.Open();var styleXml=XDocument.Load(styles);
        int count=styleXml.Root!.Element(s+"cellXfs")!.Elements().Count();
        if(cells.Values.Any(c=>int.Parse(c.Attribute("s")?.Value??"0")>=count))throw new Exception("Missing style");
        Directory.CreateDirectory("artifacts/reports");File.WriteAllBytes($"artifacts/reports/test-{name}.xlsx",bytes);
        File.WriteAllText($"artifacts/reports/test-{name}.json",MasonryFiles.Write(project));
        Console.WriteLine("PASS Excel "+name+": results, pressure, disabled/multiple frames, styles and no external data");
    }
}
