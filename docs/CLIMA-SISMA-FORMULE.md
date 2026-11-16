# Neve, vento, spettri e calcestruzzo

## Neve
Selezionare una provincia nell'elenco NTC 2018 oppure “Zona manuale”, inserire la quota effettiva del sito e l'inclinazione della falda. La provincia determina la zona al posto del selettore manuale; la quota non è dedotta dal capoluogo. Le denominazioni e i confini di riferimento sono quelli del DM 17 gennaio 2018: per modifiche amministrative successive scegliere la zona manuale dopo verifica del sito.

Schema di singola falda con angolo reale e frecce verticali sulla proiezione orizzontale; grafico qsk/quota con punto del sito. Sono implementati TR 50 anni, Ct = 1 e quote 0–1500 m. “Ostacolato” impone μ1 ≥ 0,8; non calcola accumuli locali. Coperture multiple, accumuli, situazioni speciali e quote superiori richiedono un'analisi specifica.

## Vento
Il nuovo progetto usa NTC 2018. La velocità base è vb = vb0 · ca, con ca = 1 sotto a0 e ca = 1 + ks · (as/a0 − 1) sopra a0. Sono implementati TR = 50 anni, quote del sito 0–1500 m e altezze z fino a 200 m.

La categoria I–V è **assegnata dal progettista** secondo Fig. 3.3.2: non è dedotta dalla sola rugosità A–D. La vecchia fascia costiera è ignorata in modalità NTC 2018. Il testo mostra descrizione della zona, della rugosità e categoria effettivamente utilizzata. Il grafico confronta tutte le categorie a parità di ct, evidenziando quella scelta.

ce(z) = kr² · ct · ln(z/z0) · [7 + ct · ln(z/z0)], con z sostituito da zmin sotto zmin.
p(z) = qr · ce(z) · cp · cd, qr = 0,625 vb² in N/m².
cp ha segno: positivo pressione, negativo depressione. cp = cd = 1 sono ipotesi iniziali, non un riconoscimento automatico dell'edificio. Il profilo non sostituisce l'assegnazione delle pressioni sulle diverse facce. Azioni tangenti, distacco dei vortici e aeroelasticità non sono implementati. ct è limitato dal modulo a 1–5; valori superiori richiedono analisi specifica.

I vecchi JSON con i cinque campi originali vengono aperti in “Storico NTC 2008”, conservando velocità e categoria precedenti. La modifica a NTC 2018 è esplicita. Esempio Zona 3 a 1000 m: 37 m/s storico, 36,99 m/s NTC 2018.

## Spettri sismici
q iniziale = 1,5. Il valore salvato in un vecchio progetto non viene sovrascritto.
La scheda “Sito sulla mappa” usa OpenStreetMap: clic posiziona il marker, trascinamento sposta la mappa o il marker, rotella e pulsanti +/− regolano lo zoom, Home centra il marker. La coppia WGS84 longitudine/latitudine viene trasferita ai campi. Anche l'inserimento manuale riposiziona il marker. Nessuna conversione delle coordinate del motore sismico in Web Mercator.

La cartografia richiede Internet; se non disponibile restano attivi i campi manuali. Il servizio richiede solo le tessere visibili, con User-Agent identificativo e cache locale minima di sette giorni. Non sono presenti download offline o prefetch. Attribuzione: © OpenStreetMap contributors, https://www.openstreetmap.org/copyright. La cartografia non certifica la copertura del reticolo sismico storico; restano i limiti di quest'ultimo (e l'intervallo di input lon 6–19°, lat 36–48°).

“Tutti gli spettri TXT (ZIP)” salva 9 file numerici: SLO, SLD, SLV, SLC elastici H/V e SLV di progetto H, più istruzioni e progetto JSON. Il motore storico non definisce altri spettri di progetto. Il verticale rimane elastico e non è etichettato come progetto. Tutti i 40 campioni nativi per serie sono inclusi, indipendentemente da selezione e periodo massimo visualizzato. Colonne TAB, punto decimale, T [s], Sa [m/s²], intestazioni commentate con #. q modifica solo il progetto orizzontale.

## Calcestruzzo
Le schede dei risultati mostrano formula, sostituzione e risultato in MPa. È mantenuta ed esplicitata la conversione storica fck = 0,83 Rck, diversa dalla resistenza cilindrica nominale della sigla Cxx/yy. Sono conservati αcc = 0,85 e γc = 1,5.
Correzioni documentate:
- Rck per C80/95, C90/105 e C100/115: 95, 105 e 115 MPa (prima tutte erroneamente 85).
- esponente di fctm per classi fino a C50/60: 2/3 esatto al posto di 0,66666666.
Le classi storiche elevate rimangono nel catalogo: non se ne certifica l'ammissibilità normativa. Per esse va verificato l'ambito di impiego.

## Verifiche
Suite portabile: associazioni geografiche, formule indipendenti, plateau zmin, segno del vento, compatibilità JSON, proiezione geografica, 9 serie TXT e invarianza degli elastici rispetto a q, tutte le classi calcestruzzo.
Smoke WPF: finestre reali, assenza di errori binding, tema, caricamento atomico, invalidazione, marker bidirezionale, zoom, esportazione ZIP e layout compatto. Il servizio mappa è simulato nei test per non generare richieste automatizzate ai server OSM.

## Fonti
- DM 17 gennaio 2018, capitolo 3, §§3.3–3.4, tabelle 3.3.I–II e zonazione provinciale §3.4.2, consultato il 16/11/2026:
https://www.gazzettaufficiale.it/do/atto/serie_generale/caricaPdf?art.codiceRedazionale=18A00716&art.dataPubblicazioneGazzetta=2018-02-20&art.num=1&art.tiposerie=SG&cdimg=18A0071600100010110004&dgu=2018-02-20
- Politica tessere OSM: https://operations.osmfoundation.org/policies/tiles/
- Motori sismico e materiale del repository: formule storiche, non aggiornamento normativo complessivo.
