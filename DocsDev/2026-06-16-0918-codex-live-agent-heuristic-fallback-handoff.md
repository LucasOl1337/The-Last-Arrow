# The Last Arrow - Handoff Codex Live Agent Heuristic Fallback

Data/hora local: 2026-06-16 09:18 -03:00

## Contexto

A stack de bots ainda dependia do `codex.exe` para o `codex_live_agent.py` subir. Neste ambiente o executavel nao existe, entao o agente terminava antes de controlar qualquer sessao. Isso quebrava o playtest automatizado via broker.

## Alterado nesta continuacao

- `tools/codex_live_agent.py`
  - Adicionado `resolve_runtime_provider()`.
  - Quando o provider configurado exige Codex, mas o executavel nao existe, o runtime agora cai para `heuristic`.
  - O agente heuristico gera intents validas localmente a partir do snapshot de combate.
  - O heartbeat agora reporta `local-heuristic` em vez de fingir que ainda esta rodando o modelo do Codex.
  - O loop principal continua funcionando com `openai_codex`, `openrouter` e `ollama` quando esses caminhos estao disponiveis.
- `tools/tests/test_codex_live_agent.py`
  - Novo teste para resolver provider.
  - Novo teste para o nome do modelo local heuristico.
  - Novo teste para intent de punish.
  - Novo teste para evasao de projetil com dash.

## Verificacoes

Passou:

- `python -m py_compile tools\\codex_live_agent.py tools\\tests\\test_codex_live_agent.py`
- `python -m pytest tools\\tests -q` -> `21 passed`

## Playtest simulado

Consegui validar o fluxo end-to-end sem Unity:

1. Subi `tools/codex_broker.py`.
2. Subi `tools/codex_live_agent.py` com fallback automatico.
3. Criei uma sessao `slot 2` via `POST /agent/session/start`.
4. Apliquei dois updates de estado.
5. O agente publicou intents diferentes conforme o estado:
   - primeiro `zone`
   - depois `retreat`

Resultado observado no broker:

- `model=local-heuristic`
- `agent_action` recebido com sucesso
- `agentActionCount=2`

## Proximo passo recomendado

Quando a Unity voltar a ficar disponivel, o playtest real agora pode validar feel e timing em cima de uma base que nao depende mais do executavel do Codex para funcionar.
