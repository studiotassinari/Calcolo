using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using STA.Calcolo.Application;
using STA.Calcolo.Desktop;
using STA.Calcolo.Desktop.Views;
using STA.Calcolo.Desktop.ViewModels;
using STA.Calcolo.Desktop.Services;
internal static class Program
{
    static void CheckSaveDirectories()
    {
        string projectDirectory=System.IO.Path.GetFullPath("artifacts/save-project");
        string excelDirectory=System.IO.Path.GetFullPath("artifacts/save-excel");
        System.IO.Directory.CreateDirectory(projectDirectory);System.IO.Directory.CreateDirectory(excelDirectory);
        string? selected=null;string initial="";
        var service=new FileService(dialog=>{initial=dialog.InitialDirectory;if(selected==null)return false;dialog.FileName=selected;return true;});
        const string project="Progetto muratura|*.json",excel="Foglio Excel|*.xlsx";
        selected=System.IO.Path.Combine(projectDirectory,"p.json");service.Save(project,"p.json");
        selected=System.IO.Path.Combine(excelDirectory,"p.xlsx");service.Save(excel,"p.xlsx");
        selected=null;
        if(service.Save(project,"p.json")!=null||initial!=projectDirectory)throw new Exception("Project save directory not remembered");
        service.Save(excel,"p.xlsx");if(initial!=excelDirectory)throw new Exception("Excel save directory not independent");
        service.Save(project,"p.json");if(initial!=projectDirectory)throw new Exception("Cancel changed save directory");
        var fresh=new FileService(dialog=>{if(dialog.InitialDirectory!=Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments))throw new Exception("Save directory leaked to a new session");return false;});
        fresh.Save(project,"p.json");
        Console.WriteLine("PASS save folders: independent project/Excel, cancellation and session lifetime");
    }
    [STAThread]
    static int Main()
    {
        CheckSaveDirectories();
        var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown }; app.Resources.MergedDictionaries.Add(new ResourceDictionary {Source=new Uri("/STA.Calcolo.Desktop;component/Themes/Tassinari.xaml",UriKind.Relative)}); var files = new MemoryFileService(); var windows = new WindowService(files); var mainVm = new MainViewModel(windows); var main = new MainWindow { DataContext = mainVm }; app.MainWindow = main;
        foreach (var module in WorkspaceCatalog.Create())
        {
            var vm = new WorkspaceViewModel(module, files); vm.CalculateCommand.Execute(null);
            if (vm.Results.Count == 0) throw new Exception(module.Id + ": " + vm.Status);
            files.NextSave = module.Id + ".json"; vm.SaveCommand.Execute(null); var saved = files.Content[files.NextSave];
            files.NextSave = module.Id + "-results.json"; vm.ExportCommand.Execute(null); if (!files.Content[files.NextSave].Contains("results")) throw new Exception("Export failed");
            var original = vm.Fields[0].Value; vm.Fields[0].Value = "changed"; files.NextOpen = module.Id + ".json"; vm.OpenCommand.Execute(null); if (vm.Fields[0].Value != original || vm.Results.Count != 0) throw new Exception("Round trip failed");
            vm.CalculateCommand.Execute(null);
            var window = new WorkspaceWindow { DataContext = vm }; Render(window, "module-" + module.Id); window.Close();
            vm.Fields[0].Value += " invalid"; if (vm.Results.Count != 0 || vm.ExportCommand.CanExecute(null)) throw new Exception("Stale results: " + module.Id);
            Console.WriteLine("PASS WPF " + module.Id);
        }
        ShearSmoke.Run(Render);
        // Verifica il nuovo modulo integrato e i suoi formati di progetto.
        RetainingWallSmoke.Run(Render);
        SeismicSpectraSmoke.Run(Render);
        // Verifica le nuove interfacce senza richieste cartografiche di rete.
        ScientificSmoke.Run(Render);
        var masonry=new MasonryViewModel(files);masonry.ExampleCommand.Execute(null);masonry.CalculateCommand.Execute(null);
        if(!new MasonryViewModel(files).Frame.ByKey["Fy"].Choices.SequenceEqual(new[]{"235","275","355"}))throw new Exception("Steel list");
        if(new MasonryViewModel(files).Frame.ByKey["Fy"].Value!="275")throw new Exception("Steel default");
        files.NextSave="masonry.xlsx";masonry.ExcelReportCommand.Execute(null);
        if(!files.Binary.ContainsKey(files.NextSave))throw new Exception(masonry.Status);
        masonry.Before[0].Editor.ByKey["N"].Value="175";files.NextSave="masonry-updated.xlsx";masonry.ExcelReportCommand.Execute(null);
        if(!files.Binary.ContainsKey(files.NextSave)||files.Binary[files.NextSave].SequenceEqual(files.Binary["masonry.xlsx"]))throw new Exception("Stale Excel data");
        masonry.ExampleCommand.Execute(null);masonry.CalculateCommand.Execute(null);
        if(masonry.Results.Count==0)throw new Exception(masonry.Status);
        files.NextSave="masonry.json";masonry.SaveCommand.Execute(null);var masonrySaved=files.Content[files.NextSave];
        files.NextSave="masonry-report.pdf";masonry.ReportCommand.Execute(null);if(!files.Binary.ContainsKey(files.NextSave))throw new Exception(masonry.Status);
        files.NextSave="masonry-report.docx";masonry.WordReportCommand.Execute(null);if(!files.Binary.ContainsKey(files.NextSave))throw new Exception(masonry.Status);
        System.IO.Directory.CreateDirectory("artifacts/reports");System.IO.File.WriteAllBytes("artifacts/reports/relazione-esempio.pdf",files.Binary["masonry-report.pdf"]);System.IO.File.WriteAllBytes("artifacts/reports/relazione-esempio.docx",files.Binary["masonry-report.docx"]);
        using(var package=new System.IO.Compression.ZipArchive(new System.IO.MemoryStream(files.Binary["masonry-report.docx"])))if(package.GetEntry("word/document.xml")==null)throw new Exception("DOCX invalid");
        if(masonry.Comparison.Count!=2||masonry.BeforeResults.Count()!=2||masonry.AfterResults.Count()!=3)throw new Exception("Grouped results missing");
        masonry.Before[0].Editor.Fields.Single(f=>f.Key=="H").Value="bad";if(masonry.Results.Count>0||masonry.ReportCommand.CanExecute(null))throw new Exception("Stale masonry results");
        masonry.CalculateCommand.Execute(null);if(masonry.Results.Count>0)throw new Exception("Bad masonry geometry accepted");
        files.NextOpen="masonry.json";masonry.OpenCommand.Execute(null);if(STA.Muratura.Core.MasonryFiles.Write(masonry.Build())!=masonrySaved)throw new Exception("Masonry round trip");
        masonry.DesignCommand.Execute(null);if(masonry.Proposals.Count!=3)throw new Exception(masonry.Status);
        var mw=new MasonryWindow{DataContext=masonry};Render(mw,"muratura");((FrameworkElement)mw.FindName("BeforeSection")).BringIntoView();Render(mw,"muratura-prima");((FrameworkElement)mw.FindName("FrameSection")).BringIntoView();Render(mw,"muratura-cerchiatura");((FrameworkElement)mw.FindName("ResultsSection")).BringIntoView();Render(mw,"muratura-risultati");mw.Close();
        masonry.SelectedProposal=masonry.Proposals.First(p=>p.Candidate!=null);var chosen=masonry.SelectedProposal.Candidate!.Profile.Id;masonry.UseProfileCommand.Execute(null);if(masonry.Build().Frame.Profile!=chosen||masonry.Results.Count==0)throw new Exception("Apply profile failed");
        masonry.CopyCommand.Execute(null);if(masonry.Before.Count!=masonry.After.Count)throw new Exception("Copy state");
        masonry.MaterialId="tenera-irregolare";masonry.ApplyMaterialCommand.Execute(null);masonry.CalculateCommand.Execute(null);if(masonry.Results.Count>0)throw new Exception("Missing density accepted");
        masonry.MaterialId="mattoni-calce";masonry.ApplyMaterialCommand.Execute(null);masonry.CalculateCommand.Execute(null);if(masonry.Results.Count==0)throw new Exception(masonry.Status);
        masonry.BeforeCount=4;masonry.EqualBefore=true;masonry.Before[0].Editor.ByKey["H"].Value="380";
        if(masonry.Before.Any(p=>p.Editor.ByKey["H"].Value!="380")||masonry.Before[1].HeightEditable)throw new Exception("Linked heights failed");
        var linked=STA.Muratura.Core.MasonryFiles.Read(STA.Muratura.Core.MasonryFiles.Write(masonry.Build()));masonry.Load(linked);
        if(!masonry.EqualBefore||masonry.BeforeCount!=4)throw new Exception("Height options not persisted");
        masonry.EqualBefore=false;masonry.Before[1].Editor.ByKey["H"].Value="410";
        if(masonry.Before[0].Editor.ByKey["H"].Value!="380"||!masonry.Before[1].HeightEditable)throw new Exception("Independent heights failed");
        masonry.Before[0].MaterialId="pietrame";if(masonry.Before[0].Build().FmBase!=100)throw new Exception("Automatic material selection failed");
        masonry.Before[0].Level="LC2";if(masonry.Before[0].Build().FmBase!=150)throw new Exception("LC selection failed");
        masonry.Before[0].Material.Editor.ByKey["E"].Value="999";if(masonry.Before[0].MaterialId!="custom")throw new Exception("Custom material label failed");
        masonry.Before[0].Editor.ByKey["H"].Value="invalid";if(masonry.Summary.StiffnessDelta!="—")throw new Exception("Stale live summary");
        var preview=new MasonryWindow{DataContext=new MasonryViewModel(files)};Render(preview,"TA-Calcolo-cerchiature");var section=(FrameworkElement)preview.FindName("BeforeSection");var cards=Find<System.Windows.Controls.ItemsControl>(section);Capture(cards!,"TA-Calcolo-maschi");preview.Close();
        var multiVm=new MasonryViewModel(files);multiVm.ExampleCommand.Execute(null);
        var originalFrame=multiVm.Frame;multiVm.AddFrameCommand.Execute(null);
        if(multiVm.Frames.Count!=2||ReferenceEquals(originalFrame,multiVm.Frame))throw new Exception("Add frame failed");
        multiVm.Frame.Profile.SelectedId="IPE 200";multiVm.Frame.ByKey["H"].Value="240";
        var multiInput=multiVm.Build();
        if(multiInput.AdditionalFrames[0].Profile!="IPE 200"||multiInput.Frame.Profile!=originalFrame.Build().Profile)throw new Exception("Frames not independent");
        multiVm.Load(STA.Muratura.Core.MasonryFiles.Read(STA.Muratura.Core.MasonryFiles.Write(multiInput)));
        multiVm.Frame=multiVm.Frames[1];multiVm.CalculateCommand.Execute(null);
        if(multiVm.Results.Count==0)throw new Exception(multiVm.Status);
        var multiWindow=new MasonryWindow{DataContext=multiVm};Render(multiWindow,"muratura-multipla");((FrameworkElement)multiWindow.FindName("FrameSection")).BringIntoView();Render(multiWindow,"cerchiature-multiple");multiWindow.Close();
        multiVm.DesignCommand.Execute(null);if(multiVm.Proposals.Count!=3)throw new Exception("Multiple frame design failed");
        multiVm.Frames[0].ByKey["H"].Value="260";
        if(multiVm.Proposals.Count!=0||multiVm.Results.Count!=0)throw new Exception("Other frame change left stale proposals");
        multiVm.RemoveFrameCommand.Execute(null);
        if(multiVm.Frames.Count!=1||multiVm.RemoveFrameCommand.CanExecute(null))throw new Exception("Remove frame failed");
        multiVm.CalculateCommand.Execute(null);if(multiVm.Results.Count==0)throw new Exception(multiVm.Status);
        Console.WriteLine("PASS WPF muratura: original JSON, invalidation, linked heights, automatic material selection, live summary, report, design and rendering");

        var pressureVm=new MasonryPierViewModel(new(){L=850,T=20,N=170},"Pressione",_=>{});
        if(pressureVm.PressureText!=$"{1d:N2}")throw new Exception("Pressure kN to daN/cm2");
        pressureVm.Editor.ByKey["LoadMode"].Value="Distribuito q";pressureVm.Editor.ByKey["Q"].Value="20";
        if(pressureVm.PressureText!=$"{1d:N2}")throw new Exception("Uniform load pressure");
        pressureVm.Editor.ByKey["T"].Value="0";
        if(pressureVm.PressureText!="—")throw new Exception("Invalid pressure geometry");
        pressureVm.Editor.ByKey["T"].Value="20";pressureVm.Editor.ByKey["Q"].Value="0";
        if(pressureVm.PressureText!=$"{0d:N2}")throw new Exception("Zero pressure");
        var compactVm=new MasonryViewModel(files);compactVm.ExampleCommand.Execute(null);
        var compactProject=compactVm.Build();
        compactProject.Before=[new(){L=400}];compactProject.After=[new(){L=300}];
        compactProject.Frame.Enabled=false;compactProject.AdditionalFrames=[];
        compactVm.Load(compactProject);
        if(compactVm.Summary.WidthReduction!=$"Riduzione {100:N1} cm"||compactVm.Summary.HasFrames)throw new Exception("Width summary or disabled frame summary");
        compactVm.Before[0].Editor.ByKey["L"].Value="250";
        if(compactVm.Summary.WidthReduction!=$"Aumento {50:N1} cm")throw new Exception("Live width summary");
        compactVm.Frame.Enabled=true;compactVm.AddFrameCommand.Execute(null);
        var cp=compactVm.Build();var ca=STA.Muratura.Core.MasonryCalculator.Total(cp.Before);var cb=STA.Muratura.Core.MasonryCalculator.Total(cp.After);
        var cf=STA.Muratura.Core.FrameGroupCalculator.Calculate(cp.AllFrames,ca,cb);
        var expectedIncrease=$"K +{cf.K/cb.K*100:N2} % · V +{cf.Veff/cb.Vt*100:N2} %";
        if(!compactVm.Summary.HasFrames||compactVm.Summary.FrameIncrease!=expectedIncrease)throw new Exception("Combined frame percentage");
        var compactWindow=new MasonryWindow{DataContext=compactVm};Render(compactWindow,"muratura-compatta",1760);
        var page=(System.Windows.Controls.ScrollViewer)compactWindow.FindName("PageScroll");
        foreach(var scrollName in new[]{"BeforeScroll","AfterScroll"})
        {
            var nested=(System.Windows.Controls.ScrollViewer)compactWindow.FindName(scrollName);
            page.ScrollToTop();compactWindow.UpdateLayout();
            nested.RaiseEvent(new System.Windows.Input.MouseWheelEventArgs(System.Windows.Input.Mouse.PrimaryDevice,0,-120){RoutedEvent=System.Windows.Input.Mouse.PreviewMouseWheelEvent});
            compactWindow.UpdateLayout();
            if(SystemParameters.WheelScrollLines!=0&&page.VerticalOffset<=0)throw new Exception("Wheel trapped in "+scrollName);
        }
        var framePanel=(FrameworkElement)compactWindow.FindName("FrameSection");
        var comparisons=(FrameworkElement)compactWindow.FindName("ComparisonCards");
        if(Math.Abs(framePanel.ActualWidth-794)>1||comparisons.ActualWidth>1174)throw new Exception("Compact section widths");
        framePanel.BringIntoView();Render(compactWindow,"cerchiatura-compatta",1760);
        var profileView=Find<SteelProfileView>(framePanel)!;
        var profileBox=(System.Windows.Controls.ComboBox)profileView.FindName("ProfileSelector");
        profileBox.ApplyTemplate();
        if(!profileBox.IsEditable||profileBox.Template.FindName("PART_EditableTextBox",profileBox)==null)throw new Exception("Profile editor not editable");
        profileBox.Text="IPE 200";profileBox.GetBindingExpression(System.Windows.Controls.ComboBox.TextProperty)!.UpdateSource();
        if(compactVm.Frame.Profile.Model?.Id!="IPE 200")throw new Exception("Typed profile not applied");
        profileBox.Text="INESISTENTE";profileBox.GetBindingExpression(System.Windows.Controls.ComboBox.TextProperty)!.UpdateSource();
        compactVm.CalculateCommand.Execute(null);
        if(compactVm.Results.Count!=0||compactVm.Summary.StiffnessDelta!="—")throw new Exception("Invalid typed profile accepted");
        compactVm.Frame.Profile.SelectedId="hea140";
        if(compactVm.Frame.Profile.Model?.Id!="HEA 140")throw new Exception("Profile normalization");
        compactVm.CalculateCommand.Execute(null);if(compactVm.Results.Count==0)throw new Exception(compactVm.Status);
        Render(compactWindow,"cerchiatura-compatta-stretto",920);
        if(framePanel.ActualWidth>794||comparisons.ActualWidth>1174)throw new Exception("Compact narrow layout");
        compactWindow.Close();

        var footing = new FootingViewModel(files); footing.ExampleCommand.Execute(null); footing.CalculateCommand.Execute(null);
        if (footing.Results.Length != 1 || !footing.ExportCommand.CanExecute(null)) throw new Exception(footing.Status);
        files.NextSave = "footing.json"; footing.SaveCommand.Execute(null); files.NextSave = "footing-results.json"; footing.ExportCommand.Execute(null);
        if (files.Content["footing-results.json"].Contains("ApiKey")) throw new Exception("Credential persisted");
        files.NextOpen = "footing.json"; footing.OpenCommand.Execute(null); footing.CalculateCommand.Execute(null);
        var beam = new BeamViewModel(files, windows); files.NextOpen = "sezione.json"; beam.ImportSectionCommand.Execute(null); files.NextOpen = "trefolatura.json"; beam.ImportTendonCommand.Execute(null);
        if (beam.Sections.Count != 1 || beam.Tendons.Count != 1) throw new Exception(beam.Status);
        files.NextSave = "beam.json"; beam.SaveCommand.Execute(null); beam.SelectedSection = beam.Sections[0]; beam.RemoveSectionCommand.Execute(null); if (beam.Sections.Count != 0) throw new Exception("Remove failed");
        files.NextOpen = "beam.json"; beam.OpenCommand.Execute(null); beam.CheckCommand.Execute(null); if (beam.Sections.Count != 1) throw new Exception(beam.Status);
        var bw = new BeamWindow { DataContext = beam }; Render(bw, "beam"); bw.Close();
        var fw = new FootingWindow { DataContext = footing }; Render(fw, "plinti"); fw.Close();
        footing.Parameters.Single(p => p.Key == "qRd").Value = "invalid"; if (footing.Results.Length != 0 || footing.ExportCommand.CanExecute(null)) throw new Exception("Stale footing results");
        footing.CalculateCommand.Execute(null); if (footing.Results.Length != 0) throw new Exception("Invalid footing result");
        Render(main, "desktop-smoke"); main.Show();
        ((System.Windows.Controls.ScrollViewer)main.FindName("HomeScroll")).ScrollToEnd();Render(main,"desktop-muro-sostegno");
        foreach (var card in mainVm.Modules) { card.OpenCommand.Execute(null); card.OpenCommand.Execute(null); }
        // Verifica che anche il muro conservi i dati alla riapertura.
        var wallWindow=main.OwnedWindows.Cast<Window>().Single(w=>w.DataContext is RetainingWallViewModel);
        var wallSession=(RetainingWallViewModel)wallWindow.DataContext;wallSession.Title="Sessione muro mantenuta";wallWindow.Close();
        mainVm.Modules.Single(m=>m.Title=="Muro di sostegno").OpenCommand.Execute(null);
        if(!main.OwnedWindows.Cast<Window>().Any(w=>ReferenceEquals(w.DataContext,wallSession)))throw new Exception("Riapertura muro: sessione persa");
        if (main.OwnedWindows.Count != mainVm.Modules.Count) throw new Exception("Navigation duplicated windows");
        var sample = main.OwnedWindows.Cast<Window>().First(w => w.DataContext is WorkspaceViewModel); var context = sample.DataContext; sample.Close(); mainVm.Modules.Single(m=>m.Title==((WorkspaceViewModel)context).Title).OpenCommand.Execute(null); if (!main.OwnedWindows.Cast<Window>().Any(w => ReferenceEquals(w.DataContext, context))) throw new Exception("Reopen lost project");
        foreach (Window child in main.OwnedWindows.Cast<Window>().ToArray()) child.Close();
        main.Close(); app.Shutdown(); Console.WriteLine("PASS MVVM: navigation, rendering, calculations, invalidation"); return 0;
    }
    static T? Find<T>(DependencyObject parent) where T:DependencyObject {for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){var child=VisualTreeHelper.GetChild(parent,i);if(child is T found)return found;var nested=Find<T>(child);if(nested!=null)return nested;}return null;}
    static void Capture(FrameworkElement content,string name){var bitmap=new RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth),(int)Math.Ceiling(content.ActualHeight),96,96,PixelFormats.Pbgra32);var visual=new DrawingVisual();using(var dc=visual.RenderOpen())dc.DrawRectangle(new VisualBrush(content),null,new Rect(0,0,content.ActualWidth,content.ActualHeight));bitmap.Render(visual);using var stream=System.IO.File.Create("artifacts/"+name+".png");var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));encoder.Save(stream);}
    static void Render(Window window, string name,double width=1180)
    {
        window.Width=width;window.Height=820;window.Show(); window.UpdateLayout();
        var content = (FrameworkElement)window.Content; content.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth), (int)Math.Ceiling(content.ActualHeight), 96, 96, PixelFormats.Pbgra32); var drawing=new DrawingVisual();using(var dc=drawing.RenderOpen())dc.DrawRectangle(new VisualBrush(content),null,new Rect(0,0,content.ActualWidth,content.ActualHeight));bitmap.Render(drawing); System.IO.Directory.CreateDirectory("artifacts"); using var file = System.IO.File.Create("artifacts/" + name + ".png"); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); encoder.Save(file);
    }
}
