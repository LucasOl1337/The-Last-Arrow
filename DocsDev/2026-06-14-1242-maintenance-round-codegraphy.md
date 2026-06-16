# The Last Arrow - Maintenance Round / CodeGraphy Notes

Data/hora local: 2026-06-14 12:42 -03:00

## Objetivo desta fatia

Continuar a rodada longa de manutencao com foco em:

- aproveitar o artefato local `.codegraph/` como apoio de investigacao arquitetural;
- reduzir bugs de combate herdados de instant-kill;
- melhorar robustez da stack Python de bots;
- aplicar uma melhoria de qualidade de vida para playtest/bots.

## CodeGraphy / .codegraph

Estado observado:

- `.codegraph/` existe localmente e esta ignorado pelo Git.
- `codegraph.db` e consultavel via SQLite.
- Contagem no momento da consulta:
  - `files`: 118
  - `nodes`: 3745
  - `edges`: 6832
  - `unresolved_refs`: 2760
- Frescor do indice no momento da consulta:
  - `checked files`: 118
  - `stale files`: 0
  - `missing files`: 0
- Antes da sincronizacao, `codegraph status --json .` reportava `5` arquivos adicionados e `14` modificados pendentes. `codegraph sync .` foi executado e o status final ficou `added: 0`, `modified: 0`, `removed: 0`.
- Os `unresolved_refs` altos parecem incluir APIs externas/Unity e chamadas encadeadas que o CodeGraph nao resolve completamente. A metrica ainda e util como mapa de opacidade por arquivo, nao como lista direta de erros de compilacao.

Arquivos mais densos por node count:

1. `Assets/ProjectPVP/Scripts/Runtime/Gameplay/PlayerController.cs`
2. `Assets/ProjectPVP/Scripts/Runtime/Match/MatchController.cs`
3. `Assets/ProjectPVP/Scripts/Runtime/Data/CharacterDefinition.cs`
4. `Assets/ProjectPVP/Scripts/Runtime/Input/AiArenaCodexProtocol.cs`
5. `Assets/ProjectPVP/Scripts/Runtime/Input/KeyboardPlayerInputSource.cs`
6. `Assets/ProjectPVP/Scripts/Runtime/Input/AiArenaProtocol.cs`
7. `Assets/ProjectPVP/Characters/Mizu/Scripts/MizuUltimateReplayModule.cs`
8. `Assets/ProjectPVP/Scripts/Runtime/Input/CodexBrokerCombatantInputSource.cs`

Interpretacao: a decisao anterior de seguir hibrido incremental continua correta. O grafo confirma os maiores centros de acoplamento ja identificados manualmente: match orchestration, player orchestration, input monolitico e bot bridge.

Ferramenta adicionada:

- `python tools\codegraphy_report.py --limit 10`
- Le `.codegraph/codegraph.db` local e imprime Markdown com contagens, frescor do indice, unresolved refs, arquivos mais densos e nodes com maior grau.
- `--json` emite JSON para automacao.
- `--workspace-root` permite apontar explicitamente a raiz usada para comparar arquivos indexados com o disco.
- Testes em `tools/tests/test_codegraphy_report.py` usam SQLite temporario, sem depender do banco local pesado.
- O relatorio agora mostra top arquivos por unresolved refs e top nomes de referencias nao resolvidas, para orientar a proxima investigacao.
- O relatorio agora tambem classifica unresolved refs em:
  - `likely external/noisy refs`: refs provaveis de API externa, builtins, Unity/NUnit/reflection ou metodos comuns de container/string/filesystem;
  - `potential project refs`: refs que sobram como candidatos mais uteis para investigacao arquitetural.
- No ultimo snapshot desta fatia: `2380` refs cairam como ruido externo/provavel e `380` ficaram como potenciais refs de projeto.

## Implementado

### Combate

- `PlayerCombatSystem.HandleIncomingProjectile` deixou de chamar `Kill()` diretamente.
- Hit de projectile agora aplica:
  - hitstun baseado em `source.characterDefinition.meleeHitstunDuration`, fallback `0.1`;
  - knockback baseado em `source.characterDefinition.projectileKnockbackForce`, fallback `300`;
  - direcao baseada em `ProjectileController.TravelDirection`, com fallback source -> target.
