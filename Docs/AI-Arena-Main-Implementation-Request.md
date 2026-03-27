# AI Arena Main Implementation Request

## Contexto

Este request deve ser executado na `main` ou em uma branch de integracao derivada da `main`.

O objetivo e integrar um modulo de AI Arena no jogo usando o contrato atual de input por combatente, sem quebrar o comportamento humano existente e sem criar regras por personagem.

Antes de implementar, leia:

- `Docs/AI-Arena-Requirements.md`
- `Docs/AI-Combat-Perception-Layer.md`
- `INPUT_SOURCE_OF_TRUTH.txt`

## Objetivo funcional

Permitir que cada slot do match seja controlado por uma origem configuravel:

- `Human`
- `AI`
- `Idle`

O jogo deve suportar `AI vs AI` no fluxo atual de partida, com respawn, reset de round e troca de personagem funcionando normalmente.

## Requests de implementacao

### 1. Adicionar controle por slot

- Introduzir uma configuracao por slot para selecionar `Human`, `AI` ou `Idle`.
- Essa configuracao nao pode depender de `CharacterDefinition`.
- O `MatchController` e o fluxo de runtime combatants precisam continuar compativeis com isso.

### 2. Adicionar fonte de input para AI

- Criar um novo `ICombatantInputSource` dedicado a AI.
- O `PlayerController` deve continuar consumindo input do mesmo jeito que hoje.
- A IA so pode sugerir `PlayerInputFrame`; ela nao pode mutar estado interno do personagem.

### 3. Adicionar camada de snapshot

- Criar um builder de snapshot separado do input source.
- O snapshot precisa ser canonico, versionado e personagem-agnostico.
- O snapshot minimo deve incluir:
  - ids de match/round/frame
  - estado do proprio slot
  - estado resumido do oponente
  - projetis ativos
  - arena/wrap bounds
  - placar e estado do round

### 4. Adicionar camada de traducao semantica

- Nao enviar apenas estado cru do runtime.
- Criar uma camada que derive sinais taticos e eventos de combate.
- Usar `Docs/AI-Combat-Perception-Layer.md` como base.

### 5. Adicionar backend de ponte

- No MVP, usar um backend local e assincrono.
- A thread principal nao pode bloquear em IO.
- O sistema precisa lidar com timeout, parse invalido e desconexao.

### 6. Adicionar observabilidade

- O HUD de debug precisa mostrar:
  - origem de controle por slot
  - status de conexao da AI
  - latencia por frame
  - timeouts
  - ultimo comando aplicado
- O sistema precisa logar snapshot e acao por frame.

### 7. Adicionar bot de exemplo

- Criar um bot simples e deterministicamente previsivel apenas para validar o loop.
- Ele nao precisa ser forte; precisa provar o pipeline.

### 8. Adicionar testes

- Tradução de snapshot
- Parse e saneamento de acao
- Fallback em timeout
- Compatibilidade com respawn e reset de round
- Compatibilidade com `MatchController.EnsureRuntimeCombatantsForConfiguredSlots()`
- Regressao: teclado e gamepad nao podem quebrar quando o modulo esta desligado

## Criterios de aceite

- Dois slots podem lutar em `AI vs AI`.
- `Human vs AI` nao e obrigatorio no primeiro merge, mas a arquitetura nao pode impedir isso.
- O loop da partida continua funcional com respawn e contagem de vitorias.
- O jogo nao trava se a AI nao responder.
- O modulo nao quebra o contrato de input atual.
- O modulo nao cria excecoes por Mizu ou Storm Dragon.
- Existe documentacao minima para rodar o bot de exemplo.

## Decisoes recomendadas

- Manter Unity como autoridade da simulacao.
- Sincronizar o tick da AI com `FixedUpdate`.
- Usar contrato de acao baseado em `PlayerInputFrame`.
- Separar:
  - snapshot builder
  - semantic translator
  - transport bridge
  - AI input source
  - debug/log services

## O que nao fazer

- Nao colocar logica de rede diretamente dentro do `PlayerController`.
- Nao acoplar o modulo a personagem especifico.
- Nao depender de LLM remoto respondendo frame a frame no MVP.
- Nao mandar o estado cru inteiro do Unity sem tradutor semantico.
- Nao introduzir branches de comportamento diferentes por character asset.
