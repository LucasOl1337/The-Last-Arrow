# The Last Arrow - Handoff da Sessao CodeGraphy / Manutencao

Data/hora local: 2026-06-15 06:49:54 -03:00

## Tema principal

Rodada longa de manutencao do repositorio `The-Last-Arrow`, com foco em:

- usar o CodeGraphy como apoio para investigacao arquitetural;
- reduzir bugs criticos herdados de codigo gerado por modelos antigos;
- melhorar performance e robustez dos fluxos de gameplay, bots e ferramentas Python;
- registrar o estado atual de forma clara para continuidade por outro agente.

## Objetivo central discutido

O objetivo do trabalho foi reviver e estabilizar o projeto sem fazer um rebuild total. A direcao adotada foi uma estrategia hibrida com vies incremental:

- preservar cena, assets, ScriptableObjects, protocolos e sistemas ja uteis;
- corrigir bugs de alto impacto e arestas de runtime;
- reduzir scans globais e reflection em caminhos quentes;
- endurecer persistencia/logs/ferramentas dos bots;
- documentar o estado para que proximas fatias possam extrair servicos maiores com menor risco.

## Estrutura de documentacao

`DocsDev` ja existia e foi usado como destino principal.

Tambem existe a pasta forte `Docs/`. Como solicitado, documentos importantes foram copiados para dentro de `DocsDev`, sem apagar os originais:

- `DocsDev/ImportedDocs/Docs/AI-Arena-Agent-Request.md`
- `DocsDev/ImportedDocs/Docs/Combat-Playtest-Checklist.md`
- `DocsDev/ImportedDocs/Docs/Game-Studio-Unity-Translation.md`
- `DocsDev/ImportedDocs/Docs/Git-Worktree-Workflow.md`
- `DocsDev/ImportedDocs/Docs/PixelLab-MCP-Workflow.md`
- `DocsDev/ImportedDocs/Docs/ReleaseNotes-v0.1.1.md`

Docs raiz importantes tambem foram copiadas para `DocsDev/ImportedDocs/Root/`:

- `README.md`
- `DEVELOPMENT_GUIDE.md`
- `GAMEPLAY_STRATEGY.md`
- `INPUT_SOURCE_OF_TRUTH.txt`
- `PHASE1_IMPLEMENTATION.md`
- `PHASE1_READY.md`
- `PHYSICS_MECHANICS_ANALYSIS.md`
- `SETUP_GUIDE.md`

Arquivos de handoff/nota ja existentes em `DocsDev`:

- `DocsDev/2026-06-14-1207-revival-architecture-handoff.md`
- `DocsDev/2026-06-14-1242-maintenance-round-codegraphy.md`
- este arquivo: `DocsDev/2026-06-15-0649-session-handoff-codegraphy-maintenance.md`

## Decisoes e planejamento

- Decisao tecnica principal: nao rebuildar tudo agora; seguir com refatoracao incremental guiada por testes e CodeGraphy.
- CodeGraphy foi tratado como artefato local pesado, sincronizado ao longo da rodada e ignorado pelo Git via `.gitignore`.
- O `MatchController` foi mantido por enquanto, mas segue como alvo claro de extracao futura.
- O fluxo de combate foi movido para uma regra mais consistente: hits geram reacao; morte deve vir de ring-out/hazards/regras explicitas.
- A stack de bots continuou sendo preservada, mas com hardening de payload, persistencia, traces e edge cases de runtime.
- Unity EditMode/PlayMode continua bloqueado por licenca, entao as mudancas C# estao cobertas por testes escritos, mas nao executadas localmente no Unity.

## Implementado ou alterado

### CodeGraphy

- Adicionado `tools/codegraphy_report.py`.
- O relatorio consulta `.codegraph/codegraph.db` e imprime Markdown ou JSON com:
  - contagens de files/nodes/edges;
  - frescor do indice;
  - unresolved refs;
  - top arquivos por node count;
  - top nodes por grau;
  - classificacao de unresolved refs entre ruido externo/provavel e potenciais refs de projeto.
- Testes adicionados em `tools/tests/test_codegraphy_report.py`.
- Ultimo status observado:
  - files: `118`
  - nodes: `3745`
  - edges: `6832`
  - pendingChanges: `added 0`, `modified 0`, `removed 0`
  - unresolved refs brutos: `2760`
  - likely external/noisy refs: `2380`
  - potential project refs: `380`