- `PlayerJumpSystem.TryCheckHeadStomp` deixou de chamar `Kill()` diretamente.
- Head stomp agora aplica hitstun/knockback no alvo e preserva o bounce vertical do atacante.
- A mudanca anterior de ultimate para hitstun/knockback foi mantida.
- `MatchController` agora tem `verticalRingOutEnabled`, ligado por padrao.
- Quando um jogador cai abaixo de `wrapBounds.yMin - wrapPadding.y`, o jogador e eliminado via `Kill()` em vez de ser embrulhado verticalmente.
- O wrap horizontal continua ativo; o ring-out so afeta a borda inferior.
- `PlayerCombatSystem.Kill()` agora retorna `bool` indicando se a morte mudou o estado de vivo para morto.
- `PlayerController` ganhou `TryKill()` e `Kill()` passou a delegar para ele.
- Motivo: evitar que chamadas repetidas de `Kill()` em um jogador ja morto agendem multiplas notificacoes `Died`, o que poderia duplicar fluxo de round/reset quando a primeira notificacao ainda esta pendente.
- `PlayerController` agora retorna defaults seguros em propriedades publicas de runtime quando `_context`, `_statResolver` ou `_actionLockSystem` ainda nao existem.
- Motivo: evitar `NullReferenceException` em tooling de Editor, overlays, gizmos ou testes que consultem estado publico antes de `Awake()`.

Testes Unity adicionados em `Assets/ProjectPVP/Tests/Editor/PlayerCombatSystemTests.cs`:

- ultimate aplica hitstun/knockback sem matar direto;
- projectile aplica hitstun/knockback sem matar direto;
- head stomp aplica hitstun/knockback sem matar direto e mantem bounce.
- `TryKill()` retorna `true` apenas na primeira transicao para morto e `false` nas chamadas seguintes.
- propriedades publicas basicas do `PlayerController` retornam defaults seguros antes de `Awake()`.

Testes Unity adicionados em `Assets/ProjectPVP/Tests/Editor/MatchControllerRoundFlowTests.cs`:

- queda abaixo do limite inferior causa ring-out/kill;
- saida lateral continua fazendo wrap horizontal.
- payload runtime de bot com slot desabilitado e processado sem reativar fallback, restaurando o slot para controle humano apos um override AI anterior.
- fallback automatico de bot respeita `autoEnableSlotTwoDebugBotOnPlay` e preserva `slotTwoDebugAiBrain` quando `autoForceCodexBrokerForSlotTwoOnPlay` esta desligado.
- reaplicar o bot debug do slot 2 nao sobrescreve mais o perfil original; desligar o bot volta ao mesmo perfil humano original.

### Bots / broker

- `AiArenaRuntimeSnapshotCollector` agora prefere contratos explicitos:
  - `IAiArenaControllerSnapshotSource`
  - `IAiArenaProjectileSnapshotSource`
  - `IAiArenaArenaSnapshotSource`
