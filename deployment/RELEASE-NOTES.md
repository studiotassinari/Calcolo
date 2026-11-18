TA-Calcolo 1.6.0: Muro di sostegno con motore rivisto NTC 2018, modifica grafica della sezione e ricalcolo automatico.

- **Motore rivisto NTC 2018 (predefinito).** Ribaltamento EQU + M2 (γR 1,15), scorrimento A1 + M1 + R3 (γR 1,1, 50 % della resistenza passiva a valle con γR 1,4), sisma pseudostatico con γ = 1, M1, kv = ±0,5·kh e forze d'inerzia di muro e terreno. Esito Rd/Ed con riquadri verdi/rossi.
- **Correzioni rispetto al programma originale:** stessa altezza di spinta per ribaltamento e scorrimento; spinte idrostatiche a monte e a valle e sottospinta lineare; sovraccarico solo come azione; KP con la pendenza a valle; eliminati il poligono immerso errato e l'errore di segno sul peso; coefficiente sismico senza il fattore g spurio e B_H continuo.
- **Motore originale ancora disponibile** per confronto dal selettore nella Sintesi; l'esportazione JSON contiene i risultati di entrambi.
- **Interfaccia più compatta:** tre colonne ridimensionabili, dati in righe con gruppi richiudibili, sintesi sempre visibile accanto al disegno.
- **Grip sul disegno:** trascinando i quadratini si modificano H1, S1, Ds1, Ba, S2, Bf, H2, H3, H4, Xd, S3, Hta, It, Htp, Itp, Qfa e Qfp (passo 5 cm, 1 cm con Ctrl, Esc annulla); le posizioni non ammesse sono rifiutate.
- **Ricalcolo automatico:** risultati, sintesi e diagramma si aggiornano a ogni modifica dei campi.

## Verifiche
62 test C# superati: i 13 casi di parità con il programma originale, il caso di Rankine con formule chiuse per ribaltamento EQU + M2 e scorrimento A1 + M1 + R3, falda simmetrica (spinte d'acqua e sottospinta), sovraccarico, KP, coefficiente sismico e grip. Test WPF su ricalcolo automatico, grip, selezione del motore, file ed esportazioni.

## Ambito e limiti
Escluse capacità portante, stabilità globale, cedimenti e armature. La distribuzione della spinta sismica segue quella statica e non sono considerate sovrappressioni idrodinamiche. Il tratto variabile di B_H va confermato sulla fonte normativa. Il valore di esempio Vs = 0,209 del programma originale porta B_H al minimo: inserire Vs in m/s. Dettagli: docs/MURO-SOSTEGNO.md e Guida-muro-sostegno.md distribuita con il programma.

## Installazione e aggiornamento
Installer Windows x64 con runtime incluso, pacchetto per l'aggiornamento automatico e file SHA256SUMS, come nelle release precedenti. I progetti JSON e .mdst esistenti restano compatibili.
