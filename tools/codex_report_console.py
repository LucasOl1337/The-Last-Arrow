import json
import os
import shutil
import textwrap
import time
from collections import deque
from pathlib import Path
from typing import Any
from urllib.error import HTTPError, URLError
from urllib.request import urlopen
from bot_manager import BotManager
from codex_memory import MemoryTracker


TOOLS_DIR = Path(__file__).resolve().parent
BROKER_BASE = os.environ.get("CODEX_BROKER_BASE", "http://127.0.0.1:8765").rstrip("/")
REFRESH_SECONDS = float(os.environ.get("CODEX_REPORT_REFRESH_SECONDS", "0.75"))
STALE_HOLD_SECONDS = float(os.environ.get("CODEX_REPORT_STALE_HOLD_SECONDS", "15"))
AGENT_LOG = TOOLS_DIR / "codex_live_agent.out.log"
BROKER_LOG = TOOLS_DIR / "codex_broker.out.log"

ANSI_RESET = "\033[0m"
ANSI_BOLD = "\033[1m"
ANSI_DIM = "\033[2m"
ANSI_CYAN = "\033[36m"
ANSI_GREEN = "\033[32m"
ANSI_YELLOW = "\033[33m"
ANSI_RED = "\033[31m"
ANSI_BLUE = "\033[34m"


def compact_line(value: str) -> str:
    return " ".join((value or "").strip().split())


def terminal_width() -> int:
    return max(96, shutil.get_terminal_size(fallback=(120, 40)).columns)


def clear_screen() -> None:
    os.system("cls" if os.name == "nt" else "clear")


def colorize(text: str, color: str, *, bold: bool = False, dim: bool = False) -> str:
    prefix = ""
    if bold:
        prefix += ANSI_BOLD
    if dim:
        prefix += ANSI_DIM
    prefix += color
    return f"{prefix}{text}{ANSI_RESET}"


def wrap_lines(text: str, width: int, indent: str = "") -> list[str]:
    normalized = compact_line(text) or "-"
    return textwrap.wrap(
        normalized,
        width=max(20, width),
        initial_indent=indent,
        subsequent_indent=indent,
        break_long_words=False,
        break_on_hyphens=False,
    ) or [f"{indent}-"]


def rule(char: str = "-", width: int | None = None) -> str:
    return char * (width or terminal_width())


def format_ms(value: int) -> str:
    if value < 0:
        return "-"
    if value < 1000:
        rounded = int(round(value / 100.0) * 100)
        return f"{rounded} ms"
    seconds = value / 1000.0
    if seconds < 10:
        rounded = round(seconds * 2) / 2
        return f"{rounded:.1f} s"
    return f"{int(round(seconds))} s"


def http_get_json(path: str) -> tuple[int, Any]:
    try:
        with urlopen(f"{BROKER_BASE}{path}", timeout=3) as response:
            return response.status, json.loads(response.read().decode("utf-8"))
    except HTTPError as exc:
        payload = exc.read().decode("utf-8", errors="replace")
        try:
            return exc.code, json.loads(payload)
        except json.JSONDecodeError:
            return exc.code, {"ok": False, "error": payload}
    except URLError as exc:
        return 0, {"ok": False, "error": str(exc.reason)}


def read_recent_lines(path: Path, count: int, patterns: tuple[str, ...] = ()) -> list[str]:
    if not path.exists():
        return []

    try:
        text = path.read_text(encoding="utf-8", errors="replace")
    except OSError:
        return []

    lines: deque[str] = deque(maxlen=count * 6)
    for raw_line in text.splitlines():
        line = compact_line(raw_line)
        if not line:
            continue
        if patterns and not any(pattern in line for pattern in patterns):
            continue
        if not lines or lines[-1] != line:
            lines.append(line)
    return list(lines)[-count:]


def read_recent_lines_from_paths(paths: list[Path], count: int, patterns: tuple[str, ...] = ()) -> list[str]:
    for path in paths:
        lines = read_recent_lines(path, count, patterns)
        if lines:
            return lines
    return []


def agent_log_paths(slot_id: int) -> list[Path]:
    return [
        TOOLS_DIR / f"codex_live_agent_slot{slot_id}.out.log",
        AGENT_LOG,
    ]