- `PlayerController`, `ProjectileController` e `MatchController` implementam esses contratos.
- O fallback por reflection/nome de classe foi mantido para compatibilidade com testes e objetos legados.
- Isso remove a necessidade de o assembly `ProjectPVP.Input` conhecer tipos concretos de `Gameplay`/`Match`, evitando ciclo de assembly.
- Teste Unity adicionado em `Assets/ProjectPVP/Tests/Editor/AiArenaInputSourceTests.cs` para exercitar fontes tipadas por interface via `LocalAiCombatantInputSource`.
- `AiArenaSnapshotSourceRegistry` foi adicionada para manter fontes ativas de controller/projectile/arena sem `FindObjectsByType` no caminho normal.
- `PlayerController`, `ProjectileController` e `MatchController` registram/desregistram suas fontes no lifecycle.
- O coletor ainda cai no scan global quando a registry esta vazia, preservando compatibilidade com objetos legados e testes antigos.
- Teste Unity adicionado em `Assets/ProjectPVP/Tests/Editor/AiArenaInputSourceTests.cs` para garantir registro unico e unregister da registry.
- `ProjectileController.BuildAiArenaProjectileSnapshot` agora marca projeteis nao lancados como invalidos, evitando que objetos ativos/prefabs em cena sejam observados como ameacas por bots.
- `tools/codex_broker.py` agora valida `Content-Length` antes de ler o body.
- Adicionado limite configuravel `CODEX_BROKER_MAX_REQUEST_BYTES`, default `1 MB`.
- Payloads JSON agora passam por helper testado que rejeita JSON invalido, UTF-8 invalido e payload nao objeto.
- Novos testes em `tools/tests/test_codex_broker.py`.
- `tools/bot_manager.py` agora usa escrita atomica para JSON/Markdown persistente (`roster.json`, runtime assignments e conhecimento global).
- Motivo: reduzir risco de corromper memoria persistente se o processo cair durante uma escrita.
- Novos testes em `tools/tests/test_bot_manager.py`.
- `tools/codex_memory.py` agora reutiliza a escrita atomica do `bot_manager.py` para perfil privado do oponente e relatorios/planos Markdown.
- A gravacao JSONL da memoria agora cria o diretorio pai antes de anexar evento/review.
- Motivo: evitar falha ou arquivo parcial quando a pasta de memoria do bot ainda nao existe, foi removida, ou o processo cai durante atualizacao de snapshot Markdown/JSON.
- Novos testes em `tools/tests/test_codex_memory.py`.
- `CodexBrokerCombatantInputSource` agora versiona requests de session start e strategy/state.
- Callbacks atrasados passam a ser ignorados quando o watchdog ou uma invalidação troca a versao ativa da request.
- `ConfigureForSlot` descarta `sessionId` quando o slot real muda, evitando reaproveitar sessao de outro slot.
- Teste Unity adicionado em `Assets/ProjectPVP/Tests/Editor/AiArenaInputSourceTests.cs` para a regra pura de versao de request.
- `CodexBrokerCombatantInputSource` agora aceita intent executavel no modo broker direto (`useAgentDrivenMode=false`) mesmo quando o envelope legado nao traz `hasAgentAction`.
- `tools/codex_broker.py` agora marca snapshots de `BrokerSession` direto com `hasAgentAction: true` e `controllerOwner: CodexDirect`.
- Motivo: evitar que o modo `/session/start` + `/strategy/tick` receba uma intent valida, mas fique preso em `AI | Waiting Codex` porque essa flag so existia no fluxo agent-driven.
- `AiArenaFrameExecutor` agora retorna frame neutro quando `roundResetPending` esta ativo.
- Motivo: intents antigas de bot nao devem continuar gerando tiro, melee, dash ou jump durante a janela de reset de round.
- Teste Unity adicionado em `Assets/ProjectPVP/Tests/Editor/AiArenaInputSourceTests.cs` cobrindo `CodexBrokerCombatantInputSource` com self/oponente/arena tipados e intent ofensiva stale.
- `MatchController` agora trata arquivo runtime de menu de bots como escolha explicita mesmo quando todos os slots estao desabilitados.
- Motivo: evitar que um payload valido com `enabled: false` em todos os slots caia no fallback automatico e force o slot 2 como Codex bot de novo.
- O helper de aplicacao runtime separa `payload processado` de `algum bot habilitado`, e restaura overrides anteriores quando um slot vem desabilitado.
- `Start`, bootstrap e respawn do `MatchController` agora usam automacao configurada em vez de chamar diretamente o metodo `Force...`.
- Motivo: respeitar `autoEnableSlotTwoDebugBotOnPlay` e `autoForceCodexBrokerForSlotTwoOnPlay` quando nao existe arquivo runtime de menu de bots.
- Os metodos publicos `ForceSlotTwoCodexBotReadyForPlay` e `ForceCodexBotsReadyForPlay` continuam com semantica explicita de forcar bot Codex.
- `EnsurePlayerTwoDebugBotEnabled(true, forceReapply: true)` agora preserva `_slotTwoOriginalProfile` quando o bot ja estava ativo.
- Motivo: bootstrap, `Start` e respawn podem reaplicar o override AI varias vezes; antes disso, o perfil original podia virar o proprio override e o shortcut de desligar restaurava AI em vez de humano.

### Performance / runtime scans

- `PlayerController` agora mantem uma registry estatica de jogadores ativos em runtime.
- `PlayerCombatSystem.ResolveProjectileAssistTarget` e `PlayerJumpSystem.TryCheckHeadStomp` consultam essa registry antes de cair em `FindObjectsByType<PlayerController>`.
- `ProjectileController` agora mantem uma registry estatica de projeteis ativos em runtime.
- `ProjectPvpCombatDebugGizmos` e `DebugAimOverlay` consultam as registries de jogadores/projeteis antes de cair em scans globais.
- O fallback global foi mantido para compatibilidade em EditMode/testes e objetos legados.
- Testes Unity adicionados em `Assets/ProjectPVP/Tests/Editor/PlayerCombatSystemTests.cs` para deduplicacao/copia/unregister das registries de jogadores e projeteis.

