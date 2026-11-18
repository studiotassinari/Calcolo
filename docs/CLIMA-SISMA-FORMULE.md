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
Il modulo ha una finestra MVVM dedicata (`SeismicSpectraViewModel`, `SeismicSpectraWindow`). Ogni modifica dei dati ricalcola subito tabella e grafico; con un dato non valido i risultati sono azzerati e le esportazioni disabilitate.

**Sito.** Il pulsante **Scelta geografica…** apre una finestra con la mappa OpenStreetMap e la **ricerca per indirizzo**. Con la conferma la finestra restituisce latitudine e longitudine WGS84, più il nome del luogo. Le coordinate si possono anche scrivere a mano.
- La ricerca usa Nominatim (OpenStreetMap), solo in Italia. Parte soltanto premendo **Cerca indirizzo** o Invio, con al massimo una richiesta al secondo e un User-Agent identificativo, come prevede la politica d'uso del servizio.
- La conferma è disabilitata fuori dal reticolo sismico (lon 6–19°, lat 36–48°).

**Dati e formule.** I parametri ag, F0 e Tc* vengono dal reticolo e dall'interpolazione del programma storico. Le forme spettrali sono calcolate secondo NTC 2018 §3.2.3 (`SeismicSpectra`, in `STA.Calcolo.Application`):
- orizzontale (eq. 3.2.2) con S = Ss·ST, dove ST è il fattore topografico di Tab. 3.2.V in sommità (T1 = 1,0; T2 = T3 = 1,2; T4 = 1,4), e smorzamento η = √(10/(5+ξ)) ≥ 0,55;
- verticale (eq. 3.2.10) con Ss = 1, TB = 0,05 s, TC = 0,15 s, TD = 1,0 s e Fv = 1,35·F0·(ag/g)^0,5. **Corregge** il motore storico, che usava i periodi e il fattore S dell'orizzontale;
- progetto SLV con η = 1/q e limite inferiore Sd ≥ 0,2·ag (non presente nel motore storico).

Con ST = 1 e ξ = 5 % l'orizzontale coincide con il motore storico (test di parità).

**Unità.** Le accelerazioni sono sempre indicate in frazione di g (ag/g, amax/g, Se/g), con il valore in m/s² accanto (g = 9,81 m/s²). La tabella riporta, per SLO, SLD, SLV e SLC: TR, ag/g, ag, F0, Tc*, S, amax/g, amax, TB, TC e TD.

**Grafico.** Ha l'asse principale in Se/g e l'asse secondario in m/s². Passando il mouse, una linea verticale segue il cursore e un riquadro riporta il periodo T e il valore di ogni curva, in g e in m/s². Si possono mostrare o nascondere le curve orizzontali, verticali e di progetto, ed evidenziare uno stato limite.

**Richiamo da altri moduli.** `IDialogService.PickSeismic` apre gli spettri come finestra di selezione. **Usa i valori dello stato limite scelto** restituisce un `SeismicSiteSelection` con sito, ag/g, F0, Tc*, S e amax/g.
- Il Muro di sostegno lo usa con **Parametri sismici dagli spettri…**: il campo PGA riceve amax/g = S·ag/g dello stato limite scelto (predefinito SLV).
- Il sito scelto viene riproposto alla riapertura, ma non è salvato nel progetto del muro.

**File.** I progetti restano compatibili con i JSON precedenti del modulo "sisma". I campi nuovi (topografia, smorzamento, stato evidenziato, luogo) hanno valori predefiniti se mancano. L'esportazione ZIP contiene un file TXT per ciascuna delle 9 curve, con T [s], Se/g e Se [m/s²], più il progetto e le istruzioni.

La cartografia richiede Internet; se non è disponibile restano attivi i campi manuali. Le tessere sono richieste solo per l'area visibile, con cache locale di sette giorni. Attribuzione: © OpenStreetMap contributors, https://www.openstreetmap.org/copyright. La mappa non certifica la copertura del reticolo sismico storico.

## Calcestruzzo — aggiornamento NTC 2018
Il modulo usa un motore dedicato alle proprietà progettuali del calcestruzzo di peso normale a 28 giorni, secondo §§11.2.10.1–3 e 4.1.2.1.1. Ogni risultato mostra formula, sostituzione, valore in MPa e riferimento normativo, conservati anche nell'esportazione JSON.

La conversione fck = 0,83 Rck è **confermata dalla formula [11.2.1] NTC 2018**: non è il valore cilindrico nominale della sigla Cxx/yy. Sono mantenuti αcc = 0,85 e γc = 1,5; non si applica automaticamente la riduzione speciale di γc a 1,4.
Sono aggiunti i frattili 5% e 95% della resistenza a trazione e fctd. Per elementi piani gettati in opera di spessore <50 mm l'apposita scelta riduce fcd e fctd del 20%.

