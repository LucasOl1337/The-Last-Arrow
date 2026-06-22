# Auditoria comparativa: The Last Arrow x TowerFall Ascension

Data: 18/06/2026  
Escopo: comparar o estado atual do projeto com um alvo de paridade funcional inspirado em TowerFall Ascension, cobrindo telas, lógica de jogo, evidência visual e testes. Esta análise não recomenda copiar marcas, assets ou conteúdo protegido; "clone" aqui significa paridade de experiência/sistemas.

## Fontes usadas

- Site oficial: https://www.towerfall-game.com/
- Steam: https://store.steampowered.com/app/251470/TowerFall_Ascension/
- Steam launch/news: https://store.steampowered.com/oldnews/12615

## Evidências visuais salvas

- `screenshots/towerfall-official-home.png`: screenshot da página oficial.
- `screenshots/towerfall-steam-page.png`: screenshot da página Steam.
- `screenshots/the-last-arrow-current-arena.png`: arena atual versionada no projeto.
- `screenshots/the-last-arrow-mizu-ult-frame.png`: frame real da personagem Mizu.
- `screenshots/the-last-arrow-stormdragon-aim-frame.png`: frame real da Storm Dragon.
- `screenshots/the-last-arrow-crystal-arrow.png`: sprite real da flecha.
- `visual-matrix.html`: matriz visual local da comparação. O Chrome bloqueou `file://`, então não foi gerado screenshot automático dessa página.

## Veredito

The Last Arrow ainda não é um clone de TowerFall Ascension. Hoje ele é um protótipo forte de duelo 2D com arco/flecha, bots, round flow, HUD básico de rounds e alguns sistemas de combate que lembram o núcleo de TowerFall. O que falta é o volume e a arquitetura de produto: 4 jogadores, seleção de arenas, variantes, baús, power-ups, tipos de flecha, Quest, Trials, replay final real, metaprogressão e fluxo completo de menus.

Estimativa objetiva:

- Cobertura de telas: baixa. Existe um menu inicial/jogadores e HUD de partida, mas a maior parte do fluxo de TowerFall Ascension está ausente.
- Cobertura de lógica versus: média. Movimento, tiro, flechas recuperáveis, dash/parry, stomp, round reset e placar existem em algum nível.
- Cobertura de TowerFall Ascension completo: baixa. Quest, Trials, 4P, variantes, mapas, power-ups e conteúdo persistente ainda não existem.

## Alvo de referência TowerFall Ascension

Pelos materiais oficiais, TowerFall é um jogo local de combate com arco em arenas, até 4 jogadores, flechas limitadas, recuperação de flechas, baús/power-ups, tipos especiais de flecha, shields, wings/artifacts, dodge/catch de flechas e replay instantâneo da kill final. A página Steam também descreve Quest para 1-2 jogadores, 120 mapas, 8 arqueiros, 75 variantes e segredos desbloqueáveis.

## Inventário atual do The Last Arrow

### Telas/UI presentes

- Menu overlay em runtime com `THE LAST ARROW`, subtítulo `ARROWFALL DUEL`, seletor de modo, dois painéis de slot/personagem/AI e botão START em `Assets/ProjectPVP/Scripts/Runtime/Match/ProjectPvpAscensionMenuOverlay.cs`.
- Modos internos: `Versus`, `HumanVsAi`, `AiArena` em `Assets/ProjectPVP/Scripts/Runtime/Match/ProjectPvpMenuSelection.cs`.
- HUD de round com pontos de vitória, winner banner, marcador/label de final kill e painel `BOT COACH` em `Assets/ProjectPVP/Scripts/Runtime/Match/ProjectPvpMatchRoundHudOverlay.cs`.

### Limites estruturais atuais

- Slots: `CombatantSlotId` só define `SlotOne` e `SlotTwo`.
- Personagens: `CharacterCatalog.asset` aponta para 2 entradas: Mizu e Storm Dragon.
- Arena: existe uma arena default em `Assets/ProjectPVP/Environment/Arenas/DefaultArenaDefinition.asset`; o asset de arena modela nome, background, música, spawn points e wrap bounds, sem lista de hazards/baús/power-ups/tileset/variantes.
- Cena jogável: o validador abriu `Assets/ProjectPVP/Scenes/Bootstrap.unity`, mas falhou porque os dois slots estão sem `ICombatantInputSource` configurado.

### Lógica presente