### Combate e round flow

- `PlayerCombatSystem.HandleIncomingProjectile` deixou de matar diretamente.
- Projectile hit agora aplica hitstun/knockback.
- Ultimate hit foi mantido como hitstun/knockback em vez de kill direto.
- `PlayerJumpSystem.TryCheckHeadStomp` deixou de matar diretamente e preserva o bounce.
- `MatchController` ganhou `verticalRingOutEnabled` ligado por padrao.
- Queda abaixo de `wrapBounds.yMin - wrapPadding.y` agora mata por ring-out inferior.
- Wrap horizontal continua ativo.
- `PlayerCombatSystem.Kill()` passou a retornar `bool`.
- `PlayerController.TryKill()` foi adicionado para tornar morte idempotente.
- `PlayerController.Kill()` delega para `TryKill()`, evitando multiplas notificacoes `Died` para o mesmo estado morto.
- Getters publicos de runtime do `PlayerController` passaram a retornar defaults seguros antes de `Awake()`.

### Bots, input e IA

- `AiArenaRuntimeSnapshotCollector` agora prefere interfaces explicitas:
  - `IAiArenaControllerSnapshotSource`
  - `IAiArenaProjectileSnapshotSource`
  - `IAiArenaArenaSnapshotSource`
- `PlayerController`, `ProjectileController` e `MatchController` implementam fontes tipadas de snapshot.
- `AiArenaSnapshotSourceRegistry` foi adicionada para evitar `FindObjectsByType` no caminho normal.
- Registries ativas tambem foram adicionadas em `PlayerController` e `ProjectileController`.
- `PlayerCombatSystem.ResolveProjectileAssistTarget`, `PlayerJumpSystem.TryCheckHeadStomp`, `ProjectPvpCombatDebugGizmos` e `DebugAimOverlay` passam a preferir registries antes de scans globais.
- `ProjectileController.BuildAiArenaProjectileSnapshot` agora marca projeteis nao lancados como invalidos.
- `CodexBrokerCombatantInputSource` agora versiona requests de session start e strategy/state.
- Callbacks atrasados sao ignorados quando watchdog ou troca de slot invalidam a request ativa.
- `ConfigureForSlot` limpa `sessionId` quando o slot muda.
- Modo broker direto (`useAgentDrivenMode=false`) agora aceita intent executavel mesmo quando envelope legado nao traz `hasAgentAction`.
- `tools/codex_broker.py` agora marca snapshots diretos com `hasAgentAction: true` e `controllerOwner: CodexDirect`.
- `AiArenaFrameExecutor` agora retorna frame neutro quando `roundResetPending` esta ativo, evitando que intents antigas atirem, deem melee, dash ou jump durante reset de round.

### Runtime bot menu e slot 2

- Arquivo runtime de menu de bots agora conta como escolha explicita mesmo quando todos os slots estao desabilitados.
- Isso evita cair no fallback que forca slot 2 como Codex bot quando o usuario/menu escolheu nenhum bot.
- `Start`, bootstrap e respawn do `MatchController` agora usam automacao configurada.
- `autoEnableSlotTwoDebugBotOnPlay` e `autoForceCodexBrokerForSlotTwoOnPlay` passaram a ser respeitados quando nao ha arquivo runtime.
- Metodos publicos `ForceSlotTwoCodexBotReadyForPlay` e `ForceCodexBotsReadyForPlay` continuam com semantica explicita de forcar.
- Reaplicar o bot debug do slot 2 nao sobrescreve mais `_slotTwoOriginalProfile`.
- Desligar o bot via shortcut deve voltar ao mesmo perfil original, em vez de restaurar um override AI intermediario.

### Python tools e persistencia

- `tools/codex_broker.py`:
  - valida `Content-Length`;
  - rejeita body acima de `CODEX_BROKER_MAX_REQUEST_BYTES`, default `1 MB`;
  - centraliza decode de JSON objeto;
  - agora marca snapshots diretos como executaveis.
- `tools/bot_manager.py`:
  - usa escrita atomica para JSON/Markdown persistentes;
  - protege `roster.json`, runtime assignments e conhecimento global contra escrita parcial.
- `tools/codex_memory.py`:
  - reutiliza escrita atomica do bot manager para perfil privado e relatorios/planos Markdown;
  - garante criacao do diretorio pai antes de anexar JSONL.