Con aggregati grossi di riciclo la stima di fctm è ridotta del 10% e quella di Ecm del 20%. La selezione presuppone che origine, percentuali d'impiego e qualificazione della miscela rispettino la Tab. 11.2.III: questo modulo non verifica tali condizioni né la composizione. Un Ecm da prove sostituisce la stima senza ulteriori riduzioni; è obbligatorio per maturazione a vapore. Nessuna formula di maturazione nel tempo, ritiro o viscosità è implementata.

Correzioni rispetto al catalogo originale:
- inserita C30/37, mantenendo gli identificatori numerici delle classi esistenti;
- Rck per C80/95, C90/105 e C100/115 nel motore storico: 95, 105 e 115 MPa (prima tutte erroneamente 85);
- esponente 2/3 esatto e soglia basata sulla resistenza, indipendente dall'ordine delle enumerazioni;
- C100/115 resta riconoscibile nel motore storico, ma è esclusa dal modulo NTC 2018; un vecchio file con questa classe viene rifiutato senza sovrascrivere il progetto aperto.

C28/35 e C32/40 sono conservate come consentito dal §4.1. Oltre C45/55 è richiesta sperimentazione preventiva; oltre C70/85 il §4.1 rinvia alla qualificazione del §11.1, caso C). Il calcolo di una proprietà non costituisce verifica dell'ammissibilità del materiale per una specifica struttura.

I vecchi JSON con il solo campo classe acquisiscono le ipotesi originarie esplicite: niente riciclo, niente vapore, Ecm teorico ed elemento ordinario. Contratti parziali o sconosciuti sono rifiutati.

### EN 206, UNI 11104 ed Eurocodici
Al 16/11/2026 il catalogo UNI indica UNI EN 206-1:2026 e UNI EN 206-2:2026 (09/07/2026) come sostitutive della UNI EN 206:2021. Le specificazioni italiane sono UNI 11104:2025, con errata corrige 2026. I cataloghi sono stati consultati; i testi integrali delle nuove norme **non erano disponibili per questo aggiornamento**.

Non sono quindi introdotte tabelle ricostruite di durabilità, rapporti a/c, dosaggi minimi, classi di esposizione o criteri di conformità e certificazione. I riferimenti di edizione non equivalgono a una dichiarazione di conformità a EN 206. Il profilo numerico implementato rimane NTC 2018; le edizioni più recenti degli Eurocodici non sono applicate automaticamente in sostituzione delle NTC.

Il motore sismico resta storico e **non è dichiarato aggiornato alle NTC 2018**. La revisione deve includere unità e datum del reticolo, interpolazione temporale, parametri del verticale e limite inferiore dello spettro di progetto. La mappa è un ausilio di selezione, non una validazione di questi aspetti. Il modulo taglio e gli altri moduli non sono ricertificati da questo aggiornamento.

## Verifiche
Suite portabile: associazioni geografiche, formule indipendenti, plateau zmin, segno del vento, compatibilità JSON, proiezione geografica, 9 serie TXT e invarianza degli elastici rispetto a q, tutte le classi calcestruzzo.
Smoke WPF: finestre reali, assenza di errori binding, tema, caricamento atomico, invalidazione, marker bidirezionale, zoom, esportazione ZIP e layout compatto. Il servizio mappa è simulato nei test per non generare richieste automatizzate ai server OSM.

## Fonti
- DM 17 gennaio 2018, capitolo 3, §§3.3–3.4, tabelle 3.3.I–II e zonazione provinciale §3.4.2, consultato il 16/11/2026:
https://www.gazzettaufficiale.it/do/atto/serie_generale/caricaPdf?art.codiceRedazionale=18A00716&art.dataPubblicazioneGazzetta=2018-02-20&art.num=1&art.tiposerie=SG&cdimg=18A0071600100010110004&dgu=2018-02-20
- Politica tessere OSM: https://operations.osmfoundation.org/policies/tiles/
- NTC 2018, capitolo 11, §§11.2.10.1–3:
https://www.gazzettaufficiale.it/do/atto/serie_generale/caricaPdf?art.codiceRedazionale=18A00716&art.dataPubblicazioneGazzetta=2018-02-20&art.num=1&art.tiposerie=SG&cdimg=18A0071600100010110012&dgu=2018-02-20
- NTC 2018, capitolo 4, §4.1 e §§4.1.2.1.1.1–2:
https://www.gazzettaufficiale.it/do/atto/serie_generale/caricaPdf?art.codiceRedazionale=18A00716&art.dataPubblicazioneGazzetta=2018-02-20&art.num=1&art.tiposerie=SG&cdimg=18A0071600100010110005&dgu=2018-02-20
- Cataloghi UNI: https://store.uni.com/uni-en-206-1-2026 ; https://store.uni.com/uni-en-206-2-2026 ; https://store.uni.com/uni-11104-2025 ; https://store.uni.com/ec-2-2026-uni-11104-2025
- Motore sismico del repository: formule storiche, revisione normativa ancora da completare.