- Round flow: first-to-5 (`maxWins = 5`), campeão da série, reset de rounds, seeds de respawn, corpse arrows e auto balance em `MatchController`.
- Combate: tiro 8-direções, flechas com limite de munição, melee, cortar projéteis com melee, hit de projétil, coleta de flecha, parry/reflexão, ultimate, shield e stomp.
- Movimento: jump, wall jump/slide, dash e invulnerabilidade/parry-window via timers.
- Bot/AiArena: cobertura considerável para snapshots, policies e broker de IA.

## Matriz de telas

| Tela/fluxo TowerFall | Estado atual | Gap para clone |
| --- | --- | --- |
| Title/Main menu | Parcial | Existe overlay com marca e START, mas não há fluxo completo de menus de produto. |
| Mode select | Parcial | Só `Versus`, `HumanVsAi`, `AiArena`. Faltam Quest, Trials, opções e variantes como fluxo principal. |
| Player/character select | Parcial | Só 2 slots e 2 personagens. TowerFall exige até 4 jogadores e roster maior. |
| Team/color/player join | Ausente | Não há suporte genérico de 4 jogadores, times ou join/ready flow. |
| Match settings | Ausente | Sem tela para lives, score, teams, treasure, variants, random, arena rules. |
| Variant selection | Ausente | Sem sistema visual/lógico de variantes estilo TowerFall. |
| Arena select | Ausente | Há uma arena default, mas não há catálogo navegável nem seleção de mapa. |
| Gameplay HUD | Parcial | Pontos de vitória e final kill label existem; falta HUD completo de munição, quiver, player cards, awards e legibilidade final. |
| Pause/options | Ausente | Não há tela final de pause, remap, volume, vídeo, controles. |
| Round result | Parcial | Há winner banner/label, mas sem replay instantâneo real nem awards pós-round. |
| Match/champion result | Parcial | Há reset de série e anúncio, mas falta tela de resultados/metas/estatísticas. |
| Quest menu | Ausente | Sem campanha/co-op PVE. |
| Trials menu | Ausente | Sem trials/time attack/medalhas. |
| Unlocks/secrets | Ausente | Sem metaprogressão, secretos ou biblioteca de unlocks. |
| Credits/extras | Ausente | Fora do fluxo atual. |

## Matriz de lógica

| Sistema TowerFall | Estado atual | Gap para clone |
| --- | --- | --- |
| Local multiplayer 4P | Ausente | Código e UI são centrados em 2 slots. Precisa generalizar slots, spawn, HUD, input e scoring para 4. |
| Versus 2P | Parcial/forte | Base de duelo existe, mas precisa tuning de feel, regras e fluxo completo. |
| Teams | Ausente | Sem times, friendly fire, paleta/equipe ou round scoring por time. |
| Movimento de plataforma | Parcial/forte | Jump/wall/dash existem; precisa calibrar contra TowerFall e cobrir edge cases de arena. |
| Mira e tiro | Parcial/forte | Tiro 8-dir e munição existem; precisa comportamento exato de charge/aim/cancel/recover. |
| Flechas recuperáveis | Parcial | Coleta de projéteis stuck/disarmed existe; precisa alinhar com quiver, pickups e regras de corpse/arena. |
| Dodge/catch | Parcial | Há invulnerabilidade/parry/reflexão; TowerFall tem catch/dodge com timing próprio. |
| Stomp | Presente | Head stomp mata, mas precisa regras exatas de colisão/prioridade. |
| Shields | Parcial | Shield existe e autobalance concede shield; falta treasure pickup e estado visual/UI completo. |
| Baús/treasure | Ausente | Não há sistema de chest spawn, loot table ou item pickup. |
| Tipos especiais de flecha | Ausente | Sem Drill/Bomb/Laser/Bramble/etc. Só flecha base/config por personagem. |
| Wings/artifacts | Ausente | Sem power-ups persistentes de rodada. |
| Variants | Ausente | Sem sistema de rule modifiers selecionáveis. |
| Arenas em volume | Ausente | Só uma arena default. TowerFall Ascension anuncia 120 mapas. |
| Hazards/interativos | Ausente | ArenaDefinition não modela hazards/tiles/events. |
| Quest/PVE | Ausente | Sem inimigos, waves, boss/co-op objective. |
| Trials | Ausente | Sem pontuação, medalhas, tempo/alvos/desafios. |
| Replay final | Parcial baixo | HUD mostra final kill; há replay ligado a ultimate da Mizu, não replay instantâneo final da rodada. |
| Bots | Extra/parcial | Bom para teste e AI Arena, mas não substitui paridade local 4P. |
| Persistência/stats/achievements | Ausente | Sem unlocks, estatísticas, cloud/achievements. |