def status_chip(session: dict[str, Any], offline: bool = False) -> str:
    if offline:
        return colorize("OFFLINE", ANSI_RED, bold=True)

    last_error = compact_line(str(session.get("agentLastError", "") or session.get("lastError", "") or ""))
    owner = str(session.get("controllerOwner", "") or "-")
    phase = str(session.get("agentPhase", "") or "unknown").lower()

    if last_error:
        return colorize("ERROR", ANSI_RED, bold=True)
    if owner == "BrokerDefault":
        if phase == "thinking":
            return colorize("THINKING", ANSI_BLUE, bold=True)
        if phase == "waiting_for_agent":
            return colorize("WAITING", ANSI_CYAN, bold=True)
        if phase == "idle":
            return colorize("DEFAULT", ANSI_YELLOW, bold=True)
        return colorize("BROKER", ANSI_YELLOW, bold=True)
    if owner != "Codex":
        return colorize("FALLBACK", ANSI_YELLOW, bold=True)
    if phase == "thinking":
        return colorize("THINKING", ANSI_BLUE, bold=True)
    if phase in {"ready", "acting"}:
        return colorize("LIVE", ANSI_GREEN, bold=True)
    return colorize(phase.upper(), ANSI_CYAN, bold=True)


def print_block(lines: list[str], output: list[str]) -> None:
    output.extend(lines)
    output.append("")


def add_title(output: list[str], width: int) -> None:
    output.append(colorize("CODEX THOUGHT HUD", ANSI_CYAN, bold=True))
    output.append(rule("=", width))


def add_section(title: str, output: list[str]) -> None:
    output.append(colorize(title, ANSI_CYAN, bold=True))


def append_wrapped(label: str, value: str, width: int, output: list[str]) -> None:
    prefix = f"  {label:<15}"
    wrapped = wrap_lines(value, max(30, width - len(prefix) - 1))
    for index, line in enumerate(wrapped):
        if index == 0:
            output.append(f"{prefix} {line}")
        else:
            output.append(f"{' ' * len(prefix)} {line}")


def diagnose(session: dict[str, Any], memory: MemoryTracker) -> list[str]:
    hints: list[str] = []

    heartbeat = int(session.get("agentHeartbeatAgeMs", -1) or -1)
    actions = int(session.get("agentActionCount", 0) or 0)
    phase = str(session.get("agentPhase", "") or "")
    intent = str(session.get("intentMode", "") or "")
    summary = compact_line(str(session.get("summary", "") or ""))
    note = compact_line(str(session.get("agentNote", "") or ""))
    target_visible = bool(session.get("targetVisible", False))
    projectile_threat = bool(session.get("projectileThreatActive", False))
    owner = str(session.get("controllerOwner", "") or "")
    source = str(session.get("controllerSource", "") or "")
    feedback_mode = str(session.get("feedbackIntentMode", "") or "")
    last_error = compact_line(str(session.get("agentLastError", "") or session.get("lastError", "") or ""))
    input_summary = compact_line(str(session.get("lastInputSummary", "") or ""))

    if last_error:
        hints.append(f"Erro do loop do Codex: {last_error}. Prioridade maxima e estabilizar isso antes de refinar comportamento.")

    has_agent_action = bool(session.get("hasAgentAction", False))

    if owner == "BrokerDefault" and not has_agent_action:
        hints.append("A sessao ainda esta no estado base do broker; aguardando a primeira acao executavel do agente.")
    elif owner != "Codex" or source == "waiting_for_codex":
        hints.append("O personagem ainda caiu em fallback em algum ponto. Vale medir por que o Codex nao publicou a acao util cedo o bastante.")

    if heartbeat >= 2500:
        hints.append("Heartbeat alto. Para o bot ficar mais esperto em luta rapida, reduza o prompt e publique intents curtas antes do plano completo.")
    elif heartbeat >= 0:
        hints.append("Saude do loop parece boa. O proximo ganho vem mais de qualidade de decisao do que de infraestrutura.")

    if actions <= 1 and phase == "thinking":
        hints.append("O bot ainda pensa demais antes de agir. Um bom upgrade e emitir uma acao provisoria e refinar nos ticks seguintes.")

    if target_visible and intent == "stabilize" and not projectile_threat:
        hints.append("O alvo esta visivel e o plano segue conservador. O prompt pode punir mais esse caso e puxar pressure ou punish.")

    if target_visible and intent == "pressure":
        hints.append("Bom sinal: ele esta enxergando janela de pressao. O proximo passo e variar a pressao com memoria do oponente.")

    if feedback_mode and feedback_mode != intent:
        hints.append(f"Ha desalinhamento entre o plano ({intent}) e o feedback da execucao ({feedback_mode}). Isso costuma pedir heuristica intermediaria melhor.")

    if "axis=+1.00" in input_summary or "axis=-1.00" in input_summary:
        hints.append("Ha movimento sustentado em uma direcao. Vale detector de anti-stall para forcar replanning quando o ganho real nao aparece.")

    if summary and summary != "-":
        hints.append(f"O broker resumiu a situacao como: {summary}. Esse resumo e uma boa base para memoria curta e telemetria de match.")

    if note and "Thinking for frame 0" in note:
        hints.append("O pensamento ainda parece muito stateless. Um upgrade importante e carregar ultimos intents, falhas e respostas do oponente.")

    for memory_hint in memory.smart_hints():
        if len(hints) >= 5:
            break
        hints.append(memory_hint)

    defaults = [
        "Adicionar memoria do oponente: frequencia de pulo, dash, defesa e anti-air para adaptar spacing e punishes.",
        "Medir resultado de cada intent: aproximou, tomou hit, travou ou gerou dano. Isso evita repetir planos ruins.",
        "Criar leitura de contexto de arena: canto, altura e linha de tiro. Isso melhora muito a troca entre pressure, zone e retreat.",
    ]

    for item in defaults:
        if len(hints) >= 5:
            break
        hints.append(item)

    return hints[:5]


