TA-Calcolo 1.7.0: Muro di sostegno con forze e diagrammi nel disegno, pressione sul terreno di fondazione e relazione di calcolo.

- **Relazione di calcolo Word e PDF** (13 pagine A4): inquadramento normativo (NTC 2018, Circolare 2019) e coefficienti parziali, dati, 19 formule numerate con sostituzione numerica, cinque figure generate dal disegno (sezione e schema di calcolo, azioni statiche, azioni rispetto al polo di ribaltamento, diagrammi delle pressioni, azioni sismiche) e riepilogo delle verifiche.
- **Formule native di Word:** frazioni, radici, apici e pedici sono equazioni di Office modificabili; nel PDF le righe di frazione sono proporzionate al contenuto (anche nelle relazioni di Muratura).
- **Forze e diagrammi nel disegno:** frecce proporzionali alle azioni della combinazione scelta (esercizio, EQU, A1, sisma), diagrammi della spinta attiva, dell'acqua, della resistenza passiva e della pressione sul terreno.
- **Pressione sul terreno di fondazione:** distribuzione lineare con base parzializzata oltre il nocciolo centrale, in esercizio, allo SLU (A1) e in sisma; risultati nella sintesi e nella tabella.
- **Grip H1:** abbassando la sommità sotto il terreno, le altezze del terreno a monte e a valle scendono insieme al muro.

## Verifiche
64 test C# del core: pressione sul terreno in forma chiusa (trapezio, nocciolo, base parzializzata, risultante fuori base), coerenza fra forze, diagrammi e verifiche, grip. Relazioni su tre casi con controllo dell'impaginazione A4, delle figure e delle equazioni native di Word. Test WPF su relazione, figure, combinazione disegnata e livelli.

## Ambito e limiti
La capacità portante non è verificata: le pressioni sul terreno vanno confrontate con la resistenza del terreno di fondazione. Esclusi stabilità globale, cedimenti e armature. Restano valide le ipotesi del motore rivisto descritte in docs/MURO-SOSTEGNO.md.

## Installazione e aggiornamento
Installer Windows x64 con runtime incluso, pacchetto per l'aggiornamento automatico e file SHA256SUMS. I progetti JSON e .mdst esistenti restano compatibili.
