using System.ComponentModel;
using System.Globalization;
using STA.Calcolo.Application;
using STA.Calcolo.Desktop.Infrastructure;
namespace STA.Calcolo.Desktop.ViewModels;

public sealed class ShearNumberField(ModuleField definition) : ObservableObject, IDataErrorInfo
{
    public string Key => definition.Key;
    public string Label => definition.Label;
    private string value = definition.Default;
    public string Value
    {
        get => value;
        set { if (Set(ref this.value, value)) { Changed(nameof(Error)); Changed(nameof(IsValid)); } }
    }
    public bool TryNumber(out double number) =>
        double.TryParse((Value ?? "").Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out number) && double.IsFinite(number);
    public bool IsValid => TryNumber(out _);
    public string Error => IsValid ? "" : "Scegli un numero finito; usa la virgola o il punto decimale.";
    public string this[string columnName] => columnName == nameof(Value) ? Error : "";
}