def format_threats(session: dict[str, Any]) -> str:
    threats: list[str] = []
    if session.get("projectileThreatActive"):
        threats.append("projectile")
    if session.get("targetMeleeThreatActive"):
        threats.append("melee")
    if session.get("targetRangedThreatActive"):
        threats.append("ranged")
    if session.get("targetUltimateThreatActive"):
        threats.append("ultimate")
    if session.get("selfCornered"):
        threats.append("cornered")
    return ", ".join(threats) if threats else "none"


def build_session_view(
    session: dict[str, Any],
    memory: MemoryTracker,
    width: int,
    *,
    offline_error: str = "",
    stale_seconds: float = 0.0,
) -> str:
    output: list[str] = []
    add_title(output, width)

    model = str(session.get("agentModel", "-") or "-")
    phase = str(session.get("agentPhase", "-") or "-")
    intent = str(session.get("intentMode", "-") or "-")
    actions = int(session.get("agentActionCount", 0) or 0)
    heartbeat = format_ms(int(session.get("agentHeartbeatAgeMs", -1) or -1))
    summary_line = (
        f"STATUS {status_chip(session, offline=bool(offline_error))}  |  MODEL {model}  |  "
        f"PHASE {phase}  |  INTENT {intent}  |  ACTIONS {actions}  |  HEARTBEAT {heartbeat}"
    )
    output.extend(wrap_lines(summary_line, width))

    if offline_error:
        stale_text = f"Broker offline. Mostrando ultimo estado bom de {stale_seconds:.1f}s atras."
        output.append(colorize(stale_text, ANSI_YELLOW, bold=True))
        output.extend(wrap_lines(offline_error, width))

    output.append(rule("-", width))

    add_section("Now", output)
    current_thought = compact_line(str(session.get("agentNote", "") or ""))
    if not current_thought:
        current_thought = compact_line(str(session.get("intentReason", "") or "")) or "-"
    append_wrapped("Slot", str(session.get("slotId", "-") or "-"), width, output)
    append_wrapped("Bot", str(session.get("botDisplayName", "") or memory.bot_id), width, output)
    append_wrapped("Bot ID", str(session.get("botId", "") or memory.bot_id), width, output)
    append_wrapped("Intent", str(session.get("intentMode", "-") or "-"), width, output)
    append_wrapped("Reason", str(session.get("intentReason", "-") or "-"), width, output)
    append_wrapped("Thought", current_thought, width, output)
    append_wrapped("Summary", str(session.get("summary", "-") or "-"), width, output)
    output.append("")

    add_section("Live Read", output)
    append_wrapped("Owner", str(session.get("controllerOwner", "-") or "-"), width, output)
    append_wrapped("Source", str(session.get("controllerSource", "-") or "-"), width, output)
    append_wrapped("Target visible", "yes" if session.get("targetVisible") else "no", width, output)
    append_wrapped("Projectile risk", "yes" if session.get("projectileThreatActive") else "no", width, output)
    append_wrapped("Threats", format_threats(session), width, output)
    append_wrapped("Input echo", str(session.get("lastInputSummary", "-") or "-"), width, output)
    append_wrapped("Feedback", str(session.get("feedbackIntentReason", "-") or "-"), width, output)
    append_wrapped("Bot feedback", str(session.get("botFeedback", "-") or "-"), width, output)
    output.append("")

    add_section("Health", output)
    append_wrapped("Heartbeat", heartbeat, width, output)
    append_wrapped("Actions posted", str(actions), width, output)
    append_wrapped("Thinking", "yes" if session.get("agentThinking") else "no", width, output)
    append_wrapped("Intent age", format_ms(int(session.get("intentAgeMs", -1) or -1)), width, output)
    append_wrapped("Last error", str(session.get("agentLastError", "") or session.get("lastError", "") or "-"), width, output)
    output.append("")

    add_section("Learned Memory", output)
    for label, value in memory.profile_rows():
        append_wrapped(label, value, width, output)
    output.append("")

    add_section("Latest Death Review", output)
    for label, value in memory.latest_death_rows():
        append_wrapped(label, value, width, output)
    output.append("")

    add_section("Latest Round Review", output)
    for label, value in memory.latest_round_rows():
        append_wrapped(label, value, width, output)
    output.append("")

    add_section("Latest Series Review", output)
    for label, value in memory.latest_match_rows():
        append_wrapped(label, value, width, output)
    output.append("")

    add_section("Next Series Plan", output)
    for label, value in memory.latest_plan_rows():
        append_wrapped(label, value, width, output)
    output.append("")

    add_section("Recommended Improvements", output)
    for hint in diagnose(session, memory):
        wrapped = wrap_lines(hint, width - 4, indent="  - ")
        output.extend(wrapped)
    output.append("")

    add_section("Recent Live-Agent", output)
    agent_lines = read_recent_lines_from_paths(agent_log_paths(int(session.get("slotId", 0) or 0)), 3)
    if agent_lines:
        for line in agent_lines:
            output.extend(wrap_lines(line, width - 4, indent="  "))
    else:
        output.append("  -")
    output.append("")

    add_section("Recent Broker", output)
    broker_lines = read_recent_lines(BROKER_LOG, 3, ("[broker] slot=", "error", "failed"))
    if broker_lines:
        for line in broker_lines:
            output.extend(wrap_lines(line, width - 4, indent="  "))
    else:
        output.append("  -")
    output.append("")

    output.append(colorize(rule("=", width), ANSI_DIM))
    output.append(colorize("Feche esta janela apenas se nao quiser mais acompanhar o pensamento do bot.", ANSI_DIM))
    return "\n".join(output)


