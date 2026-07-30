# Test manuali consigliati

## 1. Configurazione base

1. apri `Configura palestra`
2. crea una sala
3. crea una parete
4. aggiungi un pannello
5. salva la parete

Verifica:

- la parete compare nella sala corretta
- il pannello compare nella lista
- la build dati non va in errore

## 2. Immagine pannello

1. seleziona un pannello
2. carica un'immagine
3. muovi offset X/Y
4. modifica scala
5. prova `Auto allinea immagine ai fori`

Verifica:

- l'overlay segue il pannello selezionato
- cambiando pannello cambia anche l'immagine mostrata
- l'auto align non manda in errore la pagina

## 3. Analisi prese

1. con un pannello selezionato e con immagine caricata
2. apri `Analizza prese dal pannello`
3. controlla i ritagli immagine dei fori
4. salva

Verifica:

- i ritagli siano vicini al foro corretto
- il salvataggio non fallisca

## 4. Mapping hardware

1. apri `Mapping hardware`
2. seleziona sala e parete
3. modifica alcuni `pointId`
4. modifica alcuni `LED`
5. salva

Verifica:

- conflitti duplicati evidenziati
- rinumerazione automatica funzionante
- dati ricaricati correttamente dopo riapertura

## 5. Circuiti

1. apri `Circuiti`
2. seleziona la sala
3. crea un circuito
4. scegli i colori globali con il picker del circuito
5. imposta due start
6. aggiungi movimenti
7. imposta top
8. salva

Verifica:

- colori corretti per DX, SX, Start, Top
- il picker aggiorna anteprima e valore colore senza input testuale manuale
- sequenza unificata dei movimenti
- miniature immagine coerenti

## 5B. Settings circuito

1. apri `Settings`
2. vai alla sezione `Circuiti`
3. cambia i quattro colori usando il picker
4. salva
5. torna in `Circuiti`
6. crea un nuovo circuito

Verifica:

- i nuovi default colore arrivano nel nuovo circuito
- i circuiti gia esistenti non vengono modificati automaticamente
- il picker di `Settings` aggiorna anteprima e valore coerentemente

## 6. Utility ESP32

1. apri `Utility`
2. inserisci base URL e controller ID
3. prova health/status
4. prova invio configurazione parete

Verifica:

- i payload partano
- nessun crash UI

## 7. Esegui Circuiti

Prerequisiti:

- esiste almeno una sala con una parete salvata
- la parete ha mapping hardware valido
- esiste almeno un circuito associato alla parete
- l'ESP32 e' raggiungibile via `Base URL`

### Caso A. Visualizzazione circuito con sync automatico

1. apri `Esegui Circuiti`
2. seleziona `Sala`
3. seleziona `Parete`
4. seleziona un circuito della parete
5. premi `Visualizza`

Verifica:

- nella mappa app compare il circuito corretto
- se la config parete non era presente su ESP32, viene inviata prima del comando
- se il catalogo circuiti non era presente o non coerente, viene sincronizzato prima del comando
- sull'ESP32 si accendono tutti i LED del circuito con i colori corretti
- il comando non avvia la sequenza dinamica

### Caso B. Avvio circuito con sync automatico

1. con lo stesso circuito selezionato
2. premi `Avvia`

Verifica:

- se necessario, l'app sincronizza prima parete e circuiti
- la sequenza del circuito parte davvero su ESP32
- i LED seguono l'ordine dei `steps`
- lo stato ESP32 passa a circuito attivo

### Caso C. Stop / Spegni

1. con circuito visualizzato o avviato
2. premi `Stop / Spegni`

Verifica:

- i LED si spengono
- il runtime torna in stato `Idle`
- non ci sono eccezioni UI

### Caso D. Cambio parete

1. seleziona una parete con circuiti
2. poi cambia sala o parete

Verifica:

- la lista circuiti si aggiorna solo con i circuiti della nuova parete
- il circuito precedentemente selezionato non resta agganciato se non appartiene alla nuova parete
- la preview grafica mostra la nuova parete

### Caso E. Parete senza circuiti

1. seleziona una parete senza circuiti

Verifica:

- la lista mostra stato vuoto
- `Visualizza` e `Avvia` restano disabilitati
- `Stop / Spegni` resta disponibile

### Caso F. ESP32 non raggiungibile

1. imposta un `Base URL` errato oppure spegni il device
2. premi `Visualizza` oppure `Avvia`

Verifica:

- la pagina mostra un messaggio di errore chiaro
- non si blocca la UI
- al tentativo successivo la pagina resta usabile

## 8. Regressioni importanti da controllare

- `Configura palestra` deve restare fluida
- i click sui bottoni non devono causare eccezioni
- le immagini devono essere legate al pannello, non alla parete
- i crop dei fori devono usare il pannello giusto
- `Esegui Circuiti` non deve alterare i dati dei circuiti locali
- `Visualizza` e `Avvia` devono restare semanticamente distinti