### Trace store

- `tools/codex_trace_store.py` agora rotaciona `trace_events.jsonl` quando passa do limite configurado.
- Limite configuravel `CODEX_TRACE_MAX_BYTES`, default `5 MB`.
- Rotacao simples para `trace_events.1.jsonl`.
- `append_trace_event` agora sanitiza payloads antes de persistir:
  - redige chaves sensiveis como `authorization`, `apiKey`, `password`, `secret`, `accessToken`, `refreshToken`;
  - redige tokens comuns dentro de strings, como `Bearer ...` e `sk-...`;
  - trunca strings longas via `CODEX_TRACE_MAX_STRING_CHARS`, default `4096`;
  - limita listas via `CODEX_TRACE_MAX_LIST_ITEMS`, default `50`.
- Novos testes em `tools/tests/test_codex_trace_store.py`.

### Qualidade de vida

- `ProjectSettings/ProjectSettings.asset`: `runInBackground` alterado para `1`.
- Motivo: playtest com bots e janelas auxiliares nao deve depender da janela Unity estar focada.
- `KeyboardPlayerInputSource` agora resolve gamepads por familia usando o indice dentro dos controles que batem com a familia.
- Motivo: evitar que dois slots que preferem a mesma familia de controle peguem o mesmo primeiro gamepad antes da regra por indice.
- Teste Unity adicionado em `Assets/ProjectPVP/Tests/Editor/AiArenaInputSourceTests.cs` para o helper puro de selecao por familia.

## Verificacao

Passou:

- `python -m pytest tools\tests -q` -> `17 passed`
- `python -m compileall -q mainbot.py tools`
- `codegraph sync .` -> sincronizou `19` arquivos inicialmente, depois `2` arquivos do proprio relatorio/teste, depois `4` arquivos C# da registry, depois `4` arquivos de overlay/projeteis, depois `2` arquivos do trace store, depois `2` arquivos da persistencia de memoria, depois `2` arquivos da classificacao de unresolved refs, depois `3` arquivos da morte idempotente, depois `2` arquivos dos defaults pre-`Awake`, depois `2` arquivos do fallback do menu runtime de bots, depois `2` arquivos da automacao configuravel de bots, depois `4` arquivos do broker direto, depois `2` arquivos da preservacao do perfil original do slot 2 e depois `2` arquivos da protecao de frame neutro durante round reset
- `codegraph status --json .` -> `pendingChanges`: `added 0`, `modified 0`, `removed 0`
- `python tools\codegraphy_report.py --limit 8` -> reportou `0` arquivos stale, `0` missing, `2760` unresolved refs brutos, `2380` likely external/noisy refs e `380` potential project refs
- `git diff --check` nos arquivos alterados desta fatia

Bloqueado:

- Unity EditMode batchmode tentou rodar com Unity `6000.3.11f1`.
- Nao gerou XML de resultado.
- Log em `Logs/codex-editmode.log` termina com:
  - `No valid Unity Editor license found. Please activate your license.`
  - return code reportado pelo Unity no log: `198`
- A tentativa foi repetida nesta continuação com `-runTests -testPlatform EditMode`; o processo retornou rapido, mas ainda nao gerou `Logs/codex-editmode-results.xml` e o log voltou a terminar no mesmo erro de licenca.
- A tentativa foi repetida novamente depois dos defaults pre-`Awake`; o resultado continuou igual: sem XML e com erro de licenca no log.
- A tentativa foi repetida novamente depois do fallback do menu runtime de bots; o resultado continuou igual: sem XML e com erro de licenca no log.
- A tentativa foi repetida novamente depois da automacao configuravel de bots; o resultado continuou igual: sem XML e com erro de licenca no log.
- A tentativa foi repetida novamente depois da correcao do broker direto; o resultado continuou igual: sem XML e com erro de licenca no log.
- A tentativa foi repetida novamente depois da preservacao do perfil original do slot 2; o resultado continuou igual: sem XML e com erro de licenca no log.
- A tentativa foi repetida novamente depois do frame neutro em round reset; o resultado continuou igual: sem XML e com erro de licenca no log.

