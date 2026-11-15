using STA.Calcolo.Desktop.Infrastructure;
using STA.Profilati.Core;
namespace STA.Calcolo.Desktop.ViewModels;

public sealed class SteelProfileViewModel : ObservableObject
{
    private readonly ModuleFieldViewModel selection;
    public IReadOnlyList<SteelProfile> Profiles=>ProfileCatalog.All;
    public SteelProfile? Model=>Profiles.SingleOrDefault(p=>p.Id==SelectedId);
    public string SelectedId {get=>selection.Value;set=>selection.Value=Profiles.FirstOrDefault(p=>Normalize(p.Id)==Normalize(value))?.Id??value;}
    private static string Normalize(string? value)=>string.Concat((value??"").Where(c=>!char.IsWhiteSpace(c))).ToUpperInvariant();
    public SteelProfileViewModel(ModuleFieldViewModel selection)
    {
        this.selection=selection;
        selection.PropertyChanged+=(_,_)=>{Changed(nameof(SelectedId));Changed(nameof(Model));};
    }
}
