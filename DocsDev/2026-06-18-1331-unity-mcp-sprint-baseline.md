# Unity MCP Sprint - Fase 0/1 Baseline

Data: 2026-06-18 13:31

## Fase 0 - Smoke test do MCP

Resultado: **bloqueado nesta sessão do ZCode**.

O projeto já tem configuração local para o Unity MCP:

- `.codex/config.toml` define `mcp_servers.unity-mcp` usando `C:\Users\user\.unity\relay\relay_win.exe --mcp`.
- `Packages/manifest.json` inclui `com.unity.ai.assistant` em `2.12.0-pre.2`.

Porém, nesta sessão as ferramentas MCP do Unity não aparecem na lista de ferramentas expostas ao agente. Assim, não foi possível chamar diretamente operações como ler Console, hierarquia ativa, cena ativa ou componentes de GameObjects via MCP.

Critérios da Fase 0:

- Confirmar que unity-mcp está visível no Codex: **não passou nesta sessão**.
- Confirmar que Unity carregou `com.unity.ai.assistant`: **confirmado por `Packages/manifest.json`**.
- Ler Console do Unity via MCP: **não executado; ferramenta MCP não exposta**.
- Ler cena/hierarquia ativa via MCP: **não executado; ferramenta MCP não exposta**.
- Inspecionar pelo menos um GameObject real via MCP: **não executado; ferramenta MCP não exposta**.
- Alteração pequena/reversível ou leitura conservadora: **somente leitura por arquivos/logs**.

Próximo passo recomendado para destravar MCP: no Editor, abrir `Edit > Project Settings > AI > Unity MCP`, confirmar Bridge `Running`, aprovar o cliente pendente e reiniciar/abrir nova sessão Codex/ZCode para que as ferramentas MCP apareçam no harness.

## Fase 1 - Baseline inicial por arquivos/logs

Como fallback conservador, foi levantado um baseline parcial por arquivos e logs.

### Projeto/Unity

- Versão Unity registrada nos logs: `6000.3.11f1`.
- Cena encontrada: `Assets/ProjectPVP/Scenes/Bootstrap.unity`.
- Testes editor ficam em `Assets/ProjectPVP/Tests/Editor`.
- Logs recentes ficam em `Logs/`.

### Testes e logs

Arquivos lidos/consultados:

- `Logs/unity-editmode-full-current.log`
- `Logs/unity-current-failure.log`
- `Logs/unity-ai-input.log`

Achados:

- `unity-editmode-full-current.log` termina com `Test run completed. Exiting with code 2 (Failed). One or more tests failed.`
- Os logs contêm avisos/erros de licenciamento Unity comuns em batch mode:
  - `Failed to handshake to channel: "LicenseClient-user"`
  - `Access token is unavailable; failed to update`
  - depois a licença Unity Personal é resolvida com sucesso.
- Não foi possível extrair resumo XML de testes a partir de `Temp/unity-editmode-full-current.xml` ou `Logs/unity-current-failure.xml` no momento da checagem, pois o comando não retornou nós de resultado.

### Stack de bots

Arquivos principais encontrados:

- `tools/codex_broker.py`
- `tools/codex_live_agent.py`
- `tools/start_codex_stack.ps1`
- logs `tools/codex_broker*.log` e `tools/codex_live_agent*.log`
- memória em `tools/bot_memory/`

Não foi feito playtest/broker nesta rodada porque a Fase 0 MCP não passou e o pedido priorizava primeiro provar a visibilidade do Editor.

## Atualização de baseline sem MCP

Após a primeira escrita do relatório, foi feita uma checagem adicional por artefatos locais para reduzir pontos cegos enquanto o MCP não está exposto.

### EditMode

Resultado XML mais recente encontrado:

- `Temp/unity-editmode-full-20260618-075623.xml`
- Total: `468`
- Passed: `468`
- Failed: `0`
- Skipped: `0`
- Result: `Passed`
- Duration: `1,3291934`

Observação: isto contradiz o log `Logs/unity-editmode-full-current.log`, que termina com exit code 2. Para baseline atual, o XML timestampado é a evidência mais estruturada de um full EditMode verde, mas o log `current` ainda deve ser investigado quando o MCP/Console estiver disponível.

### Broker/live agent

Logs consultados:

- `tools/codex_broker.out.log`
- `tools/codex_broker.err.log`
- `tools/codex_live_agent.out.log`
- `tools/codex_live_agent.err.log`
- `tools/codex_live_agent_slot1.*.log`
- `tools/codex_live_agent_slot2.*.log`

Achados principais:

- Broker estava emitindo `active report | agentSessions=2 strategySessions=0`.
- Slots aparecem em `LocalHeuristic` / `heuristic_fallback` com `No Codex heartbeat yet` nos trechos finais do broker.
- Live agent registra `codex executable not found: C:\Users\user\.codex\.sandbox-bin\codex.exe; using local heuristic fallback`.
- Logs de erro recentes incluem `ConnectionAbortedError: [WinError 10053]` no broker e `TimeoutError: timed out` nos agentes de slot.

Conclusão parcial: a stack existe e produz fallback local, mas a confiabilidade do loop agente/broker ainda não está saudável; há ausência de heartbeat Codex e timeouts/conexões abortadas nos logs recentes.

### Bot memory

Arquivos consultados:

- `tools/bot_memory/runtime_slot_assignments.json`
- `tools/bot_memory/roster.json`
- `tools/bot_memory/**/latest_round_review.md`

Estado observado:

