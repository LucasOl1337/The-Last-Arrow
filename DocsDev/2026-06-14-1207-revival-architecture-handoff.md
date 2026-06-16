# The Last Arrow - Estado de Trabalho e Handoff

Data/hora local: 2026-06-14 12:07:14 -03:00

## Objetivo central trabalhado

Avaliar adversarialmente o estado arquitetural do projeto `The Last Arrow` e decidir qual caminho tem maior chance de sucesso para reviver o jogo:

1. Manter a estrutura atual e corrigir/refatorar pontos criticos.
2. Rebuildar do zero ou quase do zero, preservando logica desejada, objetivos declarados e assets.

O foco foi decidir a estrategia tecnica antes de implementar novas features.

## Decisoes tomadas

- Decisao final por votacao multiagente: **Hibrido com vies incremental**.
- Placar final: **4/4 agentes votaram Hibrido**.
- Notas de sucesso provavel: entre **7/10 e 8/10**.
- Interpretacao pratica:
  - Nao fazer rebuild total.
  - Nao seguir com fixes soltos em cima dos god objects atuais.
  - Preservar assets, cena, ScriptableObjects, contratos e sistemas uteis.
  - Reconstruir seletivamente as cascas ruins de orquestracao.

## Alteracoes feitas

- Nenhuma alteracao em codigo-fonte Unity ou Python foi feita ate este ponto.
- Foram executadas analises, buscas e testes.
- Foi criada a pasta `DocsDev`.
- Foi criado este arquivo de handoff.
- Apos o handoff inicial, `.codegraph/` foi adicionado ao `.gitignore` como artefato local pesado de ferramenta.
- Primeiro patch tecnico aplicado: `PlayerCombatSystem.ApplyUltimateDamageHits` voltou a aplicar `hitstun` e `knockback` no alvo em vez de chamar `Kill()` diretamente.
- Foi adicionado `PlayerCombatSystemTests.cs` para cobrir a regressao do ultimate: deve aplicar hitstun/knockback sem matar diretamente.

Verificacoes executadas:

- `python -m pytest tools\tests -q`: passou com `7 passed`.
- `python -m compileall -q mainbot.py tools`: passou.
- Unity EditMode foi tentado com Unity `6000.3.11f1`, mas bloqueou por falta de licenca valida no ambiente. Nao houve XML de resultado.
- Apos o patch de combate, Unity EditMode foi tentado novamente e continuou bloqueado por licenca (`No valid Unity Editor license found`, return code 198 no log). Nao houve XML de resultado.

## Estado atual do repositorio

Antes deste arquivo, a worktree ja estava suja:

- `changelog.md`, `patchnotes.md` e `grokimaginevideos/README.md` modificados.
- Muitas delecoes em `grokassets/...`.
- `.codegraph/` nao rastreado.
- Depois da primeira higiene operacional, `.codegraph/` passou a estar coberto pelo `.gitignore`.

Essas mudancas nao foram feitas nesta analise e nao devem ser revertidas automaticamente.

## Contexto consolidado da pasta Docs

Existe uma pasta `Docs` com documentacao importante. Pontos essenciais consolidados aqui:

- `Docs/Git-Worktree-Workflow.md`: o projeto e um monorepo Unity; recomenda worktrees por feature/fix/spike; descreve ownership de assemblies.
- `Docs/Game-Studio-Unity-Translation.md`: Unity e o simulador autoritativo; input entra por `ICombatantInputSource` e sai como `PlayerInputFrame`; HUD nao deve competir com leitura do combate.
- `Docs/Combat-Playtest-Checklist.md`: checklist manual de smoke, leitura de combate, impacto, HUD, movimento, identidade de personagem e regressao de round/input.
- `Docs/AI-Arena-Agent-Request.md`: IA deve entrar por `ICombatantInputSource`; Unity continua autoritativo; estado deve virar camada semantica de combate; testar timeout, reset/respawn, parse invalido e regressao de input humano.
- `Docs/PixelLab-MCP-Workflow.md`: fluxo de assets/personagens via PixelLab; Mizu ja tem configuracao relacionada.
- `INPUT_SOURCE_OF_TRUTH.txt`: referencia canonica de input; sem excecoes por personagem; normalizacoes de controle devem ficar centralizadas.
- `PHYSICS_MECHANICS_ANALYSIS.md`: documenta normalizacao/tuning de fisica, mas ha indico de drift com assets atuais.

## Achados arquiteturais principais

Pontos preservaveis:

- Assemblies ja separados: `Core`, `Data`, `Input`, `Gameplay`, `Characters`, `Match`, `Presentation`.
- Cena principal `Assets/ProjectPVP/Scenes/Bootstrap.unity` ja integra Mizu, StormDragon, match, HUD/debug e arena.
- `CharacterDefinition`, `CharacterBootstrapProfile`, `CharacterCatalog` e assets de personagens tem valor real.
- Contrato de input existe: `ICombatantInputSource` e `PlayerInputFrame`.
- Sistemas de gameplay ja foram parcialmente extraidos: movimento, colisao, jump, dash, action lock, anchors e combate.
- Stack Python de bots ja existe: `mainbot.py`, `tools/codex_broker.py`, `tools/codex_live_agent.py`, `tools/bot_manager.py`, `tools/codex_memory.py`.

