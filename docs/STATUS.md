# Stato attuale

## Visione generale

L'app permette gia di:

- creare una o piu sale
- creare una o piu pareti per sala
- aggiungere pannelli alle pareti
- generare automaticamente i fori in base alla geometria dei pannelli
- salvare fori e metadata hardware su database
- associare immagini ai pannelli e allinearle
- creare circuiti e movimenti sulle pareti
- preparare ed eseguire i flussi app -> ESP32 per i circuiti
- importare/esportare circuiti editoriali con ESP32 a livello codice
- preparare payload per ESP32

## Cosa funziona oggi

### Configurazione palestra

- gestione sale
- gestione pareti
- gestione pannelli
- routing LED a livello pannello
- selezione pannello esistente per modifica/eliminazione
- preview della parete con zoom
- overlay dell'immagine del pannello selezionato
- analisi prese dal pannello
- auto align iniziale immagine/fori tramite euristica

### Persistenza dati

Database SQLite con persistenza di:

- sale
- pareti
- pannelli
- fori
- posizione assoluta dei fori
- stato presa del foro
- tipo e dimensione presa
- pointId
- ledIndex
- flag `IsEnabled`
- circuiti
- globali circuito
- movimenti circuito

### Circuiti

- circuito associabile a una o piu pareti della stessa sala
- parete attiva selezionabile nell'editor con sequenza movimenti globale
- `CircuitId` stabile per sync con ESP32
- parametri globali circuito persistiti
- parametri globali di default configurabili da `Settings`
- filtri per sala
- sequenza movimenti unica
- supporto start dx/sx, top dx/sx, movimenti dx/sx
- colori distinti tra mano destra, mano sinistra, start e top
- scelta colori con picker dedicati sia in `Settings` sia nell'editor circuito
- miniature immagine vicino ai movimenti
- pagina separata `Esegui Circuiti`
- comandi `Visualizza`, `Avvia`, `Stop / Spegni`
- auto-sync `app -> ESP32` prima dei comandi operativi

### Mapping hardware

- pagina dedicata `Mapping hardware della parete`
- filtro `solo conflitti`
- auto-mapping LED dai pannelli
- routing LED pannello con serpentina implicita
- salvataggio su DB

### ESP32

Gia presenti:

- modello impostazioni controller
- builder payload parete
- builder payload circuiti
- builder payload circuiti editoriali
- client API ESP32
- pagina utility con azioni base
- endpoint editoriali previsti e integrati nel codice:
  - `GET /api/circuits/editorial`
  - `POST /api/circuits/editorial`
- endpoint operativi distinti previsti e integrati nel codice:
  - `POST /api/circuit/visualize`
  - `POST /api/circuit/start`
- import `ESP32 -> app` da Utility

## Problemi aperti noti

- il protocollo RouteLab Hub usa ancora un solo `WallId`: l'esecuzione hardware dei circuiti multi-parete richiede un'estensione firmware/API
- l'auto allineamento immagine e' ancora euristico, da validare sul campo
- l'analisi prese automatica va ancora raffinata
- esistono ancora campi legacy immagine a livello `wall`, mantenuti per compatibilita dati
- manca ancora la vera sezione `allenamento`
- la UX di configurazione foto e pannelli puo' essere resa piu chiara
- il firmware editoriale circuiti non e' stato ancora collaudato su hardware reale
- l'import/export editoriale circuiti e' verificato in build app ma non in end-to-end con ESP32 reale
- i nuovi endpoint `visualize/start` sono implementati ma non ancora validati con hardware reale

## Ultime decisioni importanti

- `WallName` resta la parete primaria compatibile, mentre `WallNamesJson` contiene tutte le pareti del circuito
- il suggerimento della presa successiva produce un piano statico con tecnica, equilibrio e sequenza piedi-baricentro-mano
- i movimenti dinamici sono esclusi dal calcolo corrente e rimandati a uno sviluppo futuro dedicato
- il mapping hardware e' stato spostato fuori da `Configura palestra` per evitare rallentamenti
- l'immagine e' stata spostata da `parete` a `pannello`
- i ritagli immagine per fori e movimenti ora usano il pannello corretto
- il branding app e namespace applicativi sono stati riallineati a `RouteLab`
- il lessico ufficiale distingue tra `parete selezionata`, `pannello selezionato`, `immagine pannello` e `mapping hardware della parete`
- il routing LED e' definito a livello pannello, con serpentina implicita
- il modello circuito locale include `CircuitId` e `Globals`
- i colori circuito non si inseriscono piu a mano: la UI usa picker guidati
- il canale circuiti editoriale `ESP32 <-> app` e' stato introdotto nel codice

## Stato build

Ultima build verificata:

```powershell
dotnet build ".\RouteLab.csproj"
```

Esito:

- build OK
- warning noti su `SQLitePCLRaw`
- firmware ESP32 non compilato in questo ambiente per assenza di `platformio`

## Priorita attuali

1. collaudare su hardware reale `Visualizza`, `Avvia`, `Stop / Spegni`
2. collaudare import/export editoriale circuiti su ESP32 reale
3. validare bene immagini pannello, crop e auto allineamento
4. migliorare UX configurazione palestra e mapping
5. aprire la futura sezione allenamento

