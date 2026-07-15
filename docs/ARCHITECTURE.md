# Architettura

## Struttura del progetto

- `Models`
  contiene i modelli di dominio e gli state model delle pagine
- `ViewModels`
  contiene lo stato applicativo usato dalle pagine
- `Services`
  contiene logica applicativa, persistenza, helper immagine, payload ESP32
- `Persistence`
  contiene entity SQLite e factory DB
- `Drawing`
  contiene il renderer della preview parete/circuito
- `Resources`
  contiene tema e asset

## Dominio principale

## Vocabolario ufficiale

- `Sala`
  contenitore logico principale
- `Parete`
  appartiene a una sala e contiene pannelli, fori e mapping hardware
- `Pannello`
  elemento geometrico della parete; ogni pannello puo' avere la sua immagine
- `Immagine pannello`
  foto associata al pannello selezionato
- `Mapping hardware della parete`
  relazione `foro -> pointId -> ledIndex` della parete selezionata

### Sala

- una sala ha piu pareti

### Parete

- appartiene a una sala
- contiene piu pannelli
- contiene il layout fori derivato dai pannelli
- contiene il mapping hardware dei fori

### Pannello

- ha geometria propria
- genera i fori locali
- puo' avere una propria immagine con offset/scala/opacita
- non contiene il mapping hardware

### Foro

- ha numero progressivo
- ha coordinate assolute sulla parete
- ha coordinate relative sul pannello
- puo' avere metadata hardware
- puo' avere metadata presa

### Circuito

- appartiene a una sala
- e' legato a una parete
- contiene una sequenza di movimenti

## Persistenza

SQLite:

- `rooms`
- `walls`
- `panels`
- `wall_holes`
- `circuits`
- `circuit_movements`

Nota:

- i campi immagine storici sulla parete esistono ancora per compatibilita legacy
- i dati reali correnti dell'immagine stanno sul pannello
- il vocabolario UI corretto e' `immagine pannello`, non `immagine parete`

## Servizi chiave

- `GymSetupService`
  logica di creazione/aggiornamento sala, parete, pannello
- `SqliteWallRepository`
  salvataggio/caricamento pareti, pannelli e fori
- `WallConfigurationStorageService`
  salvataggio applicativo della parete
- `HoldAnalysisSuggestionService`
  euristiche per suggerire il tipo di presa
- `PanelImageAlignmentService`
  prima versione auto-align immagine pannello
- `Esp32PayloadBuilderService`
  costruzione payload verso firmware
- `Esp32ApiClient`
  chiamate HTTP al controller

## Pagine UI

- `HomePage`
  ingresso app
- `GymSetupPage`
  configurazione palestra
- `HardwareMappingPage`
  mapping hardware della parete
- `CircuitPage`
  editor circuiti
- `HoldAnalysisPage`
  analisi/suggerimento prese
- `UtilityPage`
  utility e pannello tecnico ESP32

## Principi attuali

- tenere la UI principale leggera
- spostare le aree tecniche in pagine dedicate
- salvare tutto il necessario su DB
- separare il piu possibile dominio, persistenza e UI