## Testes e validação

### Inventário estático

Foram encontrados 48 arquivos de teste editoriais, 471 marcadores `[Test]`/`[UnityTest]` e 2604 chamadas `Assert.`. As maiores áreas cobertas são:

- `AiArenaInputSourceTests.cs`: 109 testes.
- `PlayerCombatSystemTests.cs`: 69 testes.
- `CodexBrokerStateMapperTests.cs`: 53 testes.
- `MatchControllerRoundFlowTests.cs`: 28 testes.
- `AiArenaSemanticObservationBuilderTests.cs`: 20 testes.
- `ProjectileGravityTests.cs`: 16 testes.

Isso mostra boa cobertura unitária de IA, combate, round flow e projéteis. Não mostra cobertura de produto completo.

### Execução Unity

O projeto original em `C:\Projetos\The-Last-Arrow` falhou em batch com erro de ambiente do Unity sobre filesystem/read-only/case-sensitive. Para isolar, foi criada uma cópia temporária em `C:\Temp\The-Last-Arrow-Codex-Audit`.

Resultados na cópia temporária:

- `ProjectPvpPlayableValidator.ValidatePlayableSlice` carregou a cena Bootstrap e falhou corretamente:
  - `Slot 1 sem ICombatantInputSource configurado.`
  - `Slot 2 sem ICombatantInputSource configurado.`
- O Unity Test Runner recebeu `-runTests -testPlatform editmode -testResults ...`, importou/compilou o projeto e saiu com código 0, mas não gerou XML nem registrou início/fim de suite. Portanto, não há resultado confiável de "todos os testes passaram" nesta auditoria.

### Gaps de teste

- Sem screenshot/golden tests por tela.
- Sem PlayMode end-to-end cobrindo menu -> seleção -> partida -> round reset -> campeão.
- Sem testes de 4 jogadores.
- Sem testes de arena select, variantes, baús, power-ups e tipos especiais de flecha.
- Sem testes de Quest/Trials.
- Sem teste visual de HUD final/replay instantâneo.

## Backlog para virar clone funcional

### P0 - Base obrigatória

1. Corrigir a cena Bootstrap para passar no `ProjectPvpPlayableValidator`: cada slot jogável precisa de `ICombatantInputSource`.
2. Generalizar `CombatantSlotId`, roster, HUD, spawn, round flow e input de 2 slots para até 4 slots.
3. Criar fluxo de telas de produto: title/main, mode select, player join, character select, arena select, match settings, pause e results.
4. Criar `ArenaCatalog` e selector com múltiplas arenas reais.
5. Separar regra de partida em configuração serializada: max wins, teams, arrows, treasure, variants, hazards e score.
6. Medir e tunar movimento/combat feel contra o alvo: tempos de dash/catch, queda, recuperação, hitboxes, pickup e round freeze.

### P1 - Versus Ascension-like

1. Implementar 4P local completo.
2. Implementar variants como sistema de modifiers plugáveis.
3. Implementar baús/treasure com loot table.
4. Implementar tipos especiais de flecha.
5. Implementar power-ups como shield, wings e artifacts.
6. Implementar hazards/interativos de arena.
7. Implementar replay instantâneo de final kill.
8. Expandir roster para pelo menos 8 personagens/archetypes e skins/cores.

### P2 - Conteúdo completo

1. Quest 1-2P: enemies, waves, objectives, boss/arena progression.
2. Trials: desafios, timers, medalhas e leaderboard local.
3. Unlocks, segredos, estatísticas e achievements.
4. Polimento de áudio/VFX/camera shake/UI para leitura competitiva.
5. Matrix de testes visuais e PlayMode para 100% das telas.

## Definition of done para "clone funcional"

- Todas as telas da matriz acima existem e têm teste de navegação.
- Partida local suporta 2-4 jogadores humanos com input real.
- Versus possui seleção de personagem, arena, variantes e regras.
- Flechas limitadas, recuperação, dodge/catch, shields, treasure, power-ups e tipos especiais funcionam em PlayMode.
- Replay final mostra a kill decisiva, não só um label.
- Quest e Trials são selecionáveis e jogáveis.
- Há testes automáticos para menu, gameplay, round flow, conteúdo de arena, variantes e PVE.
- O validador de playable slice passa em batch.

