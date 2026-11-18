TA-Calcolo 1.8.0: Spettri sismici riorganizzati, scelta geografica con ricerca per indirizzo e valori sismici richiamabili dal Muro di sostegno.

- **Nuova finestra Spettri sismici:** dati del sito, dell'opera e del terreno con ricalcolo a ogni modifica, tabella SLO–SLC (TR, ag/g, ag, F0, Tc*, S, amax/g, amax, TB, TC, TD) e valori dello stato limite scelto.
- **Accelerazioni sempre in frazione di g** (ag/g, amax/g, Se/g) con il valore in m/s² accanto.
- **Scelta geografica:** il pulsante apre la mappa con la ricerca per indirizzo (OpenStreetMap, Italia); la conferma restituisce latitudine, longitudine e nome del luogo. Le coordinate restano modificabili a mano.
- **Grafico con cursore:** passando il mouse si leggono il periodo T e il valore di ogni curva in g e in m/s²; asse secondario in m/s²; curve orizzontali, verticali e di progetto selezionabili.
- **Spettri NTC 2018 §3.2.3:** fattore topografico ST (T1–T4), smorzamento ξ, spettro verticale con i parametri NTC (corregge il programma storico), progetto SLV con limite Sd ≥ 0,2·ag. Con ST = 1 e ξ = 5 % l'orizzontale coincide con il programma storico.
- **Muro di sostegno:** "Parametri sismici dagli spettri…" apre gli spettri e importa amax/g = S·ag/g dello stato limite scelto nel campo PGA.
- Esportazione ZIP di tutte le curve con T, Se/g e Se in m/s².

## Verifiche
68 test C#: parità con il motore storico, formule NTC indipendenti (plateau, ST, η, verticale, limite del progetto), domini, ZIP e lettura della ricerca indirizzi. Test WPF su finestra, cursore, ricerca simulata, selezione e import nel muro.

## Ambito e limiti
ag, F0 e Tc* provengono dal reticolo e dall'interpolazione del programma storico (lon 6–19°, lat 36–48°). ST è il valore in sommità del rilievo. Mappa e ricerca richiedono Internet; senza connessione restano disponibili le coordinate manuali.

## Installazione e aggiornamento
Installer Windows x64 con runtime incluso, pacchetto per l'aggiornamento automatico e file SHA256SUMS. I progetti esistenti restano compatibili.
