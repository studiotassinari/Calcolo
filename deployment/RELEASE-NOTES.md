TA-Calcolo 1.5.0: neve geografica, vento MVVM, mappa sismica e proprietà del calcestruzzo NTC 2018.

- Neve: scelta della provincia secondo la zonazione NTC 2018, figura della falda e grafico qsk/quota.
- Vento: velocità e profili NTC 2018, pressione/depressione e confronto delle categorie di esposizione I–V assegnate dal progettista.
- Spettri: marker OpenStreetMap bidirezionale, q iniziale 1,5 ed esportazione ZIP dei 9 spettri TXT disponibili nel motore storico.
- Calcestruzzo: formule teoriche, sostituzioni, risultati e riferimenti NTC 2018; C30/37, frattili a trazione e fctd, riduzioni per riciclo ed elementi sottili, Ecm da prove obbligatorio per maturazione a vapore.
- Interfacce MVVM con grafica aziendale, compatibilità dei vecchi progetti e invalidazione dei risultati dopo le modifiche.

## Ambito e limiti
Il motore sismico rimane storico: la revisione di datum/unità del reticolo, interpolazione, spettro verticale e limite inferiore di progetto non è completata. La mappa non ne costituisce validazione normativa.
Le nuove edizioni UNI EN 206-1:2026, UNI EN 206-2:2026 e UNI 11104:2025 sono censite, ma i testi integrali non sono stati disponibili: non è dichiarata conformità completa EN 206 e non sono automatizzate le prescrizioni di durabilità.
Neve: singola falda, TR 50 anni, Ct=1, quota fino a 1500 m; esclusi accumuli e casi speciali. Vento: TR 50 anni, quota fino a 1500 m, altezza fino a 200 m, categoria manuale. Il riciclo del calcestruzzo presuppone miscela qualificata e limiti della Tab. 11.2.III verificati; C100/115 esclusa dal modulo NTC.
Dettagli: docs/CLIMA-SISMA-FORMULE.md. Gli altri motori storici, incluso Taglio, non sono aggiornati normativamente da questa versione.

Test numerici Windows/Linux, compatibilità JSON e smoke WPF superati; la mappa è simulata nei test. Il processo di rilascio ripete build, test, installazione e rimozione prima della pubblicazione.

Scaricare **TA-Calcolo-Setup-1.5.0-win-x64.exe** oppure usare Verifica aggiornamenti e riavviare l'app. Il file STA-Calcolo-Update è riservato all'aggiornatore.
