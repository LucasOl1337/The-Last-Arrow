# Patch Notes

Este arquivo deve ser atualizado de forma sequencial a cada versao nova.

Regra do projeto:
- a versao mais recente fica sempre no topo
- as versoes antigas continuam abaixo, sem apagar historico
- sempre que possivel, cada patch deve trazer imagens reais dos assets implementados naquela entrega

---

## v0.1.1 - 2026-03-15

### Destaques

- Movimentacao dos personagens refinada para deixar o combate mais consistente e responsivo.
- Colisao da `arrow` revisada para evitar comportamentos injustos e melhorar a leitura do hit real.
- Hitboxes de `melee` e `ultimate` dos dois personagens polidas com ajuste mais fino no gameplay.
- `ProjectileOrigin` ajustado para alinhar melhor disparo, sprite e sensacao de combate.
- Nova `ultimate` da Storm Dragon implementada no jogo.
- Nova `ultimate` da Mizu implementada no jogo com dash curto, bloqueio de flechas e repeticao da sombra.
- O ataque `melee` da Mizu agora consegue cortar flechas e inutiliza-las no meio do combate.
- Mapa e enquadramento melhorados com zoom mais coerente para a escala atual dos personagens.
- Animacoes de morte criadas e implementadas para os personagens jogaveis.

### Ferramentas e pipeline

- Pipeline com PixelLab MCP ampliado para importar e sincronizar animacoes novas com mais seguranca.
- Novas ferramentas no Unity para ajustar `SpawnAnchor`, `MeleeHitbox` e `UltimateHitbox` direto na cena.
- Fluxo de spawn melhorado para respeitar melhor a posicao configurada no mapa.

### Imagens deste patch

#### Mizu - Ultimate Red Afterimage

![Mizu Red Afterimage](Assets/ProjectPVP/Characters/Mizu/Animations/ult/east/frame_002.png)

#### Storm Dragon - Ultimate

![Storm Dragon Ultimate](Assets/ProjectPVP/Characters/StormDragon/Animations/ult/east/frame_002.png)

#### Storm Dragon - Death Animation

![Storm Dragon Death](Assets/ProjectPVP/Characters/StormDragon/Animations/death/east/frame_005.png)

#### Arena Atual

![Arena Atual](Assets/ProjectPVP/Environment/Backgrounds/Maps/background1.png)