- `tools/codex_trace_store.py`:
  - rotaciona `trace_events.jsonl`;
  - sanitiza chaves sensiveis e tokens;
  - trunca strings/listas/payloads profundos.

### Qualidade de vida

- `ProjectSettings/ProjectSettings.asset`: `runInBackground` alterado para `1`.
- `KeyboardPlayerInputSource` agora resolve gamepads por familia usando indice dentro dos controles que batem com a familia.
- Isso reduz chance de dois slots pegarem o mesmo primeiro gamepad quando usam a mesma familia preferida.

## Testes e verificacoes realizadas

Passou:

- `python -m pytest tools\tests -q` -> `17 passed`
- `python -m compileall -q mainbot.py tools`
- `git diff --check` nos arquivos alterados nas fatias finais
- `codegraph sync .`
- `codegraph status --json .` -> pendingChanges zerado
- `python tools\codegraphy_report.py --limit 8`

Bloqueado:

- Unity EditMode batchmode foi tentado varias vezes com Unity `6000.3.11f1`.
- Nao foi gerado `Logs/codex-editmode-results.xml`.
- `Logs/codex-editmode.log` termina com:
  - `No valid Unity Editor license found. Please activate your license.`
  - return code reportado pelo Unity no log: `198`
- Por isso, os testes C# adicionados ainda nao foram executados localmente nesta sessao.

Observacao:

- `git diff --check` mostrou um aviso de line ending em `tools/codex_broker.py`: `LF will be replaced by CRLF the next time Git touches it`.
- O aviso nao indicou erro de whitespace.

## Arquivos principais tocados

Runtime Unity:

- `Assets/ProjectPVP/Scripts/Runtime/Gameplay/PlayerCombatSystem.cs`
- `Assets/ProjectPVP/Scripts/Runtime/Gameplay/PlayerController.cs`
- `Assets/ProjectPVP/Scripts/Runtime/Gameplay/PlayerJumpSystem.cs`
- `Assets/ProjectPVP/Scripts/Runtime/Gameplay/ProjectileController.cs`
- `Assets/ProjectPVP/Scripts/Runtime/Input/AiArenaFrameExecutor.cs`
- `Assets/ProjectPVP/Scripts/Runtime/Input/AiArenaRuntimeSnapshotCollector.cs`
- `Assets/ProjectPVP/Scripts/Runtime/Input/CodexBrokerCombatantInputSource.cs`
- `Assets/ProjectPVP/Scripts/Runtime/Input/KeyboardPlayerInputSource.cs`
- `Assets/ProjectPVP/Scripts/Runtime/Match/MatchController.cs`
- `Assets/ProjectPVP/Scripts/Runtime/Presentation/ProjectPvpCombatDebugGizmos.cs`
- `Assets/ProjectPVP/Scripts/Runtime/Gameplay/DebugAimOverlay.cs`

Testes Unity adicionados/alterados:

- `Assets/ProjectPVP/Tests/Editor/PlayerCombatSystemTests.cs`
- `Assets/ProjectPVP/Tests/Editor/AiArenaInputSourceTests.cs`
- `Assets/ProjectPVP/Tests/Editor/MatchControllerRoundFlowTests.cs`

Python/tools:

- `tools/codegraphy_report.py`
- `tools/codex_broker.py`
- `tools/bot_manager.py`
- `tools/codex_memory.py`
- `tools/codex_trace_store.py`
- `tools/tests/test_codegraphy_report.py`
- `tools/tests/test_codex_broker.py`
- `tools/tests/test_bot_manager.py`
- `tools/tests/test_codex_memory.py`
- `tools/tests/test_codex_trace_store.py`

Configuracao/documentacao:

- `.gitignore`
- `ProjectSettings/ProjectSettings.asset`
- `DocsDev/2026-06-14-1207-revival-architecture-handoff.md`
- `DocsDev/2026-06-14-1242-maintenance-round-codegraphy.md`
- `DocsDev/ImportedDocs/...`
- `DocsDev/2026-06-15-0649-session-handoff-codegraphy-maintenance.md`

## Pendencias

- Resolver a licenca Unity para rodar EditMode/PlayMode.
- Validar compilacao C# real no Unity.
- Validar Play Mode com:
  - ring-out inferior;
  - runtime bot menu gravando `tools/bot_runtime_assignments.json`;
  - shortcut de ligar/desligar bot do slot 2;
  - broker real em `useAgentDrivenMode=true`;
  - broker direto em `useAgentDrivenMode=false`;
  - dois controles fisicos com preferencias Xbox/DualSense.
