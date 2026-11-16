# Taglio di sezioni rettangolari in c.a.

Aprire **Taglio · sezioni in c.a.** dalla schermata iniziale di TA-Calcolo.
Il modulo usa il tema aziendale condiviso (Acherus, blu #192845, verde #135F6E, logo TA) e conserva il progetto quando si chiude e riapre la finestra nella stessa sessione.

## Uso

1. Scegliere Trave o Parete e il modello NTC 2018 / EN 1992 del programma originale.
2. Inserire geometria, azioni e armature. Tutte le quote sono in mm, le aree in mm², le resistenze in MPa e le forze in kN. N è positivo a compressione; VEd è il valore assoluto del taglio di progetto.
3. Le caselle gialle **H** e **b** sul disegno sono editabili anche da tastiera e sincronizzate con i campi del pannello. Il disegno è vettoriale e usa la stessa scala sui due assi. La linea Asl è uno schema dell'armatura tesa, non un dettaglio esecutivo.
4. Premere **Calcola**. Qualunque modifica invalida subito esiti ed esportazioni. **Applica θ proposto** applica la proposta del modello storico e ricalcola tutte le resistenze allo stesso angolo.
5. Aprire il tab Risultati per i valori intermedi; salvare il progetto JSON oppure esportare risultati e relazione Word.

Per una parete, H è la **lunghezza resistente in pianta**, b è lo **spessore**. Non rappresentano l'altezza di piano. Il parametro Cover indica la distanza al baricentro dell'armatura longitudinale tesa; non è il copriferro netto. La figura non modifica parametri del modello non disponibili nell'originale (altezza di piano, vincoli, snellezza).

## Compatibilità

**Apri JSON / .tgl** importa i file del vecchio TaglioClsRect. Gli indici dei diametri sono convertiti nei valori effettivi; virgola e punto decimale sono accettati senza separatori delle migliaia. fck e fyk sono letti dai valori numerici del file, senza farli sovrascrivere dagli eventi di una ComboBox. Il titolo può contenere il carattere =.
I file incompleti, gli indici errati, le chiavi duplicate e le selezioni contraddittorie vengono rifiutati prima di modificare il progetto aperto.
Il salvataggio nuovo è il ModuleDocument JSON versione 1, module = taglio-cls; Data contiene il titolo.
Esempi: examples/taglio-parete.json e examples/taglio-parete.tgl.
Il modulo è disponibile anche dalla CLI: `modulo examples/taglio-parete.json risultati.json`.

## Migrazione e limiti del modello

Fonte: [studiotassinari/TaglioClsRect](https://github.com/studiotassinari/TaglioClsRect/tree/a90d8c939d46e9e8f95de8d25df162489f8a66d8), ClsTaglio.cs e MainWindow.xaml.cs.
Questa modifica integra il programma, **non costituisce una revisione o certificazione normativa**.

Sono conservati dal sorgente:

- VRd,c con σcp = N/(b·d), limitata a 0,2 fcd, e ρl ≤ 0,02.
- VRd,s come somma di staffe e piegati.
- Riduzione delle bielle NTC 0,5; variante EN originale 0,6 fino a fck = 60 MPa, poi max(0,5; 0,9 − fck/200).
- Coefficiente parete 0,40 sulle bielle; ricerca θ tra 22,5° e 45°. La proposta NTC per pareti usa 45°, come la finestra originale.
- Il cambio di modello propone αcc = 0,85 oppure 1,00; il valore resta esplicito e modificabile. L'apertura di un file conserva quello salvato.

Correzioni di gestione rispetto alla finestra originale:

- Resistenze ricalcolate allo **stesso θ**, senza dipendere dai valori lasciati da una ricerca precedente.
- Riduzione parete applicata una sola volta, coerentemente anche nella proposta EN (la vecchia finestra ignorava il coefficiente in quel ramo).
- Nessuna divisione 0/0 per armature disabilitate; nessuna esportazione di Infinity o NaN. Capacità nulla significa confronto non soddisfatto, con utilizzo non definito.
- Input finiti e dominio controllato; armature con numero intero, θ 22,5–45°, α 45–90°, d > 0. Trazione N < 0 non supportata.
- VRd corrente = min(VRd,s; VRd,max) con armatura; VRd,c quando bracci e piegati sono entrambi assenti. Il risultato confronta solo la resistenza a taglio, senza dichiarare una verifica completa del muro.
- Il vecchio testo "massimo taglio sollecitante" diviso per 1,5 non è usato come resistenza di progetto: VEd deve già includere le maggiorazioni richieste dal progetto.

Restano da valutare nel progetto strutturale dettagli e minimi delle armature, flessione, duttilità, sollecitazioni sismiche e applicabilità delle formule e dei coefficienti al caso concreto.
La proposta θ conserva la logica storica di bilanciamento e non è presentata come ricerca globale del massimo per ogni inclinazione delle armature.

## Architettura e verifica

- STA.Taglio.Core: input/risultati immutabili, validazione e funzioni numeriche pure, senza WPF, MathNet o Word COM.
- ShearWorkspace / ShearFiles: adattatore comune desktop/CLI e importazione.
- ShearViewModel: stato, comandi, invalidazione e geometria di presentazione; IFileService e IShearReportService iniettati.
- ShearWindow / ShearSectionView: binding XAML; code-behind limitato a InitializeComponent.
- ShearReportBuilder: documento Word attraverso il generatore aziendale, senza Microsoft Office installato.
- Test: 288 confronti con il sorgente storico fissato al commit indicato, controlli indipendenti a 45°, casi limite, importazione/round trip. Smoke WPF con modifica effettiva delle TextBox sul disegno, sincronizzazione inversa, focus durante cancellazione, errori di binding, invalidazione ed esportazione.
- Gli screenshot taglio-*.png sono nell'artefatto GitHub Actions WPF-smoke.
