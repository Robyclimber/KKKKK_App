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
4. imposta due start
5. aggiungi movimenti
6. imposta top
7. salva

Verifica:

- colori corretti per DX, SX, Start, Top
- sequenza unificata dei movimenti
- miniature immagine coerenti

## 6. Utility ESP32

1. apri `Utility`
2. inserisci base URL e controller ID
3. prova health/status
4. prova invio configurazione parete

Verifica:

- i payload partano
- nessun crash UI

## 7. Regressioni importanti da controllare

- `Configura palestra` deve restare fluida
- i click sui bottoni non devono causare eccezioni
- le immagini devono essere legate al pannello, non alla parete
- i crop dei fori devono usare il pannello giusto
