![The-Last-Arrow v0.1.0](https://github.com/LucasOl1337/The-Last-Arrow/releases/download/v0.1.0/v0.1.0-card.png)

# v0.1.0 - Baseline oficial (22/06/2026)

Primeira release oficial do The-Last-Arrow, publicada a partir da branch `agent-loop` para registrar o estado factual do prototipo Unity PvP e da stack de bots.

## Novidades

- **Ferramenta de portraits PVP:** `ProjectPvpCharacterPortraitRepairTools.cs` adiciona uma ferramenta de Editor para reparar portraits dos personagens PVP.
- **Baseline Unity MCP:** `DocsDev/2026-06-18-1331-unity-mcp-sprint-baseline.md` documenta o sprint Unity MCP e o estado inicial da integracao.
- **Unity AI Assistant:** o pacote oficial `com.unity.ai.assistant` permanece configurado para habilitar Unity AI/MCP no projeto.

## Melhorias

- **UI ProjectPVP organizada:** assets de menu, incluindo `the-last-arrow-menu-bg-v1.png`, ficam em `Resources/ProjectPVP/UI/`.
- **Auditoria visual TowerFall:** `DocsDev/towerfall-clone-audit/` registra screenshots e `visual-matrix.html` para comparar apresentacao e arena.
- **Arquitetura runtime registrada:** o release descreve os blocos `Runtime/Core`, `Gameplay`, `Input`, `Match` e `Presentation`, alem da stack de bots em `tools/`.

## Correcoes

- **Estado de merge explicitado:** o corpo do release registra que `main` permanece intocada enquanto o GO firsthand do dono nao for concedido.
- **Limitacao operacional registrada:** o release documenta que nao ha CI configurado para esta baseline.

## Sistemas

- **Regras de agente canonicas:** `AGENTS.md` define operacao por microfone/transcricao, preferencia por Chrome/Codex sobre Playwright e verificacao Unity.
- **Gitignore de automacao:** `.gitignore` bloqueia artefatos locais de autonomia que nao sao codigo-fonte.
- **IA tatica consolidada:** o runtime mantem intencao tatica via Codex broker, snapshot semantico e memoria persistente de bots.

---

## Notas tecnicas

Base publicada em `v0.1.0` no commit `abedb27` da branch `agent-loop`. Esta reparacao nao move a tag: apenas adiciona o card visual obrigatorio ao GitHub Release existente e registra os artefatos de release em `DocsDev/releases/`.
