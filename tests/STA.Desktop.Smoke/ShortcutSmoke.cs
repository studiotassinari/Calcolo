using System.Diagnostics;
using System.IO;
using STA.Calcolo.Updates.Windows;

/// <summary>
/// Nome visibile del programma: collegamenti delle vecchie installazioni "STA Calcolo" rinominati in "TA-Calcolo"
/// e descrizione del file eseguibile. Usa cartelle temporanee: non tocca il menu Start né il desktop reali.
/// </summary>
internal static class ShortcutSmoke
{
    private static void Check(bool ok, string message) { if (!ok) throw new Exception("Nome TA-Calcolo: " + message); }

    public static void Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "TA-Calcolo-ShortcutSmoke-" + Guid.NewGuid().ToString("N"));
        string programs = Path.Combine(root, "Programs"), desktop = Path.Combine(root, "Desktop");
        try
        {
            // Installazione nata con il vecchio nome: gruppo e tre collegamenti.
            Directory.CreateDirectory(Path.Combine(programs, "STA Calcolo")); Directory.CreateDirectory(desktop);
            File.WriteAllText(Path.Combine(programs, "STA Calcolo", "STA Calcolo.lnk"), "app");
            File.WriteAllText(Path.Combine(programs, "STA Calcolo", "Ripristina STA Calcolo.lnk"), "ripristino");
            File.WriteAllText(Path.Combine(desktop, "STA Calcolo.lnk"), "desktop");
            Check(InstalledApplication.RenameLegacyShortcuts(programs, desktop) == 3, "collegamenti non rinominati");
            Check(File.ReadAllText(Path.Combine(programs, "TA-Calcolo", "TA-Calcolo.lnk")) == "app" && File.ReadAllText(Path.Combine(programs, "TA-Calcolo", "Ripristina TA-Calcolo.lnk")) == "ripristino", "contenuto dei collegamenti alterato");
            Check(File.ReadAllText(Path.Combine(desktop, "TA-Calcolo.lnk")) == "desktop" && !Directory.Exists(Path.Combine(programs, "STA Calcolo")) && !File.Exists(Path.Combine(desktop, "STA Calcolo.lnk")), "vecchi nomi rimasti");
            // Seconda esecuzione: nulla da fare.
            Check(InstalledApplication.RenameLegacyShortcuts(programs, desktop) == 0, "operazione non ripetibile");

            // Collegamento nuovo già presente (installatore rieseguito): il vecchio è un doppione e viene tolto senza sovrascrivere il nuovo.
            Directory.CreateDirectory(Path.Combine(programs, "STA Calcolo"));
            File.WriteAllText(Path.Combine(programs, "STA Calcolo", "STA Calcolo.lnk"), "vecchio"); File.WriteAllText(Path.Combine(programs, "STA Calcolo", "altro.txt"), "dell'utente");
            Check(InstalledApplication.RenameLegacyShortcuts(programs, desktop) == 1 && File.ReadAllText(Path.Combine(programs, "TA-Calcolo", "TA-Calcolo.lnk")) == "app", "collegamento nuovo sovrascritto");
            // Una cartella con altri file dell'utente non viene cancellata.
            Check(File.Exists(Path.Combine(programs, "STA Calcolo", "altro.txt")), "file dell'utente rimosso");

            // Cartelle inesistenti e installazione non registrata: nessun errore e nessuna modifica.
            Check(InstalledApplication.RenameLegacyShortcuts(Path.Combine(root, "assente"), Path.Combine(root, "assente")) == 0, "cartelle assenti");
            Check(InstalledApplication.RenameLegacyShortcuts(root) == 0, "installazione non registrata");

            // Gestione attività e barra delle applicazioni mostrano la descrizione del file, non il nome tecnico dell'assembly.
            var info = FileVersionInfo.GetVersionInfo(Path.Combine(AppContext.BaseDirectory, InstalledApplication.ExeName));
            Check(info.FileDescription == "TA-Calcolo" && info.ProductName == "TA-Calcolo" && info.CompanyName == "Tassinari Associati", "descrizione del file: " + info.FileDescription);
            Console.WriteLine("PASS nome TA-Calcolo: collegamenti delle vecchie installazioni rinominati, doppioni, file dell'utente e descrizione dell'eseguibile");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