def build_waiting_view(memory: MemoryTracker, width: int) -> str:
    output: list[str] = []
    add_title(output, width)
    output.append("Status: aguardando a Unity publicar uma sessao ativa de bot.")
    output.append(f"Broker: {BROKER_BASE}")
    output.append("")

    add_section("Learned Memory", output)
    for label, value in memory.profile_rows():
        append_wrapped(label, value, width, output)
    output.append("")

    add_section("Latest Death Review", output)
    for label, value in memory.latest_death_rows():
        append_wrapped(label, value, width, output)
    output.append("")

    add_section("Latest Round Review", output)
    for label, value in memory.latest_round_rows():
        append_wrapped(label, value, width, output)
    output.append("")

    add_section("Latest Series Review", output)
    for label, value in memory.latest_match_rows():
        append_wrapped(label, value, width, output)
    output.append("")

    add_section("Next Series Plan", output)
    for label, value in memory.latest_plan_rows():
        append_wrapped(label, value, width, output)
    output.append("")

    add_section("Recent Live-Agent", output)
    for line in read_recent_lines_from_paths(agent_log_paths(memory.slot_id), 3):
        output.extend(wrap_lines(line, width - 4, indent="  "))
    output.append("")

    add_section("Recent Broker", output)
    broker_lines = read_recent_lines(BROKER_LOG, 3, ("[broker] active report", "[broker] slot=", "error", "failed"))
    if broker_lines:
        for line in broker_lines:
            output.extend(wrap_lines(line, width - 4, indent="  "))
    else:
        output.append("  -")
    output.append("")

    output.append(colorize(rule("=", width), ANSI_DIM))
    output.append(colorize("Feche esta janela apenas se nao quiser mais acompanhar o pensamento do bot.", ANSI_DIM))
    return "\n".join(output)


