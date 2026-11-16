using STA.Calcolo.Application;
namespace STA.Calcolo.Desktop.Services;
/// <summary>Contratto dell'export Word, sostituibile senza introdurre dipendenze WPF nel ViewModel.</summary>
public interface IShearReportService { byte[] Word(ModuleDocument document); }