- Medir em profiler o impacto real das registries contra cenas com muitos objetos.
- Extrair `RoundFlowService`, `RespawnService` e `RuntimeBotAssignmentService` de `MatchController`.
- Decidir remocao/arquivo de `PlayerController.cs.bak` e `PlayerControllerRefactored.cs`.
- Revisar docs importadas para evitar duplicidade ou drift apos a copia para `DocsDev`.
- Usar o relatorio de unresolved refs para distinguir limitacao do parser de acoplamento real.

## Riscos e pontos de atencao

- O estado C# esta sem validacao de compilacao porque Unity esta bloqueado por licenca.
- Muitos testes Unity usam reflection em metodos privados; isso ajuda a cobrir bugs agora, mas indica que futuras extracoes devem criar APIs/servicos testaveis.
- `MatchController` ainda concentra round flow, respawn, audio, HUD, debug shortcuts, leitura JSON e bot assignment.
- `PlayerController` ainda e denso e segue como centro de acoplamento.
- O total bruto de unresolved refs do CodeGraphy segue alto, mas parte relevante e ruido de Unity/NUnit/Python builtins.
- A protecao de frame neutro em `roundResetPending` deve ser validada em Play Mode para confirmar que nao atrapalha feedback visual ou freeze/respawn.
- O trace store agora sanitiza logs novos, mas arquivos antigos em `tools/bot_memory/traces/` nao foram migrados ou apagados.
- Docs antigas copiadas para `DocsDev` podem estar em drift com o estado atual do jogo.
- O estado sujo preexistente em `grokassets`, `patchnotes.md`, `changelog.md` e `grokimaginevideos/README.md` nao foi revertido nem organizado.

## Estado da worktree e cuidado com mudancas existentes

Antes e durante esta rodada a worktree ja estava suja. Nao reverter automaticamente:

- muitas delecoes em `grokassets/...`;
- `changelog.md`, `patchnotes.md`, `grokimaginevideos/README.md` modificados;
- arquivos novos em `DocsDev/`;
- testes novos em `Assets/ProjectPVP/Tests/Editor/`;
- ferramentas/testes Python novos em `tools/`.

Ao continuar, separar claramente:

- mudancas da rodada de manutencao/codegraphy;
- mudancas preexistentes de assets/docs;
- novos arquivos de handoff/importacao em `DocsDev`.

## Proximos passos recomendados

1. Corrigir/ativar licenca Unity e rodar:
   - EditMode tests;
   - PlayMode smoke;
   - validacao manual da cena `Assets/ProjectPVP/Scenes/Bootstrap.unity`.
2. Fazer uma rodada de compile/fix C# se Unity apontar erros.
3. Validar manualmente o novo modelo de combate:
   - projectile/ultimate/head stomp reagem sem kill direto;
   - ring-out inferior mata;
   - round reset pontua corretamente.
4. Validar o menu runtime de bots:
   - todos slots desabilitados nao devem reativar slot 2;
   - flags auto do slot 2 devem ser respeitadas;
   - desligar bot deve restaurar perfil humano original.
5. Validar broker:
   - agent-driven com agente real;
   - broker direto sem `hasAgentAction`;
   - round reset sem input ofensivo stale.
6. Extrair `RoundFlowService` e `RuntimeBotAssignmentService` para reduzir o tamanho do `MatchController`.
7. Revisar `DocsDev/ImportedDocs` e decidir se a pasta `Docs` original deve continuar existindo como espelho ou ser tratada como legado.

## Informacoes importantes para o proximo agente

- Instrucoes do usuario/projeto:
  - o usuario pode usar microfone/transcricao; interpretar palavras estranhas pelo contexto;
  - se algo puder ser executado autonomamente, execute sem pedir comando manual;
  - nao usar Playwright; preferir Chrome/Codex app se browser for necessario.
- Nao marcar o goal como completo: a manutencao ampla ainda nao acabou.
- Nao reverter mudancas que nao foram feitas por voce.
- Tratar `.codegraph/` como artefato local pesado.
- Preferir `rg` para buscas.
- Usar `apply_patch` para edicoes manuais.
- Unity esta indisponivel por licenca neste ambiente; documentar isso se tentar de novo.
- Antes de qualquer commit, revisar cuidadosamente o escopo, porque ha alteracoes relacionadas e nao relacionadas misturadas na worktree.
