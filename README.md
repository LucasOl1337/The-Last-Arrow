# The Last Arrow ⚔️

A 2D combat game prototype in **Unity / C#** (local + online multiplayer) — and, more interestingly for engineers, a **sandbox for LLM agents that play the game in real time.**

I built it as a testbed for one idea: *can an external AI agent perceive live game state, decide, and act through a stable interface — fast enough to actually fight?* The combat game is the environment; the agent loop is the point.

**Stack:** Unity · C# · Python (agent broker) · local HUD/observability (`localhost:8050`)

## The AI-agent loop (what makes this repo worth a look)

Bots aren't scripted state machines baked into the game. They run **outside** the engine and talk to it through a broker:

```
Unity (captures live fight state) → broker → agent → Codex-backed model → broker → Unity (applies the action)
```

- **Perception → decision → action over a clean boundary** — the game streams state out; the agent streams actions back. The engine never hard-codes bot logic, so the "brain" is swappable without touching gameplay code.
- **Bot Manager** — dual bot slots with persistent profiles and memory, launched from a single entrypoint (`python mainbot.py`).
- **Observability built in** — a technical HUD, per-slot overlays, and a local docs panel so you can watch what the agent perceives and why it acted.
- **Automated playtesting** — bot matches generate data that feeds back into tuning combat feel and balance, instead of relying only on manual play.

It's the same instinct behind my production work (agents that *operate* a system through a defined tool boundary, with logging and memory) — here applied to a real-time game instead of a SaaS.

## The game itself (honest scope)

A **prototype** (v0.3.0), not a finished title:

- Local and online 1v1 combat, "first to 5 kills" format with round indicators.
- Playable characters with distinct identities (a fast samurai; an electric martial artist).
- Modular architecture split into **Core · Gameplay · Input · Match · Presentation**.

Direction: sharper combat feel and visual feedback, stronger character identity, and using the automated-playtesting data to drive what changes next.

---

Built by [Lucas Oliveira](https://github.com/LucasOl1337) — full-stack / AI engineer.
More work: [LojaSync](https://github.com/LucasOl1337/LojaSync) (ERP automation, React + FastAPI) · [ChessCam](https://github.com/LucasOl1337/ChessCam) (real-time multiplayer chess).
