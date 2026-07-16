# Roadmap

## Fase 1: consolidamento configurazione

- validare bene immagini pannello
- migliorare auto allineamento immagine
- mostrare in elenco pannelli se un pannello ha gia una foto
- rifinire analisi prese automatica
- ripulire i residui legacy immagine a livello parete quando non serviranno piu

## Fase 2: consolidamento circuiti

- migliorare UX tracciatura circuito
- rifinire globali circuito e preset
- verifica qualitativa dei crop immagine dei movimenti
- validare import `ESP32 -> app` con merge coerente per `CircuitId`
- collaudo reale import/export editoriale circuiti

## Fase 3: integrazione ESP32

- collaudare flusso push configurazione parete
- collaudare flusso push circuiti runtime
- collaudare flusso editoriale `app <-> ESP32`
- collaudare davvero `Visualizza` vs `Avvia`
- verificare convenzione `pointId`, `ledIndex` e `holeNumber`
- migliorare gestione stato controller e diagnostica

## Fase 4: allenamento

- estendere la pagina `Esegui Circuiti`
- feedback piu ricco da stato runtime ESP32
- eventuale comando da app o da pulsanti fisici ESP32
- feedback visivo sincronizzato

## Fase 5: extra futuri

- export/import palestra
- condivisione circuiti
- statistiche
- gestione multi-parete piu avanzata

## Debito tecnico noto

- alcuni nomi legacy potrebbero ancora parlare di `wall image`; il termine corretto e' `immagine pannello`
- alcune classi UI sono ancora corpose
- servono piu test di integrazione reale su Android
- il firmware editoriale circuiti va compilato e collaudato con toolchain reale
