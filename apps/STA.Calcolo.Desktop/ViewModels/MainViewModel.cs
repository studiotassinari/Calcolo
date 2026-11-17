using STA.Calcolo.Application;
using STA.Calcolo.Desktop.Services;
namespace STA.Calcolo.Desktop.ViewModels;

public sealed class MainViewModel
{
    public UpdatesViewModel Updates {get;}=new();
    public IReadOnlyList<ModuleCardViewModel> Modules { get; }
    // Il nuovo modulo usa la stessa scheda grafica e segue i moduli già presenti nella release 1.5.0.
    public MainViewModel(IWindowService windows) { Modules = [new("muratura", "Muratura e cerchiatura", "Confronto prima/dopo, materiali condivisi e ricerca dei profili in acciaio.", windows), .. WorkspaceCatalog.Create().Select(m => new ModuleCardViewModel(m.Id, m.Title, m.Description, windows)), new("plinti", "Plinti · reazioni e MIDAS", "Progetti JSON, reazioni CSV e collegamento a MIDAS GEN NX.", windows), new("trave", "Travi prefabbricate", "Raccoglie sezioni e trefolature in un progetto interoperabile.", windows), new("muro-sostegno", "Muro di sostegno", "Geometria, spinte statiche e sismiche, ribaltamento e componenti di scorrimento.", windows)]; }
}