## Riscos e observacoes

- A migracao de combate removeu instant-kill dos hits diretos restantes em runtime. Isso melhora feel/counterplay.
- Foi adicionada uma regra minima de eliminacao: ring-out inferior. Ainda falta validar o feel em Play Mode.
- `MatchController` ainda depende de evento `Died` para pontuar/resetar round, mas agora a queda inferior volta a alimentar esse fluxo.
- A notificacao `Died` agora so deve ser agendada quando a morte realmente muda o estado do jogador. Ainda precisa de validacao Unity assim que a licenca permitir rodar os testes EditMode/PlayMode.
- A protecao de getters pre-`Awake` melhora robustez de Editor/tooling, mas tambem depende da validacao Unity bloqueada para confirmar compilacao e execucao dos novos testes C#.
- O coletor de snapshots agora prefere registry de fontes ativas; ainda falta medir em profiler o impacto real contra cenas com muitos `MonoBehaviour`.
- A correcao de gamepad e pequena, mas ainda precisa de validacao em Play Mode com dois controles fisicos, especialmente combinacoes Xbox/DualSense.
- `CodexBrokerCombatantInputSource` foi blindado contra callbacks atrasados por versao de request, mas ainda precisa de validacao em Play Mode com o broker real por causa do fluxo assincrono UnityWebRequest/coroutines.
- A correcao do broker direto cobre parse/estado local e snapshot Python, mas ainda precisa de validacao Play Mode com `useAgentDrivenMode=false`.
- A protecao de round reset evita input ofensivo stale no executor, mas ainda precisa de validacao Play Mode para confirmar interacao com freeze/respawn e feedback visual.
- A correcao do menu runtime de bots cobre o helper em teste de Editor, mas ainda precisa de validacao Play Mode com o menu real gravando `tools/bot_runtime_assignments.json`.
- A automacao configuravel de bots preserva o comportamento padrao porque as flags publicas seguem `true`, mas cenas que serializarem essas flags como `false` agora passam a ser respeitadas.
- A preservacao do perfil original do slot 2 cobre reaplicacao via teste de Editor, mas ainda precisa de validacao manual com o shortcut em Play Mode.
- O trace store agora reduz vazamento acidental em logs novos, mas arquivos antigos em `tools/bot_memory/traces/` nao foram migrados ou apagados nesta fatia.
- A memoria privada/Markdown dos bots agora grava com replace atomico, mas arquivos antigos ja corrompidos continuam sendo tratados pelo fallback existente em leitura, nao reparados retroativamente.
- `.codegraph/codegraph.db` esta sincronizado no fim desta fatia. O total bruto de `unresolved_refs` segue alto, mas agora o relatorio separa o ruido provavel do conjunto menor de potenciais refs de projeto.
- As registries de jogadores/projeteis ativos reduzem scans globais em runtime, mas ainda precisam de validacao em Play Mode para confirmar lifecycle correto em respawn/desabilitacao de objetos.
- A regra de design atual passa a ser:
  - hits causam reacao;
  - morte acontece por ring-out inferior;
  - HP/hazards/categorias letais podem ser discutidos depois como expansao, nao como dependencia imediata.
- `PlayerController.cs.bak` e `PlayerControllerRefactored.cs` continuam rastreados e sao ruido operacional. Nao foram removidos nesta fatia.
- O estado sujo existente em `grokassets`, `patchnotes.md`, `changelog.md` e outros arquivos nao foi revertido nem organizado.

## Proximos passos recomendados

1. Resolver licenca Unity para validar EditMode/PlayMode.
2. Validar a nova regra de ring-out inferior em Play Mode e ajustar `wrapBounds`/`wrapPadding` se necessario.
3. Extrair do `MatchController` um `RoundFlowService` testavel sem cena.
4. Medir `AiArenaRuntimeSnapshotCollector` em profiler com bots ativos para confirmar o ganho da registry e identificar alocacoes restantes.
5. Decidir remocao/arquivo de `PlayerController.cs.bak` e `PlayerControllerRefactored.cs`.
6. Validar `CodexBrokerCombatantInputSource` em Play Mode com broker real e confirmar que watchdog/timeouts nao duplicam falhas.
7. Usar a nova secao de unresolved refs do `tools\codegraphy_report.py` para separar limitacoes do parser de acoplamentos reais que merecem extracao.