- `runtime_slot_assignments.json` atualizado em `2026-06-18T07:56:54`.
- Slot 1 ativo: `slot-1-smoke-g2-g3-g4`, provider `openai_codex`.
- Slot 2 ativo: `slot-2-smoke-g2-g3`, provider `openai_codex`.
- Roster contém modelos com validação `unvalidated` em alguns perfis.
- Existem reviews e relatórios recentes em `tools/bot_memory/bots/*/match_reports/`.

## Estado final desta rodada

- MCP configurado no projeto: **sim**.
- MCP disponível como ferramenta nesta sessão: **não**.
- Baseline por logs/arquivos: **parcial, ampliado com XML de testes, broker e bot_memory**.
- EditMode full mais recente por XML timestampado: **468/468 passed**.
- Código/gameplay alterado: **não**.
- Console/cena/hierarquia via MCP: **não validado**.
- Loop broker/live-agent: **ativo em fallback heurístico, com timeouts/heartbeat ausente nos logs recentes**.

## Checagem pós-reinício do usuário

Após o usuário reiniciar para tentar destravar o bridge, foi feita nova checagem local.

Evidências:

- Processo `relay_win.exe` está rodando a partir de `C:\Users\user\.unity\relay\relay_win.exe`.
- `.codex/config.toml` continua apontando para o relay correto e para `UNITY_PROJECT_PATH = 'C:\Projetos\The-Last-Arrow'`.
- Existe conexão antiga em `C:\Users\user\.unity\mcp\connections\bridge-a55b95bf-43132.json`, mas ela aponta para `project_path = C:\Users\user\Desktop\The Last Arrow` e `editor_pid = 43132` em vez do projeto atual `C:\Projetos\The-Last-Arrow`.
- Mesmo após o reinício, esta sessão do ZCode ainda não expõe ferramentas MCP do Unity no harness; portanto ainda não há como chamar Console/hierarquia/GameObject diretamente daqui.

Interpretação: o relay está vivo como processo, mas o cliente MCP desta sessão ainda não recebeu ferramentas Unity. Além disso, a conexão persistida encontrada parece ser de um projeto/caminho antigo, não do workspace atual.

## Próxima rodada sugerida

Assim que o Unity MCP aparecer como ferramenta nesta sessão:

1. Ler Console real via MCP.
2. Ler cena ativa e hierarquia.
3. Inspecionar um GameObject real da cena `Bootstrap`.
4. Capturar missing references/prefabs/componentes quebrados.
5. Rodar um ciclo pequeno de melhoria com validação por Console + testes.

Se as ferramentas continuarem ausentes, recriar a conexão do Unity MCP no Editor com o projeto atual aberto em `C:\Projetos\The-Last-Arrow`, garantindo que o bridge registrado em `%USERPROFILE%\.unity\mcp\connections` aponte para esse caminho, não para `C:\Users\user\Desktop\The Last Arrow`.

## Correção aplicada na conexão MCP

Ação executada após solicitação do usuário:

1. A conexão stale `C:\Users\user\.unity\mcp\connections\bridge-a55b95bf-43132.json` foi movida para backup em `C:\Users\user\.unity\mcp\connections\stale-backup-20260618-160844\`.
2. O Unity foi iniciado para o projeto atual `C:\Projetos\The-Last-Arrow`.
3. Uma nova conexão MCP foi criada automaticamente:
   - arquivo: `C:\Users\user\.unity\mcp\connections\bridge-d2cc1801-16448.json`
   - `connection_path`: `\\.\pipe\unity-mcp-d2cc1801-16448`
   - `project_path`: `C:\Projetos\The-Last-Arrow`
   - `protocol_version`: `2.0`
   - `editor_pid`: `16448`
4. O processo `relay_win.exe` foi reiniciado e está rodando novamente a partir de `C:\Users\user\.unity\relay\relay_win.exe`.

Resultado: o lado Unity/relay agora aponta para o projeto correto. Falta somente o harness desta sessão ZCode recarregar/expor as ferramentas MCP; se isso não ocorrer dinamicamente, abrir uma nova sessão deve carregar o servidor `unity-mcp` com a conexão correta.

## Correção aplicada na stack broker/live-agent

Ação executada para avançar enquanto o MCP não aparece no harness:

1. `tools/codex_live_agent.py` não usa mais somente o caminho hardcoded `C:\Users\user\.codex\.sandbox-bin\codex.exe`.
2. Foi adicionada resolução automática do comando Codex:
   - respeita `CODEX_EXE`, se definido;
   - usa `~\.codex\.sandbox-bin\codex.exe`, se existir;
   - procura `codex.exe`, `codex.cmd` ou `codex` no `PATH`.
3. Validação de sintaxe executada com sucesso:
   - `python -m py_compile tools/codex_live_agent.py`
4. A stack foi reiniciada via `tools/start_codex_stack.ps1`.
5. Logs pós-restart mostram os dois agentes usando o Codex real:
   - `using codex command: C:\Users\user\AppData\Roaming\npm\codex.cmd`
   - `provider=openai_codex`
6. Não há traceback nos logs `codex_live_agent_slot1.err.log` ou `codex_live_agent_slot2.err.log` após a correção.

Resultado: o bloqueio `codex executable not found: C:\Users\user\.codex\.sandbox-bin\codex.exe; using local heuristic fallback` foi corrigido. A stack ainda precisa receber estados reais de partida para criar `agentSessions`, mas os agentes agora iniciam apontando para o Codex CLI disponível.
