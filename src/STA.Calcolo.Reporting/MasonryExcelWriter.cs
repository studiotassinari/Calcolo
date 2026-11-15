using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using STA.Materiali;
using STA.Muratura.Core;
using STA.Profilati.Core;

namespace STA.Calcolo.Reporting;

/// <summary>Excel report using the supplied workbook's cell styles and table geometry.</summary>
public static class MasonryExcelWriter
{
    private static readonly XNamespace S="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace R="http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private sealed record TemplateCell(int Row,int Col,int Style,string? Text,string? RichText);
    private sealed record Merge(int R1,int C1,int R2,int C2);
    private sealed record Block(TemplateCell[] Cells,Merge[] Merges,double[] Heights,double[] Widths);
    private static string Resource(string name)
    {
        using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("STA.Calcolo.Reporting.ExcelTemplate."+name)!;
        using var reader=new StreamReader(stream);return reader.ReadToEnd();
    }
    private static string Col(int c){string s="";for(;c>0;c=(c-1)/26)s=(char)('A'+(c-1)%26)+s;return s;}
    private static string N(double n)=>n.ToString("R",CultureInfo.InvariantCulture);
    public static byte[] Write(MasonryProject p)
    {
        MasonryFiles.Validate(p);
        var before=MasonryCalculator.Total(p.Before);var after=MasonryCalculator.Total(p.After);
        var frames=p.AllFrames.ToArray();var group=FrameGroupCalculator.Calculate(frames,before,after);
        var templates=JsonSerializer.Deserialize<Dictionary<string,Block>>(Resource("blocks.json"))!;
        var cells=new Dictionary<(int Row,int Col),XElement>();var heights=new Dictionary<int,double>();
        var widths=new Dictionary<int,double>();var merges=new List<string>();var validations=new List<XElement>();
        XElement Cell(int r,int c){if(!cells.TryGetValue((r,c),out var cell)){cell=new XElement(S+"c",new XAttribute("r",Col(c)+r));cells[(r,c)]=cell;}return cell;}
        void Style(int r,int c,int style)=>Cell(r,c).SetAttributeValue("s",style);
        void Text(int r,int c,string? text){var x=Cell(r,c);x.RemoveNodes();x.SetAttributeValue("t","inlineStr");x.Add(new XElement(S+"is",new XElement(S+"t",text??"")));}
        void TextFormula(int r,int c,string text,string formula){var x=Cell(r,c);x.RemoveNodes();x.SetAttributeValue("t","str");x.Add(new XElement(S+"f",formula),new XElement(S+"v",text));}
        void Num(int r,int c,double n,string? formula=null)
        {
            if(!double.IsFinite(n))throw new ArgumentException("Valore non finito nell’esportazione Excel.");
            var x=Cell(r,c);x.RemoveNodes();x.SetAttributeValue("t",null);
            if(formula!=null)x.Add(new XElement(S+"f",formula));x.Add(new XElement(S+"v",N(n)));
        }
        void Bool(int r,int c,bool b){var x=Cell(r,c);x.RemoveNodes();x.SetAttributeValue("t","b");x.Add(new XElement(S+"v",b?1:0));}
        void Copy(string name,int row,int col)
        {
            var b=templates[name];foreach(var x in b.Cells){Style(row+x.Row,col+x.Col,x.Style);if(x.Text!=null)Text(row+x.Row,col+x.Col,x.Text);if(x.RichText!=null){var cell=Cell(row+x.Row,col+x.Col);cell.RemoveNodes();cell.SetAttributeValue("t","inlineStr");cell.Add(XElement.Parse(x.RichText));}}
            foreach(var m in b.Merges)merges.Add($"{Col(col+m.C1)}{row+m.R1}:{Col(col+m.C2)}{row+m.R2}");
            for(int i=0;i<b.Heights.Length;i++)heights[row+i]=Math.Max(heights.GetValueOrDefault(row+i),b.Heights[i]);
            for(int i=0;i<b.Widths.Length;i++)widths[col+i]=b.Widths[i];
            widths[col+b.Widths.Length]=3;
        }
        void Row(int r,int c,string label,double value,string unit,string? formula=null)
        {Text(r,c,label);Style(r,c,42);Num(r,c+1,value,formula);Style(r,c+1,56);Text(r,c+2,unit);Style(r,c+2,42);heights[r]=18;}
        void List(int r,int c,string options)=>validations.Add(new XElement(S+"dataValidation",new XAttribute("type","list"),new XAttribute("allowBlank",0),new XAttribute("showErrorMessage",1),new XAttribute("errorTitle","Valore non valido"),new XAttribute("error","Scegli un valore dall’elenco."),new XAttribute("sqref",Col(c)+r),new XElement(S+"formula1","\""+options+"\"")));
        Text(3,4,p.Title);Style(3,4,157);merges.Add("D3:J3");heights[3]=24;
        int afterCol=4+4*p.Before.Length+2;
        var beforeCols=Enumerable.Range(0,p.Before.Length).Select(i=>4+i*4).ToArray();
        var afterCols=Enumerable.Range(0,p.After.Length).Select(i=>afterCol+i*4).ToArray();
        string Sum(int[] cols,int row)=>"SUM("+string.Join(",",cols.Select(c=>Col(c+1)+row))+")";
        string Min(int[] cols,int row)=>"MIN("+string.Join(",",cols.Select(c=>Col(c+1)+row))+")";
        void State(MasonryPier[] piers,StateResult result,int[] cols,string state)
        {
            Text(11,cols[0],state);Style(11,cols[0],157);heights[11]=24;
            for(int i=0;i<piers.Length;i++)
            {
                var a=piers[i];var resultRow=result.Rows[i];int c=cols[i];string v=Col(c+1),u=Col(c+2);string A(int r)=>v+r;
                Copy("Pier",16,c);
                foreach(var cell in cells.Where(x=>x.Key.Row>=16&&x.Key.Row<=71&&x.Key.Col>=c&&x.Key.Col<=c+2).Select(x=>x.Value))foreach(var text in cell.Descendants(S+"t"))
                {
                    string index=state=="STATO PROGETTO"?"1":"0";
                    text.Value=text.Value.Replace("MASCHIO 1",$"MASCHIO {i+1}").Replace("ATTUALE",state=="STATO PROGETTO"?"PROGETTO":"ATTUALE")
                        .Replace("0.1",$"{index}.{i+1}").Replace("0,TOT",index+",TOT").Replace("0.TOT",index+".TOT").Replace("0,min",index+",min").Replace("0.min",index+".min");
                }
                Num(17,c+1,a.H);Num(18,c+1,a.H,A(17));Num(19,c+1,a.L);Num(20,c+1,a.T);Num(21,c+1,a.L*a.T,$"{A(19)}*{A(20)}");
                Row(79,c,"FC",MasonryCatalog.Confidence(a.Lc),"",$"IF({u}28=\"LC1\",1.35,IF({u}28=\"LC2\",1.2,1))");
                Num(24,c+1,a.H/a.L,$"{A(17)}/{A(19)}");TextFormula(24,c+2,a.H/a.L>1.5?"SNELLO>1.5":"TOZZO",$"IF({A(24)}>1.5,\"SNELLO>1.5\",\"TOZZO\")");
                Text(27,c+1,a.Type);Text(28,c+2,a.Lc);List(28,c+2,"LC1,LC2,LC3");
                Num(29,c+1,a.FmBase);Num(30,c+1,a.TauBase);Num(31,c+1,a.E);Num(32,c+1,a.G);Num(33,c+1,a.Rho!.Value);
                for(int n=0;n<7;n++)Num(35+n,c+2,a.C[n]);
                double cr=a.C.Aggregate(1d,(x,y)=>x*y),ce=new[]{0,1,4,5,6}.Aggregate(1d,(x,n)=>x*a.C[n]);double cracked=a.Cracked?.5:1;
                Num(43,c+1,a.FmBase/MasonryCatalog.Confidence(a.Lc)*cr,$"{A(29)}/{A(79)}*PRODUCT({u}35:{u}41)");
                Num(44,c+1,a.TauBase/MasonryCatalog.Confidence(a.Lc)*cr,$"{A(30)}/{A(79)}*PRODUCT({u}35:{u}41)");
                Bool(45,c+2,a.Cracked);List(45,c+2,"TRUE,FALSE");
                Num(46,c+1,a.E*ce*cracked,$"{A(31)}*{u}35*{u}36*{u}39*{u}40*{u}41*IF({u}45,0.5,1)");
                Num(47,c+1,a.G*ce*cracked,$"{A(32)}*{u}35*{u}36*{u}39*{u}40*{u}41*IF({u}45,0.5,1)");
                Num(50,c+1,resultRow.N,$"IF({A(73)}=\"Distribuito q\",{A(74)}*{A(19)}/100,{A(75)})");
                Num(51,c+1,resultRow.Sigma/10,$"{A(50)}*100/{A(21)}");
                Num(54,c+1,resultRow.B,$"MIN(1.5,MAX(1,{A(17)}/{A(19)}))");
                Num(55,c+1,resultRow.Vt,$"{A(21)}*1.5*{A(44)}/{A(54)}*SQRT(1+{A(51)}*10/(1.5*{A(44)}))/1000");
                Num(61,c+1,resultRow.Dy,$"{A(55)}*10000/{A(68)}");Text(62,c,"du");Num(62,c+1,resultRow.Du,$"0.004*MIN(2,MAX(1,{A(17)}/{A(19)}-0.5))*{A(17)}*10");
                Num(68,c+1,resultRow.K,$"{A(47)}*100*{A(21)}/(1.2*{A(17)})/(1+{A(47)}/{A(46)}/1.2*({A(17)}/{A(19)})^2)");
                if(i==0){Num(58,c+1,result.Vt,Sum(cols,55));Num(65,c+1,result.Du,Min(cols,62));Num(71,c+1,result.K,Sum(cols,68));}
                else foreach(int r in new[]{57,58,64,65,70,71})
                {
                    for(int k=0;k<3;k++)cells.Remove((r,c+k));
                    merges.RemoveAll(m=>m==$"{Col(c)}{r}:{Col(c+2)}{r}");
                }
                Text(73,c,"Modalità carico");Style(73,c,42);Text(73,c+1,a.LoadMode=="linear"?"Distribuito q":"Risultante N");Style(73,c+1,51);List(73,c+1,"Risultante N,Distribuito q");
                Row(74,c,"q",a.Q,"kN/m");Style(74,c+1,43);Row(75,c,"N inserito",a.N,"kN");Style(75,c+1,43);
                Text(77,c,"Materiale ID");Text(77,c+1,a.MaterialId);Text(78,c,"Versione materiale");Num(78,c+1,a.MaterialVersion);
            }
        }
        State(p.Before,before,beforeCols,"STATO ATTUALE");State(p.After,after,afterCols,"STATO PROGETTO");
        int frameStart=afterCols.Last()+5;var frameCols=Enumerable.Range(0,frames.Length).Select(i=>frameStart+4*i).ToArray();
        var usedProfiles=frames.Select(f=>ProfileCatalog.All.Single(x=>x.Id==f.Profile)).DistinctBy(x=>x.Id).ToArray();
        string profileRange=$"'Riepilogo'!$D$37:$F${36+usedProfiles.Length}";
        for(int i=0;i<frames.Length;i++)
        {
            int c=frameCols[i];var f=frames[i];var profile=ProfileCatalog.All.Single(x=>x.Id==f.Profile);string v=Col(c+1);string A(int r)=>v+r;
            var raw=FrameCalculator.Calculate(f with{Enabled=true},before,after)!;
            Copy("Frame",31,c);Text(31,c,$"CERCHIATURA {i+1}");Text(32,c,"Inclusa nel confronto");Bool(32,c+1,f.Enabled);Text(32,c+2,"");
            Num(33,c+1,f.Fy);List(33,c+1,"235,275,355");Num(34,c+1,f.Gamma);Num(35,c+1,f.H);
            // Remove the original independent design/check branches: proposals are not project results.
            foreach(int r in new[]{36,39,46,53,54,55})for(int k=0;k<3;k++)Text(r,c+k,"");
            Text(38,c+1,f.Profile);Num(40,c+1,f.E);Num(41,c+1,profile.Jx,$"VLOOKUP({A(38)},{profileRange},2,FALSE)");Num(42,c+1,profile.Wx,$"VLOOKUP({A(38)},{profileRange},3,FALSE)");Num(43,c+1,f.NProfiles);
            validations.Add(new XElement(S+"dataValidation",new XAttribute("type","list"),new XAttribute("showErrorMessage",1),new XAttribute("sqref",A(38)),new XElement(S+"formula1","ProfiliUsati")));
            Num(44,c+1,raw.J,$"{A(41)}*{A(43)}");Num(45,c+1,raw.W,$"{A(42)}*{A(43)}");Num(47,c+1,f.NColumns);
            // CS49:CT49 is merged in the reference. Its anchor remains the portal selector.
            Text(49,c+1,f.Portal=="closed"?"Chiuso":"Aperto");List(49,c+1,"Chiuso,Aperto");
            Num(50,c+1,raw.V,$"2*{A(47)}*{A(33)}*10*{A(45)}/({A(34)}*{A(35)})/100");
            Num(51,c+1,raw.K,$"{A(43)}*{A(47)}*IF({A(49)}=\"Chiuso\",12,3)*{A(40)}*10*{A(41)}/{A(35)}^3");
            Num(52,c+1,raw.Dy,$"{A(50)}*10000/{A(51)}");
            Num(56,c+1,f.Enabled?raw.Veff:0,$"IF({A(32)},MIN({A(50)}*{Col(afterCols[0]+1)}65/{A(52)},{A(50)}),0)");
            Row(58,c,"K incluso nel confronto",f.Enabled?raw.K:0,"N/cm",$"IF({A(32)},{A(51)},0)");
            Text(60,c,"Catalogo profili");Text(60,c+1,f.CatalogVersion);
        }
        // Each sheet owns its geometry: the summary must never resize a masonry card.
        var masonryCells=cells;var masonryHeights=heights;var masonryWidths=widths;
        var masonryMerges=merges;var masonryValidations=validations;
        cells=new();heights=new();widths=new();merges=new();validations=new();
        string SheetSum(int[] cols,int row)=>"SUM("+string.Join(",",cols.Select(c=>"'Muratura'!"+Col(c+1)+row))+")";
        Copy("Summary",83,4);string B(int r)=>"'Muratura'!E"+r;string D(int r)=>"'Muratura'!"+Col(afterCols[0]+1)+r;
        Num(84,6,before.Vt,B(58));Num(85,6,after.Vt,D(58));Num(86,6,after.Vt-before.Vt,"F85-F84");Num(87,6,after.Vt/before.Vt-1,"F86/F84");
        Num(88,6,before.K,B(71));Num(89,6,after.K,D(71));Num(90,6,after.K-before.K,"F89-F88");Num(91,6,after.K/before.K-1,"F90/F88");
        TextFormula(87,7,after.Vt<before.Vt?"DIMINUZIONE RESISTENZA":"RESISTENZA NON DIMINUITA","IF(F87<0,\"DIMINUZIONE RESISTENZA\",\"RESISTENZA NON DIMINUITA\")");
        TextFormula(91,7,after.K<before.K?"DIMINUZIONE RIGIDEZZA":"RIGIDEZZA NON DIMINUITA","IF(F91<0,\"DIMINUZIONE RIGIDEZZA\",\"RIGIDEZZA NON DIMINUITA\")");
        Text(92,4,"Confronto preliminare del modulo: altezza efficace = altezza geometrica.");
        Text(93,4,"CONTRIBUTO DELLE CERCHIATURE ATTIVE");
        Num(94,6,frames.Select((f,i)=>f.Enabled?FrameCalculator.Calculate(f,before,after)!.V:0).Sum(),"SUM("+string.Join(",",frameCols.Select(c=>$"IF('Muratura'!{Col(c+1)}32,'Muratura'!{Col(c+1)}50,0)"))+")");
        Num(95,6,group.Veff,SheetSum(frameCols,56));Num(96,6,group.K,SheetSum(frameCols,58));
        Num(97,6,group.FinalV,"F85+F95");Num(98,6,group.FinalV/before.Vt-1,"(F97-F84)/F84");
        TextFormula(99,6,group.FinalV>=before.Vt?"Non diminuita":"Diminuita","IF(F98>=0,\"Non diminuita\",\"Diminuita\")");
        Num(100,6,group.FinalK,"F89+F96");Num(101,6,group.FinalK/before.K-1,"(F100-F88)/F88");
        TextFormula(102,6,FrameCalculator.StiffnessAccepts((group.FinalK/before.K-1)*100)?"Entro ±15%":"Oltre ±15%","IF(ABS(F101)<=0.150000000000002,\"Entro ±15%\",\"Oltre ±15%\")");
        int percentStyle=int.Parse(Cell(91,6).Attribute("s")!.Value);foreach(int r in new[]{87,98,101})Style(r,6,percentStyle);
        Row(104,4,"Larghezza prima",p.Before.Sum(x=>x.L),"cm",SheetSum(beforeCols,19));
        Row(105,4,"Larghezza dopo",p.After.Sum(x=>x.L),"cm",SheetSum(afterCols,19));
        Row(106,4,"Riduzione larghezza",p.Before.Sum(x=>x.L)-p.After.Sum(x=>x.L),"cm","E104-E105");
        Row(107,4,"Incremento K cerchiature",group.K/after.K,"%","F96/F89");Style(107,5,Cell(91,6).Attribute("s") is XAttribute st?int.Parse(st.Value):58);
        Row(108,4,"Incremento V cerchiature",group.Veff/after.Vt,"%","F95/F85");Style(108,5,int.Parse(Cell(107,5).Attribute("s")!.Value));
        Text(110,4,"Altezza uguale prima");Bool(110,5,p.EqualHeights?.GetValueOrDefault("before")??false);Text(111,4,"Altezza uguale dopo");Bool(111,5,p.EqualHeights?.GetValueOrDefault("after")??false);
        Text(115,4,"PROFILI UTILIZZATI NEL PROGETTO");Style(115,4,157);merges.Add("D115:F115");
        Text(116,4,"Profilo");Text(116,5,"Jx [cm⁴]");Text(116,6,"Wx [cm³]");
        for(int i=0;i<usedProfiles.Length;i++){Text(117+i,4,usedProfiles[i].Id);Num(117+i,5,usedProfiles[i].Jx);Num(117+i,6,usedProfiles[i].Wx);for(int c=4;c<=6;c++)Style(117+i,c,42);}
        XDocument MakeSheet(Dictionary<(int Row,int Col),XElement> cells,Dictionary<int,double> heights,
            Dictionary<int,double> widths,List<string> merges,List<XElement> validations)
        {
        var sheetData=new XElement(S+"sheetData");
        foreach(var row in cells.OrderBy(x=>x.Key.Row).ThenBy(x=>x.Key.Col).GroupBy(x=>x.Key.Row))
            sheetData.Add(new XElement(S+"row",new XAttribute("r",row.Key),new XAttribute("ht",N(heights.GetValueOrDefault(row.Key,18))),new XAttribute("customHeight",1),row.Select(x=>x.Value)));
        return new XDocument(new XElement(S+"worksheet",new XElement(S+"sheetViews",new XElement(S+"sheetView",new XAttribute("workbookViewId",0),new XAttribute("showGridLines",0),new XAttribute("zoomScale",70))),new XElement(S+"sheetFormatPr",new XAttribute("defaultRowHeight",18)),new XElement(S+"cols",widths.OrderBy(x=>x.Key).Select(x=>new XElement(S+"col",new XAttribute("min",x.Key),new XAttribute("max",x.Key),new XAttribute("width",N(x.Value)),new XAttribute("customWidth",1)))),sheetData,new XElement(S+"mergeCells",merges.Select(m=>new XElement(S+"mergeCell",new XAttribute("ref",m)))),new XElement(S+"dataValidations",new XAttribute("count",validations.Count),validations),new XElement(S+"pageMargins",new XAttribute("left",0.25),new XAttribute("right",0.25),new XAttribute("top",0.3),new XAttribute("bottom",0.3),new XAttribute("header",0.15),new XAttribute("footer",0.15)),new XElement(S+"pageSetup",new XAttribute("orientation","landscape"),new XAttribute("paperSize",9),new XAttribute("scale",70))));
        }
        // Place the former row 83 at row 3; only summary-local references move.
        string Move(string value)=>Regex.Replace(value,@"(\$?[A-Z]+\$?)(\d+)",m=>
            int.Parse(m.Groups[2].Value)>=83?m.Groups[1].Value+(int.Parse(m.Groups[2].Value)-80):m.Value);
        foreach(var cell in cells.Values)
        {
            cell.SetAttributeValue("r",Move(cell.Attribute("r")!.Value));
            if(cell.Element(S+"f") is XElement formula)formula.Value=Move(formula.Value);
        }
        var summary=MakeSheet(cells.ToDictionary(x=>(x.Key.Row-80,x.Key.Col),x=>x.Value),
            heights.ToDictionary(x=>x.Key-80,x=>x.Value),widths,merges.Select(Move).ToList(),validations);
        var sheet=MakeSheet(masonryCells,masonryHeights,masonryWidths,masonryMerges,masonryValidations);
        using var memory=new MemoryStream();using(var zip=new ZipArchive(memory,ZipArchiveMode.Create,true))
        {
            void Put(string path,string text){using var writer=new StreamWriter(zip.CreateEntry(path).Open(),new UTF8Encoding(false));writer.Write(text);}
            Put("[Content_Types].xml","<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/><Override PartName=\"/xl/worksheets/sheet2.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/><Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/><Override PartName=\"/xl/theme/theme1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.theme+xml\"/></Types>");
            Put("_rels/.rels","<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
            Put("xl/workbook.xml",new XDocument(new XElement(S+"workbook",new XAttribute(XNamespace.Xmlns+"r",R),new XElement(S+"sheets",new XElement(S+"sheet",new XAttribute("name","Muratura"),new XAttribute("sheetId",1),new XAttribute(R+"id","rId1")),new XElement(S+"sheet",new XAttribute("name","Riepilogo"),new XAttribute("sheetId",2),new XAttribute(R+"id","rId4"))),new XElement(S+"definedNames",new XElement(S+"definedName",new XAttribute("name","ProfiliUsati"),$"'Riepilogo'!$D$37:$D${36+usedProfiles.Length}")),new XElement(S+"calcPr",new XAttribute("calcId",191029),new XAttribute("fullCalcOnLoad",1)))).ToString());
            Put("xl/_rels/workbook.xml.rels","<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/><Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/><Relationship Id=\"rId4\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet2.xml\"/><Relationship Id=\"rId3\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/theme\" Target=\"theme/theme1.xml\"/></Relationships>");
            Put("xl/styles.xml",Resource("styles.xml"));Put("xl/theme/theme1.xml",Resource("theme.xml"));Put("xl/worksheets/sheet1.xml",sheet.ToString());Put("xl/worksheets/sheet2.xml",summary.ToString());
        }
        return memory.ToArray();
    }
}