Pontos problematicos:

- `MatchController.cs` mistura round flow, respawn, score, audio, HUD, debug shortcuts, bot assignment, leitura de JSON e bootstrap global.
- `PlayerController.cs` ainda compoe input, AI, broker Codex, audio e fallback runtime.
- `KeyboardPlayerInputSource.cs` e monolitico e concentra Legacy Input, gamepad, D-pad quirks e debug.
- `PlayerCombatSystem.cs` tem migracao incompleta: melee usa hitstun/knockback, mas projectile, ultimate e stomp ainda podem matar diretamente.
- `AiArenaRuntimeSnapshotCollector.cs` usa `FindObjectsByType`, nomes de tipo e reflection para ler estado.
- Testes Unity usam reflection em privados e alguns parecem presos a nomes antigos que hoje so aparecem no `.bak`.
- `PlayerController.cs.bak` dentro de `Assets/ProjectPVP/Scripts/Runtime/Gameplay` e ruido operacional.

## Pendencias

- Resolver execucao/licenca Unity para poder rodar testes EditMode/PlayMode.
- Decidir o que fazer com as mudancas pendentes existentes em `grokassets` e docs.
- Adicionar `.codegraph/` ao `.gitignore` ou mover para fora do repo.
- Remover/arquivar `PlayerController.cs.bak` com cuidado.
- Atualizar docs que estao em drift com assets atuais.
- Corrigir testes Unity obsoletos.
- Revalidar tuning real de Mizu e StormDragon.
- Definir regra unica de dano/hit/kill para melee, projectile, ultimate e stomp.

## Proximos passos recomendados

1. **Higiene operacional**
   - Registrar/entender o estado sujo atual.
   - Ignorar ou remover `.codegraph/`.
   - Separar o que e asset marketing removido de mudanca relevante ao jogo.

2. **Restaurar verificabilidade**
   - Fazer Unity rodar testes.
   - Corrigir testes que dependem de reflection/membros privados antigos.
   - Criar testes para round flow, input e regra de combate via APIs publicas/servicos puros.

3. **Extrair MatchController**
   - Criar `RoundFlowService`.
   - Criar `RespawnService`.
   - Criar `RuntimeBotAssignmentService`.
   - Mover HUD/audio/debug para presentation/composicao apropriada.

4. **Unificar combate**
   - Introduzir uma entrada unica de resolucao de hit.
   - Remover divergencia entre melee/projetil/ultimate/stomp.
   - Aplicar `hitstun`, `knockback` e `Kill` por regra explicita.

5. **Refatorar input e bots**
   - Manter `PlayerInputFrame`.
   - Dividir provider Legacy/gamepad.
   - Trocar snapshot de AI baseado em reflection por interfaces explicitas.
   - Endurecer broker: limite de payload, escrita atomica, rotacao/scrub de logs.

## Riscos, bugs e inconsistencias

- Rebuild total tem risco alto de perder wiring Unity serializado, feel de movimento, hitboxes, assets, animacoes, input edge cases e fluxo de bots.
- Incremental puro tem risco alto de perpetuar god objects.
- Unity tests nao puderam confirmar estado real por falta de licenca.
- Drift entre `PHYSICS_MECHANICS_ANALYSIS.md` e assets atuais: documentacao sugere StormDragon retunado, mas assets vistos indicam stats muito parecidos com Mizu.
- Campos novos de combat feel em `CharacterDefinition` parecem nao estar serializados explicitamente nos assets atuais.
- `ProjectSettings` tem `runInBackground: 0`, ruim para playtest/bots se a janela perder foco.
- Stack Python registra prompts/respostas/stdout/stderr em traces; apesar de `tools/bot_memory/` estar ignorado, precisa scrub/rotacao.
- Broker local nao tem auth; seguro apenas enquanto restrito a `127.0.0.1`.

## Contexto essencial para outro agente

- Instrucoes do usuario/projeto:
  - O usuario pode estar usando transcricao de audio; interpretar palavras estranhas pelo contexto.
  - Quando algo puder ser feito autonomamente, executar sem pedir comandos manuais.
  - Nao usar Playwright; preferir Chrome/Codex app quando browser for necessario.
- Nao reverter mudancas existentes que nao foram feitas por este agente.
- A decisao tecnica aprovada e **Hibrido com vies incremental**.
- O trabalho seguinte deve ser implementado em fatias pequenas e verificaveis.
- Nao comecar por feature nova. Comecar por higiene, testes, combate e extracoes de orquestracao.
- Preservar assets/cena/dados/protocolo de bots; reconstruir seletivamente controladores e bordas instaveis.
