TA-Calcolo 1.10.0: il modulo Vento sulle costruzioni mostra la figura dei risultati e le valutazioni intermedie, salva e riapre i dati ed esporta il capitolo Word sul modello aziendale.

- **Figura dei risultati:** nuova scheda con la sezione nella direzione del vento e le pressioni disegnate sulle superfici, con i valori in N/m² e le ascisse dei confini fra le zone. Se la Circolare fornisce due valori si sceglie la condizione di carico rappresentata.
- **Estensione delle zone:** per ogni zona della copertura la tabella indica l'estensione dal bordo sopravento, in orizzontale (x) e sullo sviluppo (s). La volta cilindrica è divisa in quattro zone di uguale sviluppo: A, B, B, C (§C3.3.8.1.7).
- **Valutazioni:** nuova scheda con i passaggi che portano ai coefficienti: h, d, f, h/d, f/d, quote di riferimento, campo applicato, letture sulle curve e peso dell'interpolazione.
- **Apri e Salva:** i dati del modulo si salvano e si riaprono in un file JSON; un file errato o di un altro modulo viene rifiutato senza alterare il lavoro aperto.
- **Capitolo Word:** esportazione del solo capitolo "Azione del vento" sul modello Word di Tassinari Associati (stili, titoli numerati, intestazione e piè di pagina), senza frontespizio né sommario, con valutazioni, risultati, distribuzione delle zone e figure.
- **Icona del modulo**, disegnata dal pittogramma Tassinari Associati.

## Variazioni dei risultati rispetto alla 1.9.0
- **Pareti di un edificio con volta cilindrica:** la quota di riferimento è ze = h + f/2, la stessa della copertura, e il rapporto h/d usa la quota di imposta h; prima entrambe usavano l'altezza di sommità h + f. Le pressioni sulle pareti risultano più basse. Esempio con b = 90, d = 80, h = 5, f = 10 m: cpe da +0,72 / −0,65 / −0,34 a +0,71 / −0,55 / −0,31 e ze da 15 a 10 m.
- **Falde multiple con colmi:** compare anche l'ultima falda discendente, che prima mancava dai risultati.
- **Coperture con fascia sopravento estesa a tutta la profondità:** non compaiono più le "restanti zone".
- I valori di falde, padiglione, zone della volta e tettoie sono invariati.

## Verifiche
79 test C#, di cui 3 nuovi su estensione delle zone, valutazioni e file di progetto, con valori calcolati a mano dalla Circolare. Controllo del capitolo Word (stili del modello, assenza di frontespizio e sommario, tabelle, figure) e apertura del documento in Microsoft Word. Test WPF sulla finestra reale: figura dei risultati, valutazioni, salva e apri, esportazione.

## Ambito e limiti
Sono implementati i coefficienti globali; i coefficienti locali per zone non sono tabellati. Non sono trattati edifici a pianta circolare, cupole, travi e tralicci. Le curve della volta cilindrica sono lette per punti dalla Fig. C3.3.19 (c). Per il padiglione l'estensione delle falde non è indicata. La relazione del vento è solo in Word. Il modello Word è una copia incorporata: se quello aziendale cambia va aggiornata con una nuova versione del programma.

## Installazione e aggiornamento
Installer Windows x64 con runtime incluso, pacchetto per l'aggiornamento automatico e file SHA256SUMS. I progetti esistenti restano compatibili.
