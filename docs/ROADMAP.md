# Roadmap

## Fase 1: consolidamento configurazione

- validare bene immagini pannello
- migliorare auto allineamento immagine
- mostrare in elenco pannelli se un pannello ha gia una foto
- rifinire analisi prese automatica
- ripulire i residui legacy immagine a livello parete quando non serviranno piu

## Fase 2: consolidamento circuiti

- migliorare UX tracciatura circuito
- affinare sequenza start -> movimenti -> top
- gestione migliore dei cambi mano
- piu strumenti visivi nella preview
- verifica qualitativa dei crop immagine dei movimenti

## Fase 3: integrazione ESP32

- completare flusso push configurazione parete
- completare flusso push circuiti
- verificare convenzione `pointId` e `ledIndex`
- test reale end-to-end app -> ESP32
- gestione stato controller e diagnostica

## Fase 4: allenamento

- sezione dedicata allenamento
- scelta circuito
- invio comando start/stop
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
