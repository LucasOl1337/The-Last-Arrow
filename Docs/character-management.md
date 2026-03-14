# Character Management

## Estrutura

Cada personagem agora fica em uma pasta propria dentro de `Assets/ProjectPVP/Characters`.

Exemplo:

- `Assets/ProjectPVP/Characters/Mizu/Data`
- `Assets/ProjectPVP/Characters/Mizu/Animations`
- `Assets/ProjectPVP/Characters/Mizu/Rotations`

## O que editar em cada pasta

- `Data`
  - `CharacterDefinition` com stats, collider, timings e tuning de gameplay
- `Animations`
  - frames usados pelo `CharacterSpriteAnimator`
- `Rotations`
  - sprites base de orientacao e referencia visual

## Ajustes de gameplay

Os valores principais ficam no `CharacterDefinition`:

- movimento
- pulo
- gravidade
- dash
- collider
- projectile setup
- duracao e speed de animacoes por acao

## Ajustes visuais

Se a animacao estiver fora de sincronia, normalmente os pontos de ajuste sao:

- `actionAnimationDurations`
- `actionAnimationSpeeds`
- `actionSpriteScale`
- `actionSpriteOffset`
- `actionColliderOverrides`

## Fluxo recomendado

1. Abra a pasta do personagem.
2. Ajuste o `CharacterDefinition` no Inspector.
3. Use o Inspector customizado para navegar por `Data`, `Animations` e `Rotations`.
4. Se quiser limpar serializacao velha do YAML, rode `ProjectPVP/Characters/Reserialize Character Assets`.
5. Confira os frames em `Animations`.
6. Rode a cena `Bootstrap.unity`.
7. Use o HUD e `F3` para validar hitboxes e timings.
