TA-Calcolo 1.10.1: il programma si presenta come TA-Calcolo anche sulle installazioni nate con il vecchio nome STA Calcolo.

- **Collegamenti rinominati:** sulle macchine installate quando il programma si chiamava STA Calcolo e poi aggiornate in automatico, il menu Start e il desktop mostravano ancora "STA Calcolo" e "Ripristina STA Calcolo". Al primo avvio i collegamenti vengono rinominati in "TA-Calcolo" e "Ripristina TA-Calcolo". L'aggiornamento automatico sostituisce solo i file dell'applicazione e non li aveva mai toccati.
- **Nome in Gestione attività e sulla barra delle applicazioni:** l'eseguibile si presenta come "TA-Calcolo" invece del nome tecnico "STA.Calcolo.Desktop".
- Nessuna variazione ai calcoli rispetto alla 1.10.0.

## Verifiche
Test WPF sul passaggio di rinomina con cartelle di prova: tre collegamenti, esecuzione ripetuta, collegamento nuovo già presente, file dell'utente nella vecchia cartella, cartelle assenti, installazione non registrata; controllo della descrizione dell'eseguibile. Restano superati i 79 test C# e le altre suite.

## Ambito e limiti
La cartella di installazione delle macchine nate con il vecchio nome resta `%LOCALAPPDATA%\Programs\STA Calcolo`: rinominarla interromperebbe gli aggiornamenti; per averla con il nuovo nome occorre disinstallare e reinstallare con il Setup. Un collegamento fissato a mano sulla barra delle applicazioni va tolto e rimesso. I nomi interni dei file (STA.Calcolo.Desktop.exe, pacchetto STA-Calcolo-Update) restano invariati per garantire la continuità degli aggiornamenti.

## Installazione e aggiornamento
Installer Windows x64 con runtime incluso, pacchetto per l'aggiornamento automatico e file SHA256SUMS. I progetti esistenti restano compatibili.
