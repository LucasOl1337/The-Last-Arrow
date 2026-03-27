# AI Combat Perception Layer

## Objetivo

Definir o que uma IA competitiva precisa receber do jogo para jogar bem contra humanos.

Este documento nao descreve apenas serializacao. Ele descreve a camada de traducao entre o runtime cru do Unity e uma representacao de combate util para um agente.

## Principio central

Uma IA forte nao precisa de "todas as variaveis do jogo". Ela precisa de:

- estado canonico confiavel
- eventos taticos
- geometria jogavel relevante
- feedback suficiente para aprender e corrigir decisao

## O que eu gostaria de ter se eu fosse jogar para vencer

### 1. Estado de combate canonicamente limpo

- minha posicao e velocidade
- meu facing
- grounded e wall contact
- estado atual de dash, melee, ult e hitstun
- cooldowns reais
- flechas/recursos disponiveis
- estado equivalente do oponente
- projetis ativos com dono, direcao, velocidade e risco
- estado do round e placar

### 2. Eventos semanticos

Em vez de apenas estado cru, eu gostaria que o jogo emitisse eventos como:

- `opponent_started_dash`
- `opponent_started_melee`
- `opponent_started_ultimate`
- `opponent_entered_recovery`
- `projectile_spawned`
- `projectile_will_hit_me_soon`
- `parry_window_open`
- `anti_air_window_open`
- `ledge_escape_available`
- `kill_confirm_available`

Esses eventos valem muito para reduzir custo de decisao.

### 3. Geometria jogavel da fase

Esta arena tem plataformas, altura, bordas, rampas e espacos de tiro. Para jogar bem eu gostaria de receber:

- plataformas navegaveis relevantes
- tempo estimado de chegada por plataforma
- alvos alcancaveis com pulo
- linhas de tiro livres
- zonas de cobertura
- risco de cair ou ficar preso em borda

### 4. Feedback sobre previsao de risco

Eu gostaria de sinais derivados, nao so dados brutos:

- `distance_to_opponent`
- `melee_range_now`
- `dash_engage_possible`
- `unsafe_to_shoot`
- `unsafe_to_jump`
- `projectile_threat_score`
- `advantage_state`
- `neutral_state`
- `pressure_state`
- `recovery_state`

### 5. Feedback historico curto

Para decidir bem, eu gostaria de uma janela curta da luta:

- ultimos N frames de snapshot simplificado
- ultimas acoes do oponente
- ultimos eventos importantes
- ultimo motivo de dano recebido
- ultimo erro tatico detectado

## Camadas recomendadas

### Raw State Collector

Le o runtime cru:

- `PlayerController`
- `MatchController`
- `ProjectileController`
- arena e wrap bounds

### Canonical Snapshot Builder

Converte o runtime em um snapshot estavel, versionado e personagem-agnostico.

### Semantic Feature Extractor

Deriva features continuas para agente competitivo:

- distancias
- risco
- acessibilidade
- oportunidade de punicao
- janela de defesa

### Event Detector

Emite mudancas taticas discretas:

- inicio de dash
- fim de recovery
- projetil em rota de colisao
- vantagem de altura

### Agent Adapter

Expõe a informacao no formato ideal para cada classe de agente:

- bot deterministicamente programado
- agente de RL
- agente hibrido
- LLM com executor local

## Formato de exposicao recomendado

### Canal de tempo real

Usar payload curto e previsivel:

- snapshot resumido
- features taticas
- eventos recentes
- resposta de acao

### Canal assincrono de controle

Usar payload rico:

- resumo textual do round
- consulta de memoria
- replay curto
- metricas e diagnostico

## Features minimas para um agente competitivo

- distancia horizontal ao oponente
- distancia vertical ao oponente
- diferenca de altura
- velocidade relativa
- disponibilidade real de dash
- disponibilidade real de melee
- disponibilidade real de ult
- quantidade de projetis ameacando
- tempo estimado para colisao de projetil
- se o oponente esta em recovery
- se eu estou em recovery
- se tenho linha de tiro limpa
- se o oponente esta em alcance de engage
- se existe plataforma de fuga acessivel

## Eventos minimos para o MVP

- `round_started`
- `round_reset_pending`
- `opponent_died`
- `self_damaged`
- `opponent_started_dash`
- `opponent_started_melee`
- `opponent_started_ultimate`
- `projectile_spawned`
- `projectile_collision_risk`
- `parry_window_open`

## Por que isso importa

Se o modulo entregar apenas estado cru e input, a IA ate consegue jogar, mas joga cega e gasta muito custo para inferir tatica.

Se o modulo entregar percepcao taticamente util, ela consegue:

- reagir mais cedo
- punir melhor
- navegar melhor a arena
- entender matchup sem hacks
- evoluir com replay e ajuste fino

## Regra de ouro

Nao confundir "mais dados" com "melhor percepcao".

O sistema precisa entregar:

- menos ruido
- mais sinal
- semantica estavel
- neutralidade entre personagens
