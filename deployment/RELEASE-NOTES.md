TA-Calcolo 1.5.1: nuovo modulo Muro di sostegno, integrato sulla base della release 1.5.0.

- Nuova scheda nella schermata principale e interfaccia coerente con Muratura e cerchiatura: logo, font, colori, pannelli e campi numerici aziendali.
- Geometria di muro, fondazione e dente, terreni e falde; disegno vettoriale quotato e diagramma delle pressioni statiche.
- Risultati statici e sismici del motore originale: coefficienti di spinta, momenti stabilizzanti/ribaltanti e componenti di scorrimento.
- Apertura e salvataggio dei progetti JSON e dei file .mdst originali; esportazione dei risultati TXT e JSON.
- Validazione degli input, importazione atomica, conservazione della sessione e invalidazione dei risultati dopo ogni modifica.

## Verifiche
59 test C# superati, inclusi 13 casi confrontati con il programma originale (29 risultati e 14 vertici per caso), caso analitico di Rankine e round trip dei file. Test WPF su navigazione, sessione, layout, binding, esportazioni e risultati obsoleti. Verifiche degli altri moduli, architettura, aggiornamenti e relazioni comprese nel flusso di rilascio.

## Ambito e limiti
Le formule del programma TA-MuroDiSostegno sono conservate per confronto: non costituiscono una verifica normativa completa. Il modulo segnala il valore originale Vs = 0,209 m/s e l'anomalia di segno del peso immerso nel ramo con falda; il modello idraulico richiede revisione prima dell'uso per il dimensionamento. Le componenti di scorrimento non vengono trasformate in un esito complessivo. Escluse capacità portante, stabilità globale e armature.
Dettagli e provenienza: docs/MURO-SOSTEGNO.md e Guida-muro-sostegno.md distribuita con il programma.

## Installazione e aggiornamento
Installer Windows x64 con runtime incluso, pacchetto per l'aggiornamento automatico e file SHA256SUMS, come nelle release precedenti. Restano disponibili le funzionalità della versione 1.5.0.
