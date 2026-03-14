# Architecture Overview

## Objetivo

O projeto Unity agora esta estruturado como base definitiva de desenvolvimento. A migracao do Godot serviu para levantar requisitos, mas a organizacao atual foi limpa para trabalhar diretamente com assets e scripts do Unity.

## Camadas

### 1. Assets de jogo

- `Assets/ProjectPVP/Characters`
- `Assets/ProjectPVP/Environment`
- `Assets/ProjectPVP/Scenes`

Responsabilidades:
- manter definicoes de personagens e arena em assets estaveis
- centralizar animacoes e sprites em pastas previsiveis
- deixar a cena principal pronta para teste sem rebuild de pipeline

### 2. Tooling leve de editor

- `Assets/ProjectPVP/Scripts/Editor/ProjectPvpEditorPlayModeSetup.cs`
- `Assets/ProjectPVP/Scripts/Editor/ProjectPvpInputManagerInstaller.cs`
- `Assets/ProjectPVP/Scripts/Editor/ProjectPvpPlayableValidator.cs`

Responsabilidades:
- iniciar o Play direto na cena correta
- garantir eixos de input no `InputManager`
- validar referencias essenciais do projeto

### 3. Runtime de gameplay

- `Assets/ProjectPVP/Scripts/Runtime/Input`
- `Assets/ProjectPVP/Scripts/Runtime/Gameplay`
- `Assets/ProjectPVP/Scripts/Runtime/Match`
- `Assets/ProjectPVP/Scripts/Runtime/Presentation`

Responsabilidades:
- capturar input local por player
- aplicar locomocao, pulo, dash, colisao e combate
- controlar projeteis, round flow e HUD
- expor gizmos e debug visual para polimento

## Fluxo principal de um frame

1. `KeyboardPlayerInputSource` captura teclado e gamepad em um `PlayerInputFrame`.
2. `PlayerController` consome o frame no `FixedUpdate`.
3. O player resolve locomocao, gravidade, dash, aim-hold e combate.
4. Se houver disparo, instancia `ProjectileController`.
5. `ProjectileController` simula voo, stick, coleta e interacao com players.
6. `MatchController` escuta mortes, contabiliza wins e respawna os players.
7. `ProjectPvpDebugHud` e `ProjectPvpCombatDebugGizmos` mostram o estado vivo do combate.

## Decisoes de engenharia

- `ScriptableObject` para dados de personagem e arena:
  facilita edicao no Inspector e deixa a arquitetura facil de explicar.

- assets por personagem:
  cada personagem agora tem pasta propria com `Data`, `Animations` e `Rotations`, o que reduz muito a confusao operacional.

- runtime separado da apresentacao:
  `PlayerController` cuida de mecanica; `CharacterSpriteAnimator` cuida da parte visual.

- debug integrado:
  HUD e gizmos fazem parte do fluxo de polimento, nao sao ferramentas temporarias.

## O que ja esta funcional

- movimento horizontal
- pulo e wall-jump
- dash com janela de parry
- aim-hold e release-to-fire
- projeteis com gravidade, stick e coleta
- melee
- stomp
- wins, respawn e wrap
- visualizador de hitboxes, hurtboxes e probes

## O que ainda e roadmap

- skills especificas por personagem
- ultimates com paridade total
- sincronizacao fina entre animacao e gameplay
- camada audiovisual mais polida
