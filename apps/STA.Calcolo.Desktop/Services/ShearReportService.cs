using System.IO;
using STA.Calcolo.Application;
using STA.Calcolo.Reporting;
namespace STA.Calcolo.Desktop.Services;

public sealed class ShearReportService : IShearReportService
{
    public byte[] Word(ModuleDocument document)
    {
        using var stream = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/STA.Calcolo.Desktop;component/Assets/Brand/Logo.png"))!.Stream;
        using var memory = new MemoryStream(); stream.CopyTo(memory);
        var report = ShearReportBuilder.Build(new ShearWorkspace().Input(document), document.Data, DateTime.Now);
        return WordReportWriter.Write(report, memory.ToArray());
    }
}
