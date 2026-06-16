# Game Studio Unity Translation

## Objetivo

Traduzir o plugin `Game Studio` para o estado atual do projeto sem forcar Phaser, Three.js ou fluxo browser-first em uma base Unity 2D.

## Classificacao atual do projeto

- Jogo 2D de combate character-action.
- Unity e o simulador autoritativo.
- `PlayerController` e os systems de gameplay sao a fonte de verdade das regras.
- `Presentation` deve adaptar estado para sprite, HUD, video e feedback visual.

## Mapeamento das skills

### `game-studio`

Uso real no projeto:

- manter um plano unico entre gameplay, UI, assets e playtest
- evitar que arquitetura, feedback visual e pipeline de sprite evoluam de forma solta

### `web-game-foundations`

Traducao para Unity:

- simulacao fora da apresentacao
- input fisico mapeado para acoes explicitas
- assets organizados por dominio
- debug e perf com toggles claros
- estado serializavel e de rodada separado de objetos de view

Aplicar principalmente em:

- `Assets/ProjectPVP/Scripts/Runtime/Gameplay`
- `Assets/ProjectPVP/Scripts/Runtime/Input`
- `Assets/ProjectPVP/Scripts/Runtime/Match`
- `Assets/ProjectPVP/Scripts/Runtime/Presentation`

### `phaser-2d-game`

O runtime Phaser nao se aplica. O que fica:

- cena e bootstrap devem ser finos
- regras nao devem morar em callbacks de view
- camera, shake, VFX e animacao sao adaptadores de estado
- HUD denso nao deve competir com o combate

### `game-ui-frontend`

Traducao de DOM para Unity UI:

- Canvas, UI Toolkit ou IMGUI so para HUD, menus e overlays
- centro e metade inferior do playfield devem ficar livres durante combate normal
- um cluster persistente principal e, no maximo, um secundario compacto
- notas, lore, controles longos e debug detalhado ficam colapsados ou em toggle

Orcamento visual recomendado para o slice atual:

- cluster primario: placar, personagem, cooldowns criticos, municao
- cluster secundario opcional: prompt contextual ou estado de treino
- sem paines grandes permanentes no centro da tela

### `sprite-pipeline`

Traducao para pipeline de personagem em Unity:

- aprovar um frame seed por acao antes de gerar strip
- manter tamanho de frame fixo por familia de personagem
- usar ancora compartilhada, preferencialmente bottom-center
- revisar preview sheet antes de importar no jogo
- nao compensar drift de sprite no codigo quando o problema e do strip

Aplicar em:

- `Assets/ProjectPVP/Characters/*`
- `Assets/ProjectPVP/Scripts/Runtime/Characters/CharacterSpriteAnimator.cs`
- ferramentas de upgrade e normalizacao de sprite

### `game-playtest`

Traducao do fluxo browser para Unity:

- smoke pass manual com evidencias
- findings em ordem de severidade
- cada finding com: o que aconteceu, como reproduzir, impacto, dono provavel
- screenshots, gravacoes curtas e toggles de hitbox fazem parte do processo

### `three-webgl-game`

Nao usar como runtime guidance neste projeto. Regra portavel:

- view nunca vira fonte de verdade da simulacao

### `react-three-fiber-game`

Nao usar como runtime guidance neste projeto. Regra portavel:

- estado de HUD/menu nao deve controlar a simulacao por efeito colateral

### `web-3d-asset-pipeline`

Nao e prioridade agora. Regra portavel:

- nome, escala, pivots e convencoes de asset devem ser corrigidos no asset, nao no runtime

## Regras operacionais adotadas

1. Gameplay decide. Presentation mostra.
2. Input entra por `ICombatantInputSource` e sai como `PlayerInputFrame`.
3. HUD de combate deve proteger leitura de espacamento, altura e aproximacao.
4. Personagens precisam de identidade por timing, knockback, mobilidade e feedback, nao so por sprite.
5. Qualquer novo VFX, shake ou hit flash deve aumentar clareza antes de aumentar ruido.
6. Testes automatizados cobrem regra e configuracao. Playtest manual cobre leitura, impacto e sensacao.

## Focos imediatos

### Arquitetura

- continuar extraindo responsabilidade de `PlayerController`
- manter `Match`, `Input`, `Gameplay` e `Presentation` com ownership claro

### HUD

- evoluir o HUD do slice para modo compacto de combate
- manter debug detalhado atras de toggle

### Combate

- hitstun, knockback, hit-confirm e feedback de impacto
- diferenciar Mizu e Storm Dragon por parametros de acao e resposta

### Sprite pipeline

- validar consistencia de direcao, ancora, escala e fallback de animacao

### Playtest

- usar checklist fixa por rodada para comparar build contra build

