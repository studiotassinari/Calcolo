using Microsoft.Win32;
namespace STA.Calcolo.Desktop.Services;

public sealed class FileService : IFileService
{
    private readonly Dictionary<string, string> saveDirectories = new(StringComparer.Ordinal);
    private readonly Func<SaveFileDialog, bool?> showSave;
    public FileService(Func<SaveFileDialog, bool?>? showSave = null) => this.showSave = showSave ?? (dialog => dialog.ShowDialog());
    public string? Open(string filter) { var dialog = new OpenFileDialog { Filter = filter }; return dialog.ShowDialog() == true ? dialog.FileName : null; }
    public string? Save(string filter, string name)
    {
        var directory = saveDirectories.GetValueOrDefault(filter);
        if (!System.IO.Directory.Exists(directory)) directory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var dialog = new SaveFileDialog { Filter = filter, FileName = name, InitialDirectory = directory };
        if (showSave(dialog) != true) return null;
        saveDirectories[filter] = System.IO.Path.GetDirectoryName(dialog.FileName)!;
        return dialog.FileName;
    }
    public string Read(string path) => System.IO.File.ReadAllText(path);
    public void Write(string path, string text) => System.IO.File.WriteAllText(path, text);
    public void WriteBytes(string path,byte[] content)=>System.IO.File.WriteAllBytes(path,content);
}
