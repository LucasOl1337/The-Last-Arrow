# AGENTS.md

O usuario frequentemente usa microfone com transcricao. Quando uma palavra parecer estranha, mas houver uma interpretacao clara pelo contexto do projeto, assuma a interpretacao correta e continue.

## Regras de Operacao

- Quando existir uma acao que o agente possa executar sozinho, execute. Nao pare para pedir comandos manuais ou aprovacao se o ambiente permitir.
- Nao usar Playwright neste projeto. Para navegador, prefira Chrome/Codex app quando for realmente necessario.
- Antes de alterar comportamento em Unity, leia os arquivos relevantes em `Assets/ProjectPVP/Scripts/Runtime` e preserve os padroes existentes.
- Evite reverter alteracoes que nao foram feitas por voce. O worktree pode estar sujo por trabalho do usuario.

## Unity

- Projeto Unity: `C:\Projetos\The-Last-Arrow`
- Versao atual do Editor: `6000.3.11f1`
- O projeto usa serializacao textual (`m_SerializationMode: 2`), entao prefira diffs revisaveis em C# e assets YAML quando possivel.
- O pacote oficial `com.unity.ai.assistant` fica no `Packages/manifest.json` para habilitar Unity AI/MCP.
- O Codex deve usar o MCP project-local em `.codex/config.toml` quando o Unity MCP Bridge estiver ativo no Editor.
- Para validar MCP no Unity: `Edit > Project Settings > AI > Unity MCP`, conferir se o Bridge esta `Running`, aprovar o cliente pendente e pedir ao Codex para ler o console.

## Verificacao

- Testes de editor existentes aparecem em `Assets/ProjectPVP/Tests/Editor`.
- Logs recentes de validacao ficam em `Logs/`.
- Use comandos Unity em batch quando uma mudanca precisar de validacao automatica, mas nao mate a instancia do Unity aberta pelo usuario.

## Stack de Bots

- A stack operacional do jogo fica em `tools/` e no `mainbot.py`.
- Fluxo atual: `Unity -> codex_broker.py -> codex_live_agent.py -> codex.exe -> broker -> Unity`.
- Preserve a arquitetura de intencao tatica: o modelo responde intencoes de curto prazo; o runtime executa continuamente entre respostas.
