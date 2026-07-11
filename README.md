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
- Pagina dedicata per mapping hardware
- Prima versione di `auto allineamento immagine ai fori`
- Integrazione app lato ESP32 gia impostata a livello payload/API

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
