using System.IO;
using System.Diagnostics;
using Microsoft.Win32;
namespace STA.Calcolo.Updates.Windows;
public static class InstalledApplication
{
    public const string AppId="{9FEC2950-470C-4A70-ADCD-EE58E640A4DD}";
    public const string MutexName=@"Local\STA.Calcolo.Desktop";
    public const string ExeName="STA.Calcolo.Desktop.exe";
    public const string UpdaterName="STA.Calcolo.Updater.exe";
    public static string Cache=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"StudioTassinari","Calcolo","Updates");
    private const string RegistryPath=@"Software\Microsoft\Windows\CurrentVersion\Uninstall\"+AppId+"_is1";
    public static bool IsInstalled(string root)
    {
        using var key=Registry.CurrentUser.OpenSubKey(RegistryPath);
        return key?.GetValue("InstallLocation") is string location && Path.GetFullPath(location).TrimEnd('\\').Equals(Path.GetFullPath(root).TrimEnd('\\'),StringComparison.OrdinalIgnoreCase)
            && File.Exists(Path.Combine(root,ExeName));
    }
    public static Version VersionAt(string root)=>Version.Parse(FileVersionInfo.GetVersionInfo(Path.Combine(root,ExeName)).FileVersion!);
    public static void UpdateVersion(string version)
    {
        using var key=Registry.CurrentUser.OpenSubKey(RegistryPath,true);
        key?.SetValue("DisplayVersion",version);
        key?.SetValue("DisplayName","TA-Calcolo");
        key?.SetValue("Publisher","Tassinari Associati");
    }
    /// <summary>
    /// Rinomina i collegamenti creati dalle installazioni precedenti alla 1.2.0, quando il programma si chiamava "STA Calcolo".
    /// Gli aggiornamenti automatici sostituiscono soltanto i file dell'applicazione e non rieseguono l'installatore, che è
    /// l'unico a rinominarli: senza questo passaggio menu Start e desktop conservano il vecchio nome.
    /// Sposta i file .lnk senza modificarli, quindi continuano a puntare alla stessa cartella di installazione.
    /// Non solleva eccezioni: un collegamento bloccato o protetto resta com'è e l'avvio prosegue.
    /// </summary>
    /// <param name="programs">Cartella Programmi del menu Start dell'utente</param>
    /// <param name="desktop">Cartella Desktop dell'utente</param>
    /// <returns>Numero di collegamenti rinominati o rimossi</returns>
    public static int RenameLegacyShortcuts(string programs,string desktop)
    {
        const string Old="STA Calcolo",New="TA-Calcolo";
        int changed=0;
        void Move(string source,string destination)
        {
            try
            {
                if(!File.Exists(source))return;
                // Se il collegamento nuovo esiste già (installatore rieseguito) quello vecchio è un doppione e va solo tolto.
                if(File.Exists(destination))File.Delete(source);
                else{Directory.CreateDirectory(Path.GetDirectoryName(destination)!);File.Move(source,destination);}
                changed++;
            }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){}
        }
        Move(Path.Combine(programs,Old,Old+".lnk"),Path.Combine(programs,New,New+".lnk"));
        Move(Path.Combine(programs,Old,"Ripristina "+Old+".lnk"),Path.Combine(programs,New,"Ripristina "+New+".lnk"));
        Move(Path.Combine(desktop,Old+".lnk"),Path.Combine(desktop,New+".lnk"));
        try{var folder=Path.Combine(programs,Old);if(Directory.Exists(folder)&&!Directory.EnumerateFileSystemEntries(folder).Any())Directory.Delete(folder);}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){}
        return changed;
    }
    /// <summary>Rinomina i collegamenti dell'utente corrente, solo per un'installazione registrata nella cartella indicata.</summary>
    public static int RenameLegacyShortcuts(string root)=>IsInstalled(root)
        ?RenameLegacyShortcuts(Environment.GetFolderPath(Environment.SpecialFolder.Programs),Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)):0;
    public static void Launch(string root)
    {
        var start=new ProcessStartInfo(Path.Combine(root,ExeName)){UseShellExecute=false,WorkingDirectory=root};
        start.Environment["STA_CALCOLO_SKIP_UPDATE_ONCE"]="1";Process.Start(start);
    }
}
