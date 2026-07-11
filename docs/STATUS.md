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
- preparare payload per ESP32

## Cosa funziona oggi

### Configurazione palestra

- gestione sale
- gestione pareti
- gestione pannelli
- selezione pannello esistente per modifica/eliminazione
- preview della parete con zoom
- overlay immagine sul pannello selezionato
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
- movimenti circuito

### Circuiti

- circuito legato a una sola parete
- filtri per sala
- sequenza movimenti unica
- supporto start dx/sx, top dx/sx, movimenti dx/sx
- colori distinti tra mano destra, mano sinistra, start e top
- miniature immagine vicino ai movimenti

### Mapping hardware

- pagina dedicata `Mapping hardware`
- filtro `solo conflitti`
- rinumerazione automatica LED
- salvataggio su DB

### ESP32

Gia presenti:

- modello impostazioni controller
- builder payload parete
- builder payload circuiti
- client API ESP32
- pagina utility con azioni base

## Problemi aperti noti

- l'auto allineamento immagine e' ancora euristico, da validare sul campo
- l'analisi prese automatica va ancora raffinata
- esistono ancora campi legacy immagine a livello `wall`, mantenuti per compatibilita dati
- manca ancora la vera sezione `allenamento`
- la UX di configurazione foto e pannelli puo' essere resa piu chiara

## Ultime decisioni importanti

- il mapping hardware e' stato spostato fuori da `Configura palestra` per evitare rallentamenti
- l'immagine e' stata spostata da `parete` a `pannello`
- i ritagli immagine per fori e movimenti ora usano il pannello corretto

## Stato build

Ultima build verificata:

```powershell
dotnet build "C:\TMP\Prova\GF\WallPanelPlanner\WallPanelPlanner.csproj"
```

Esito:

- build OK
- warning noti su `SQLitePCLRaw`

## Priorita attuali

1. validare bene immagini pannello, crop e auto allineamento
2. migliorare UX configurazione palestra
3. consolidare editor circuiti
4. completare flusso ESP32 app -> controller
5. aprire la futura sezione allenamento
