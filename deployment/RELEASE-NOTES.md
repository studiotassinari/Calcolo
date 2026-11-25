TA-Calcolo 1.9.0: nuovo modulo Vento sulle costruzioni, con i coefficienti aerodinamici della Circolare 2019 per edifici, coperture e tettoie.

- **Nuovo modulo Vento sulle costruzioni:** dalla schermata iniziale, calcola per ogni superficie il coefficiente aerodinamico, la quota di riferimento ze, ce(ze) e la pressione p = qr · ce(ze) · c · cd in N/m², secondo la Circolare 21/01/2019 n. 7, §C3.3.8.
- **Edifici a pianta rettangolare:** pareti sopravento, laterali e sottovento; coperture piane, a falda singola, a falda doppia, a padiglione, a falde multiple (i quattro tipi della Fig. C3.3.18) e a volta cilindrica. Vento perpendicolare o parallelo al colmo.
- **Tettoie:** a falda singola, doppia e multipla, con grado di bloccaggio φ e forza risultante sulla falda in kN.
- **Pressione interna (§C3.3.8.5):** porosità uniforme (cpi = +0,2 e −0,3) oppure apertura dominante (0,75 o 0,90 cpe), con le colonne di pressione netta.
- **Disegno quotato:** sezione e pianta con gli stessi simboli delle caselle (b, d, h, α, f), ridisegnate mentre si scrive.
- **Ricalcolo a ogni modifica:** non c'è un pulsante di calcolo; con dati non validi la tabella si svuota e la barra di stato ne indica il motivo.
- **Vento del sito:** stessi campi e stessa validazione del modulo Carico vento, che resta invariato.

## Verifiche
76 test C#, di cui 8 nuovi sul vento: i valori attesi sono letti da tabelle e figure della Circolare e calcolati a mano (pareti, falde, padiglione, volta, tettoie, pressione interna, quote di riferimento, pressioni e forze). Test WPF sulla finestra reale: ricalcolo, colonne dinamiche, disegno, avvertenze, dati non validi e assenza di errori di binding.

## Ambito e limiti
Sono implementati i coefficienti globali; i coefficienti locali per zone (cpe,10 e cpe,1) non sono tabellati. Non sono trattati edifici a pianta circolare, cupole, travi e tralicci. Le curve della volta cilindrica sono lette per punti dalla Fig. C3.3.19 (c). b è sempre la dimensione ortogonale al vento e d quella parallela: cambiando direzione vanno scambiate. Fuori dal campo delle tabelle il calcolo prosegue con i valori limite e mostra un'avvertenza; quote oltre 200 m sono rifiutate. Il modulo non salva né riapre progetti e non produce una relazione.

## Installazione e aggiornamento
Installer Windows x64 con runtime incluso, pacchetto per l'aggiornamento automatico e file SHA256SUMS. I progetti esistenti restano compatibili.
