# Plinti da reazioni vincolari

La finestra **Plinti** verifica plinti superficiali rettangolari con pilastro centrato. Le azioni sono le sestuple di reazione vincolare: si inseriscono a mano, si importano da CSV o si leggono da MIDAS GEN NX. Il calcolo si aggiorna a ogni modifica dei dati o delle reazioni.

## Interfaccia (MVVM)

- **Dati.** Parametri raggruppati in Geometria, Terreno e verifiche, Calcestruzzo e armature, Pesi. Ogni campo ha l'etichetta in italiano e l'unità; i numeri si scrivono con la virgola o con il punto. Combinazioni (SLU/SLE) e capacità portante si scelgono da elenco.
- **Reazioni.** Tabella modificabile, con pulsanti per aggiungere e togliere righe. Le azioni sono le reazioni del vincolo sulla struttura, in kN e kN·m; FZ positiva comprime il plinto.
- **Esiti.** Per ogni combinazione: N, σmax, area di contatto, Rd/Ed di capacità portante, scorrimento e punzonamento, ed esito verde o rosso. Selezionando una riga si vedono, a destra, tutte le verifiche con Ed, Rd, Rd/Ed e nota, più le avvertenze.
- **Disegno.**
  - Pianta: pilastro, nocciolo centrale, risultante (verde se nel nocciolo), base efficace B′×L′, zona non reagente, perimetro di controllo a 2d e pressioni ai vertici.
  - Sezione lungo X: armatura e diagramma delle pressioni sul terreno.
- **Trova base minima.** Cerca la base quadrata minima, a passi di 5 cm, che verifica tutte le combinazioni, e la applica ai campi B e L.
- **File e MIDAS.**
  - Il progetto resta nel contratto `FootingRequest` (schemaVersion 1). I parametri nuovi sono facoltativi, quindi i progetti precedenti si aprono senza modifiche.
  - L'esportazione JSON contiene i risultati del motore rivisto (`design`) e del prototipo (`prototype`).
  - Import CSV e lettura da MIDAS non sono cambiati; la MAPI-Key resta solo in memoria.

## Motore rivisto (`FootingDesignCalculator`)

Il motore del prototipo (`FootingCalculator`) resta invariato per la CLI e per la parità con il prototipo web, controllata da `tools/verify-reference.mjs`. L'interfaccia usa il motore rivisto (NTC 2018, EC2, EC7), che si distingue dal prototipo nei punti seguenti.

| Aspetto | Prototipo | Motore rivisto |
|---|---|---|
| Pressioni sul terreno | Navier; con qmin < 0 le verifiche CA si fermano | Piano delle tensioni non reagente a trazione, biassiale (Newton su griglia 120×120): pressioni e verifiche anche con base parzializzata |
| Capacità portante | qRd assegnata × B′L′ | qRd assegnata, **oppure** calcolata con EC7 Annex D drenata (Nq, Nc, Nγ, forma, inclinazione con m direzionale) su B′×L′, con γR = 2,3 (NTC Tab. 6.4.I) |
| Scorrimento | H ≤ μ·N | H ≤ μ·N(pesi favorevoli ×γG,fav) / γR, con γR = 1,1 |
| Pesi | plinto + altri pesi | plinto, rinterro sopra la soletta fino al piano di posa (escluso il pilastro), altri pesi; γG sfavorevole per portanza e CA, favorevole per lo scorrimento |
| Flessione | qmax uniforme su tutta la mensola, z = 0,9d | Pressione netta (contatto − peso proprio e rinterro) integrata sulla mensola, al filo del pilastro, sul lato più sollecitato; braccio dal blocco rettangolare 0,8x, con controllo x/d ≤ 0,45 |
| Taglio | qmax uniforme | Pressione netta oltre la distanza d dal filo; VRd,c EC2 6.2.2 |
| Punzonamento | Solo a 2d, VRd,c senza incremento | EC2 §6.4.4: perimetri a ∈ (0, 2d] a passi di d/10, VEd ridotto della pressione netta interna, vRd = vRd,c·2d/a; al filo pilastro vRd,max = 0,4·ν·fcd |

In esercizio (SLE) si verifica la pressione massima con terreno non reagente a trazione, confrontata con la q ammissibile assegnata. Cedimenti e fessurazione non sono calcolati.

**Limiti.**
- Pilastro centrato; torsione MZ segnalata e non verificata.
- Capacità portante solo drenata (φ′ ≥ 1°), senza fattori di profondità né falda, a favore di sicurezza.
- Armatura uguale nelle due direzioni; altezza utile media d = h − c − 1,5Ø.
- Esclusi cedimenti, fessurazione e dettagli costruttivi.

## Verifiche

`tests/STA.Calcolo.Tests/FootingDesignTests.cs` confronta il motore con soluzioni in forma chiusa:
- carico centrato: pressione uniforme, momento e taglio con pressione netta, punzonamento minimo sui perimetri, armatura dal blocco rettangolare;
- base parzializzata uniassiale: σmax = 2N/(3L(B/2−e)) e area reagente;
- caso biassiale nel nocciolo uguale a Navier, ribaltamento rifiutato;
- Nq, Nγ e qlim EC7 per φ′ = 30°, rinterro, inclinazione;
- scorrimento con γR, SLE, valori predefiniti dei progetti precedenti.

La parità con il prototipo resta verificata su 9 scenari. Lo smoke WPF controlla ricalcolo, reazioni modificabili, base parzializzata, base minima e disegno.
