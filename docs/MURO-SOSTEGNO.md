# Muro di sostegno

Modulo pubblicato in TA-Calcolo **1.5.1** a partire dal tag pubblicato **v1.5.0**, commit `fd0fa950e4877e7f6181666d1925aeddb4863626`.

## Utilizzo

La nuova scheda **Muro di sostegno** segue le schede esistenti nella schermata principale. La finestra riprende il tema di Muratura e cerchiatura: Acherus Grotesque, logo aziendale, blu e petrolio, pannelli bianchi e campi numerici gialli.

Gli input sono suddivisi in Geometria, Materiali e carichi, Terreno e falda, Sisma. Il disegno vettoriale mostra muro, dente, terreni, falde e quote principali con la medesima scala sui due assi. Dopo il calcolo compare anche il diagramma delle pressioni statiche.

- **Apri progetto** legge JSON e i file `.mdst` del programma originale.
- **Salva progetto** conserva parametri e titolo nel JSON versionato del modulo.
- **Salva .mdst** produce il formato compatibile con il programma originale; questo formato non conserva il titolo.
- **Calcola** aggiorna i rapporti e la tabella dei risultati statici e sismici.
- **Esporta risultati** produce un testo con tutti gli input, i risultati e le note del motore.
- **Esporta JSON** produce input e risultati numerici utilizzabili da altri programmi.

La scheda **Note di calcolo** riporta le limitazioni applicabili. Il modulo conserva il progetto quando si chiude e si riapre la finestra nella stessa sessione. Salvare su file prima di chiudere TA-Calcolo.

Punto e virgola decimale sono accettati. Ogni modifica invalida immediatamente i risultati e disabilita l'esportazione. Gli input errati non sono rimpiazzati da valori predefiniti. Un file incompleto, di un altro modulo o non valido viene rifiutato prima di modificare il progetto aperto.

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
