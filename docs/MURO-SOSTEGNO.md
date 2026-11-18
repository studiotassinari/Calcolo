# Muro di sostegno

Modulo pubblicato in TA-Calcolo **1.5.1** a partire dal tag pubblicato **v1.5.0**, commit `fd0fa950e4877e7f6181666d1925aeddb4863626`. Dalla **1.6.0**: interfaccia compatta con grip, ricalcolo automatico e motore rivisto NTC 2018 predefinito.

## Utilizzo

La nuova scheda **Muro di sostegno** segue le schede esistenti nella schermata principale. La finestra riprende il tema di Muratura e cerchiatura: Acherus Grotesque, logo aziendale, blu e petrolio, pannelli bianchi e campi numerici gialli.

La finestra è su tre colonne ridimensionabili: a sinistra i dati, in righe compatte (etichetta, campo giallo, unità) raggruppate in sezioni richiudibili (Geometria, Materiali e carichi, Terreno e falda, Sisma); al centro il disegno con le schede Risultati e Note di calcolo; a destra una **Sintesi** sempre visibile (rapporti Mstab/Mrib, area, KA/KP, momenti e componenti di scorrimento).

**Calcolo automatico (MVVM).** Ogni modifica valida di un campo ricalcola subito geometria, risultati, sintesi e diagramma delle pressioni. Con un dato non valido i risultati vengono azzerati, l'errore compare nella Sintesi e le esportazioni sono disabilitate. Il pulsante **Ricalcola** riesegue il calcolo ma non è più necessario.

**Grip sul disegno.** I quadratini sul disegno si trascinano col mouse e scrivono i valori nei campi corrispondenti: H1, S1, Ds1 (sommità), Ba, S2 (base del paramento), Bf, H2, H3 (fondazione), H4, Xd, S3 (dente), Hta, It, Htp, Itp (terreni), Qfa, Qfp (falde). Il passo è di 5 cm, oppure di 1 cm tenendo premuto Ctrl; Esc annulla il trascinamento in corso. Durante il trascinamento la scala del disegno resta bloccata. Una posizione che produce una geometria non ammessa viene rifiutata e il valore resta invariato. Se il dente è allineato al bordo posteriore, trascinando Bf il dente si sposta con il bordo; se H2 = H3, trascinando H2 la base resta orizzontale. Le regole dei grip sono in `WallEditing` (core), indipendenti da WPF e coperte dai test.

- **Apri progetto** legge JSON e i file `.mdst` del programma originale.
- **Salva progetto** conserva parametri e titolo nel JSON versionato del modulo.
- **Salva .mdst** produce il formato compatibile con il programma originale; questo formato non conserva il titolo.
- **Esporta risultati** produce un testo con tutti gli input, i risultati e le note del motore.
- **Esporta JSON** produce input e risultati numerici utilizzabili da altri programmi.

La scheda **Note di calcolo** riporta le limitazioni applicabili. Il modulo conserva il progetto quando si chiude e si riapre la finestra nella stessa sessione. Salvare su file prima di chiudere TA-Calcolo.

Punto e virgola decimale sono accettati. Gli input errati non sono rimpiazzati da valori predefiniti. Un file incompleto, di un altro modulo o non valido viene rifiutato prima di modificare il progetto aperto.

## Motore rivisto NTC 2018 (predefinito)

Nella Sintesi si sceglie il **motore di calcolo**: *Rivisto NTC 2018* (predefinito) oppure *Originale (confronto)*. Il motore originale resta identico al sorgente e coperto dalle fixture di parità; il rivisto (`WallDesignCalculator`, `src/STA.MuroSostegno.Core/WallDesign.cs`) corregge le anomalie emerse dall'analisi:

| # | Anomalia del sorgente | Correzione nel motore rivisto |
|---|---|---|
| 1 | Spinta allo scorrimento su H1, al ribaltamento sull'altezza fino alla base del dente | Stessa altezza per entrambe: dalla superficie a monte in x = Bf alla base del dente |
| 2 | Acqua nello scorrimento come `(Qf − y)·10/2`, sommata anche alla resistenza; Qfa ignorata | Spinte idrostatiche γw·h²/2 a monte (Qfp) e a valle (Qfa), sottospinta con quota piezometrica lineare fra valle e monte, γ′ = γ − γw sotto falda |
| 3 | Resistenza "Q" pari alla spinta del sovraccarico | Il sovraccarico è solo un'azione (spinta); il suo peso non è portato in conto |
| 4 | KP con la pendenza a monte Itp | KP con la pendenza a valle It (negativa per la passiva), δ = 0 |
| 5 | Poligono immerso chiuso sull'origine, peso immerso con segno errato | Nessun poligono immerso: l'acqua agisce come pressioni sul contorno |
| 6 | G moltiplicato per g (Vs scalato di √g); B_H discontinuo (0,35/0,4) | H/(Vs·Tm) senza g; B_H continuo da 1 a 0,4 (da confermare sulla fonte) |
| 7 | Nessuna inerzia, nessun kv | Inerzie kh·W di muro e terreni, kv = ±0,5·kh con θ = atan[kh/(1 ∓ kv)]; si riporta il verso più gravoso |
| 8 | Coefficienti A1 anche in sisma, terreno come G2 | Ribaltamento EQU+M2 (γR 1,15), scorrimento A1+M1+R3 (γR 1,1), sisma γ = 1, M1 (γR 1,0); terreno e acqua come G1 |

