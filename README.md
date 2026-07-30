# KKKK KonKiKingKong

App .NET MAUI per Android dedicata alla configurazione di una palestra di arrampicata, alla definizione delle pareti e dei pannelli, alla gestione dei fori e alla creazione dei circuiti.

## Obiettivo del progetto

L'app deve coprire tre aree principali:

1. Configurazione iniziale della palestra
2. Editing rapido dei circuiti
3. Allenamento/esecuzione dei circuiti

Oggi il progetto e' concentrato soprattutto su `configurazione` ed `editor circuiti`.

## Stato rapido

- Sale salvate su SQLite
- Pareti salvate su SQLite
- Pannelli salvati su SQLite
- Fori generati dai pannelli e salvati su SQLite con coordinate assolute
- Mapping hardware `foro -> pointId -> LED` salvato su SQLite
- Immagini associate ai `pannelli`, non piu alla parete
- Circuiti e movimenti salvati su SQLite
- Pagina dedicata per il mapping hardware della parete
- Prima versione di `auto allineamento immagine ai fori`
- Integrazione app lato ESP32 gia impostata a livello payload/API

## Vocabolario ufficiale

- `Sala`: contenitore logico principale.
- `Parete`: appartiene a una sala e contiene pannelli, fori e mapping hardware.
- `Pannello`: elemento della parete con geometria propria; ogni pannello puo' avere la sua immagine.
- `Immagine pannello`: foto associata al pannello selezionato, non alla parete intera.
- `Mapping hardware della parete`: relazione `foro -> pointId -> ledIndex` della parete selezionata.
- `Parete selezionata`: la parete su cui l'utente sta lavorando.
- `Pannello selezionato`: il pannello della parete attualmente in modifica.

## Pagine principali

- `Home`
- `Configura palestra`
- `Circuiti`
- `Mapping hardware`
- `Utility`

## Avvio rapido

Compilazione:

```powershell
dotnet build "C:\TMP\Prova\GF\WallPanelPlanner\WallPanelPlanner.csproj"
```

Per il dettaglio operativo guarda:

- [docs/STATUS.md](C:\TMP\Prova\GF\WallPanelPlanner\docs\STATUS.md)
- [docs/ARCHITECTURE.md](C:\TMP\Prova\GF\WallPanelPlanner\docs\ARCHITECTURE.md)
- [docs/ROADMAP.md](C:\TMP\Prova\GF\WallPanelPlanner\docs\ROADMAP.md)
- [docs/TESTING.md](C:\TMP\Prova\GF\WallPanelPlanner\docs\TESTING.md)