def build_offline_view(memory: MemoryTracker, width: int, error_message: str) -> str:
    output: list[str] = []
    add_title(output, width)
    output.append(colorize("Status: broker offline ou sem resposta.", ANSI_RED, bold=True))
    output.append(f"Broker: {BROKER_BASE}")
    output.append("")
    for line in wrap_lines(error_message or "-", width):
        output.append(line)
    output.append("")
    output.append("Dica: esta janela so faz sentido quando o mainbot.py estiver rodando.")
    output.append("")

    add_section("Learned Memory", output)
    for label, value in memory.profile_rows():
        append_wrapped(label, value, width, output)
    output.append("")

    add_section("Latest Death Review", output)
    for label, value in memory.latest_death_rows():
        append_wrapped(label, value, width, output)
    output.append("")

    add_section("Latest Round Review", output)
    for label, value in memory.latest_round_rows():
        append_wrapped(label, value, width, output)
    output.append("")

    add_section("Latest Series Review", output)
    for label, value in memory.latest_match_rows():
        append_wrapped(label, value, width, output)
    output.append("")

    add_section("Next Series Plan", output)
    for label, value in memory.latest_plan_rows():
        append_wrapped(label, value, width, output)
    output.append("")

    add_section("Recent Live-Agent", output)
    for line in read_recent_lines(AGENT_LOG, 3):
        output.extend(wrap_lines(line, width - 4, indent="  "))
    output.append("")

    output.append(colorize(rule("=", width), ANSI_DIM))
    output.append(colorize("Feche esta janela apenas se nao quiser mais acompanhar o pensamento do bot.", ANSI_DIM))
    return "\n".join(output)


def main() -> int:
    os.system("")
    manager = BotManager()
    memory_by_bot: dict[str, MemoryTracker] = {}
    last_render = ""
    last_good_sessions: list[dict[str, Any]] = []
    last_good_at = 0.0

    while True:
        width = terminal_width()
        manager.reload()
        status, payload = http_get_json("/report")
        if status == 200 and isinstance(payload, dict) and payload.get("ok"):
            report = payload.get("report") or {}
            sessions = report.get("agentSessions") or []
            if sessions:
                renders: list[str] = []
                last_good_sessions = list(sessions)
                last_good_at = time.time()
                for session in sessions:
                    slot_id = int(session.get("slotId", 0) or 0)
                    session_bot_id = str(session.get("botId", "") or "")
                    if not session_bot_id and slot_id > 0:
                        session_bot_id = manager.resolve_slot_bot(slot_id).get("botId", "")
                    if session_bot_id not in memory_by_bot:
                        memory_by_bot[session_bot_id] = MemoryTracker(bot_id=session_bot_id, slot_id=slot_id, manager=manager)
                    memory = memory_by_bot[session_bot_id]
                    agent_status, agent_payload = http_get_json(f"/agent/next?slotId={slot_id}")
                    if agent_status == 200 and isinstance(agent_payload, dict):
                        memory.update(agent_payload)
                    renders.append(build_session_view(session, memory, width))
                render = ("\n\n" + colorize(rule("=", width), ANSI_DIM) + "\n\n").join(renders)
            else:
                waiting_assignment = manager.list_active_assignments()
                fallback_bot_id = waiting_assignment[0]["botId"] if waiting_assignment else "bot-default"
                memory = memory_by_bot.setdefault(fallback_bot_id, MemoryTracker(bot_id=fallback_bot_id, manager=manager))
                render = build_waiting_view(memory, width)
        else:
            error_message = ""
            if isinstance(payload, dict):
                error_message = compact_line(str(payload.get("error", "") or ""))
            if last_good_sessions and (time.time() - last_good_at) <= STALE_HOLD_SECONDS:
                stale_seconds = time.time() - last_good_at
                stale_renders: list[str] = []
                for session in last_good_sessions:
                    slot_id = int(session.get("slotId", 0) or 0)
                    session_bot_id = str(session.get("botId", "") or "")
                    if not session_bot_id and slot_id > 0:
                        session_bot_id = manager.resolve_slot_bot(slot_id).get("botId", "")
                    memory = memory_by_bot.setdefault(session_bot_id, MemoryTracker(bot_id=session_bot_id, slot_id=slot_id, manager=manager))
                    stale_renders.append(build_session_view(session, memory, width, offline_error=error_message, stale_seconds=stale_seconds))
                render = ("\n\n" + colorize(rule("=", width), ANSI_DIM) + "\n\n").join(stale_renders)
            else:
                waiting_assignment = manager.list_active_assignments()
                fallback_bot_id = waiting_assignment[0]["botId"] if waiting_assignment else "bot-default"
                memory = memory_by_bot.setdefault(fallback_bot_id, MemoryTracker(bot_id=fallback_bot_id, manager=manager))
                render = build_offline_view(memory, width, error_message)

        if render != last_render:
            clear_screen()
            print(render, flush=True)
            last_render = render

        time.sleep(REFRESH_SECONDS)


if __name__ == "__main__":
    raise SystemExit(main())
