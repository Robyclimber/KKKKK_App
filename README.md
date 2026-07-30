# RuoteLab

App .NET MAUI per Android dedicata alla configurazione di una palestra di arrampicata, alla definizione delle pareti e dei pannelli, alla gestione dei fori, alla creazione dei circuiti e alla loro esecuzione su ESP32.

## Obiettivo del progetto

L'app deve coprire tre aree principali:

1. Configurazione iniziale della palestra
2. Editing rapido dei circuiti
3. Allenamento/esecuzione dei circuiti

Oggi il progetto copre gia:

- `configurazione`
- `editor circuiti`
- `esecuzione circuiti` lato app
- `integrazione ESP32` lato payload/API
- `sincronizzazione circuiti editoriali` lato codice
- `Settings` con parametri globali app e colori circuito

## Stato rapido

- Sale salvate su SQLite
- Pareti salvate su SQLite
- Pannelli salvati su SQLite
- Fori generati dai pannelli e salvati su SQLite con coordinate assolute
- Mapping hardware `foro -> pointId -> LED` salvato su SQLite
- Routing LED a livello pannello con serpentina implicita
- Auto-mapping LED della parete derivato dal routing dei pannelli
- Immagini associate ai `pannelli`, non piu alla parete
- Circuiti, globali circuito e movimenti salvati su SQLite
- Colori circuito configurabili con picker in `Settings` e nell'editor circuito
- Pagina dedicata per il mapping hardware della parete
- Prima versione di `auto allineamento immagine ai fori`
- Pagina `Esegui Circuiti` con `Visualizza`, `Avvia`, `Stop / Spegni`
- Auto-sync app -> ESP32 prima dei comandi operativi
- Import/export editoriale circuiti via API ESP32 implementato a codice

## Vocabolario ufficiale

- `Sala`: contenitore logico principale.
- `Parete`: appartiene a una sala e contiene pannelli, fori e mapping hardware.
- `Pannello`: elemento della parete con geometria propria; ogni pannello puo' avere la sua immagine.
- `Routing LED pannello`: asse e verso iniziale della serpentina LED del pannello.
- `Immagine pannello`: foto associata al pannello selezionato, non alla parete intera.
- `Mapping hardware della parete`: relazione `foro -> pointId -> ledIndex` della parete selezionata.
- `Circuito editoriale`: circuito salvato come modello compatto con globali e movimenti.
- `Globali circuito`: preset, effetto, luminosita, colori, blink e durata hold usati dal circuito.
- `Parete selezionata`: la parete su cui l'utente sta lavorando.
- `Pannello selezionato`: il pannello della parete attualmente in modifica.

## Pagine principali

- `Home`
- `Configura palestra`
- `Circuiti`
- `Esegui Circuiti`
- `Settings`
- `Mapping hardware`
- `Utility`

## Avvio rapido

Compilazione:

```powershell
dotnet build "C:\TMP\Prova\GF\WallPanelPlanner\WallPanelPlanner.csproj"
```

Per il dettaglio operativo guarda:

- [docs/STATUS.md](docs/STATUS.md)
- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)
- [docs/ROADMAP.md](docs/ROADMAP.md)
- [docs/TESTING.md](docs/TESTING.md)