**Ribaltamento** attorno al piede anteriore: per ogni azione il coefficiente è scelto in base all'effetto netto; la componente verticale della spinta (tan δ) agisce in x = Bf. Verifica: Mstab,d/(γR·Mrib,d) ≥ 1.

**Scorrimento** sul piano orizzontale per la base del dente (il blocco include il terreno fra base e piano): Rd = N·tan φ′d/γR + 50 % della passiva a valle/γR,valle (1,4 statica, 1,2 sisma); verifica Rd/Ed ≥ 1.

**Limiti:** formule di spinta (F.1)–(F.4) del sorgente sul piano virtuale verticale, interpretate come componente orizzontale; distribuzione della spinta sismica uguale alla statica; nessuna sovrappressione idrodinamica; c′ non utilizzata; esclusi capacità portante, stabilità globale, cedimenti e armature. Il valore di esempio Vs = 0,209 (m/s nel sorgente, probabilmente km/s) porta B_H al minimo: inserire Vs in m/s.

Test: caso di Rankine con formule chiuse per ribaltamento EQU+M2 e scorrimento A1+M1+R3; falda simmetrica (spinte d'acqua e sottospinta in forma chiusa); sovraccarico solo come azione; KP con It; B_H continuo; domini sismici non validi rifiutati.

## Provenienza e ambito numerico

Sorgente consegnato: `TA-MuroDiSostegno-master.zip`, repository di origine `robetass/TA-MuroDiSostegno`.
SHA-256 dello ZIP: `B71C7BAB633D836657EDA2EE0B2CE2F651B25732920818E2EC086232B06C6D02`.

Sono state estratte le formule da `SpinteDelTerreno.cs`, `Verifiche.cs`, il calcolo di area/baricentro da `GeomAnalitica.cs` e l'orchestrazione/geometria da `Form1.cs`. Le operazioni vettoriali MathNet usate dal sorgente erano somme di tre prodotti: sono espresse con array senza introdurre una dipendenza esterna. I sei vertici finali nulli del muro sono omessi perché coincidenti con il punto iniziale; i quattordici vertici utili conservano la numerazione originale.

I risultati sono di **confronto con il motore originale**, non una verifica normativa completa. In particolare:

- Il sorgente applica i coefficienti A1 anche al caso con PGA. I rapporti Mstab/Mrib vengono mostrati senza convertirli in un esito verde/rosso di sicurezza.
- Lo scorrimento presenta le componenti G1, G2 e Q di spinta sismica e resistenza statica esattamente come la finestra originale. Non è ricavata una verifica complessiva aggiuntiva.
- Nel ramo con falda sopra la base del dente, `PesoMuro -= -10 * AreaMImm` incrementa il peso invece di ridurlo. Il comportamento è conservato per la parità e segnalato nelle note, con esplicita necessità di revisione del modello idraulico. Non è avallato per il dimensionamento.
- Il valore iniziale di Vs è `0,209`, etichettato **m/s** nel sorgente. Non viene interpretato implicitamente come km/s. Un valore inferiore a 10 m/s attiva una nota di controllo dell'unità.
- La coesione `C1k` è conservata nei file ma non è usata dalle formule. La falda anteriore `Qfa` è solo grafica.
- Il modello originale di sottospinta, i fattori sismici e l'interpolazione di B_H non sono stati revisionati.
- Sono esclusi capacità portante, stabilità globale, cedimenti e dimensionamento delle armature.

Le modifiche numeriche intenzionali riguardano soltanto il **rifiuto di input non validi**: geometrie incompatibili, valori non finiti, file parziali e risultati fuori dominio. I calcoli nei casi ammessi sono confrontati con quelli originali.

## Verifiche

`tests/STA.Calcolo.Tests/Reference/retaining-wall-original.json` contiene 13 casi generati eseguendo i metodi estratti dallo ZIP, indipendentemente dal motore integrato: variazioni di falda (sopra e sotto la fondazione), dente, geometria, carico, pendenze, Vs e PGA. Per ciascun caso si confrontano 29 risultati e le coordinate dei 14 vertici, con tolleranza relativa 1e-9.

Ulteriori test verificano il caso analitico di Rankine (φ=30°, δ=β=PGA=0: KA=1/3 e KP=3), area di una sezione scomponibile, JSON/MDST, numeri italiani, import atomico, validazione geometrica e note. I test WPF verificano tema, binding, layout largo/compatto, apertura dalla home, salvataggi, esportazioni e invalidazione.

```powershell
dotnet build Calcolo.sln -c Release
dotnet tests/STA.Calcolo.Tests/bin/Release/net10.0/STA.Calcolo.Tests.dll
dotnet tests/STA.Desktop.Smoke/bin/Release/net10.0-windows/STA.Desktop.Smoke.dll
node tools/verify-architecture.mjs
```
