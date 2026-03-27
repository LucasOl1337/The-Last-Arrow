# AI Arena Handoff

## Objetivo desta branch

Esta branch existe para preparar o modulo de AI Arena com escopo claro, sem implementar o merge final na `main`.

Ela deve servir como pacote de handoff para outro agente executar a integracao principal depois.

## O que ja foi produzido aqui

- Requisitos funcionais, tecnicos, operacionais e de teste em `Docs/AI-Arena-Requirements.md`.
- Request formal para implementacao na `main` em `Docs/AI-Arena-Main-Implementation-Request.md`.
- Camada de percepcao e feedback desejada para uma IA competitiva em `Docs/AI-Combat-Perception-Layer.md`.
- Estrategia de polling e transporte em `Docs/AI-Transport-Performance.md`.
- Mock server externo para validar a ponte HTTP em `tools/ai_arena_mock_server.py`.
- Runtime inicial da AI Arena em `Assets/ProjectPVP/Scripts/Runtime/AI/`.

## O que ja existe em codigo nesta worktree

- `AiCombatSnapshotBuilder`: snapshot canonico minimo para IA.
- `AiActionSanitizer`: clamp e saneamento de acao.
- `AiHeuristicInputSource`: bot local jogavel sem engine externa.
- `AiHttpPollingInputSource`: ponte HTTP por polling assincrono.
- `AiIdleInputSource`: modo neutro.
- `AiArenaMatchConfigurator`: configuracao simples por slot.
- `PlayerController.AssignInputSource(...)`: ponto minimo para rebater a origem de input em runtime.

## Como este material deve ser usado

- Ler primeiro `Docs/AI-Arena-Requirements.md`.
- Ler depois `Docs/AI-Arena-Main-Implementation-Request.md`.
- Usar `Docs/AI-Combat-Perception-Layer.md` para orientar o design do tradutor semantico e do snapshot.

## Intencao de produto

- Suportar AI vs AI no fluxo atual do jogo.
- Preservar o contrato global de input.
- Manter Unity como simulador autoritativo.
- Permitir que agentes diferentes controlem os slots por ponte local ou backend local no MVP.
- Expor feedback suficiente para que um agente consiga jogar bem, aprender e ser depuravel.

## Limites desta branch

- Esta branch nao tenta reestruturar a `main`.
- Esta branch nao tenta implementar o merge final do modulo dentro da `main`.
- Esta branch nao muda o contrato principal do gameplay.
- Esta branch prepara a especificacao e a direcao de implementacao.

## Decisoes recomendadas para o MVP

- Manter exatamente 2 slots.
- Comecar com transporte local.
- Usar `JSON` no canal de controle e um payload compacto no canal de tempo real.
- Implementar `Human`, `AI` e `Idle` por slot.
- Adotar timeout com fallback previsivel.
- Adicionar logs e HUD minimos desde o primeiro corte.

## Observacao importante para o agente da main

Nao tratar o modulo como apenas "socket + input". O valor real vem de tres camadas:

- ponte de controle em tempo real
- camada de percepcao/traducao semantica
- observabilidade e replay/log

Se uma dessas tres partes ficar de fora, o modulo nasce fraco.
