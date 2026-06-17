import argparse
import json
import os
import time
from collections import deque
from typing import Any
from urllib.error import HTTPError, URLError
from urllib.request import urlopen


BROKER_BASE = os.environ.get("CODEX_BROKER_BASE", "http://127.0.0.1:8765").rstrip("/")
REFRESH_SECONDS = float(os.environ.get("CODEX_SLOT_CONSOLE_REFRESH_SECONDS", "0.35"))


def clear_screen() -> None:
    os.system("cls" if os.name == "nt" else "clear")


def compact_line(value: str) -> str:
    return " ".join((value or "").strip().split())


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


def find_session(report: dict[str, Any], slot_id: int) -> dict[str, Any] | None:
    for session in report.get("agentSessions", []):
        if int(session.get("slotId", 0) or 0) == slot_id:
            return session
    return None


def meter(value: int, width: int = 10) -> str:
    clamped = max(0, min(100, value))
    filled = int(round((clamped / 100.0) * width))
    return "[" + ("#" * filled) + ("-" * (width - filled)) + f"] {clamped:3d}"


def compute_pressure(session: dict[str, Any]) -> int:
    mode = str(session.get("intentMode", "") or "")
    base = {
        "punish": 88,
        "pressure": 76,
        "zone": 52,
        "stabilize": 28,
        "retreat": 18,
    }.get(mode, 35)
    if session.get("targetVisible"):
        base += 8
    if session.get("projectileThreatActive"):
        base -= 18
    return max(0, min(100, base))


def compute_danger(session: dict[str, Any]) -> int:
    score = 8
    if session.get("projectileThreatActive"):
        score += 35
    if str(session.get("controllerOwner", "")) != "Codex":
        score += 25
    if str(session.get("agentPhase", "")) == "error":
        score += 30
    if compact_line(str(session.get("agentLastError", "") or session.get("lastError", "") or "")):
        score += 25
    return max(0, min(100, score))


def compute_adaptation(session: dict[str, Any], recent_events: deque[str]) -> int:
    score = min(40, int(session.get("agentActionCount", 0) or 0) * 12)
    score += min(35, len(recent_events) * 6)
    if str(session.get("intentMode", "")) in {"pressure", "punish"}:
        score += 12
    return max(0, min(100, score))


def detect_events(previous: dict[str, Any] | None, current: dict[str, Any]) -> list[str]:
    if previous is None:
        return ["Sessao conectada ao broker."]
    events: list[str] = []
    if previous.get("intentMode") != current.get("intentMode"):
        events.append(f"Plano mudou para {current.get('intentMode') or '-'}")
    if previous.get("controllerOwner") != current.get("controllerOwner"):
        events.append(f"Controle agora: {current.get('controllerOwner') or '-'}")
    if int(current.get("agentActionCount", 0) or 0) > int(previous.get("agentActionCount", 0) or 0):
        events.append("Nova acao enviada ao broker")
    if compact_line(str(current.get("agentLastError", "") or current.get("lastError", "") or "")) and current.get("agentLastError") != previous.get("agentLastError"):
        events.append("Erro novo no loop do Codex")
    if int(current.get("playerOneWins", 0) or 0) != int(previous.get("playerOneWins", 0) or 0) or int(current.get("playerTwoWins", 0) or 0) != int(previous.get("playerTwoWins", 0) or 0):
        events.append(
            f"Score mudou: {current.get('playerOneWins', 0)} x {current.get('playerTwoWins', 0)}"
        )
    return events


def print_view(slot_id: int, session: dict[str, Any] | None, recent_events: deque[str], error: str = "") -> None:
    clear_screen()
    print(f"BOT SLOT {slot_id} OVERLAY")
    print("=" * 64)
    if error:
        print(f"Broker offline: {error}")
        return
    if session is None:
        print("Aguardando sessao do Unity...")
        return

    name = session.get("botDisplayName") or session.get("botId") or f"Slot {slot_id}"
    print(f"Bot: {name}")
    print(f"Model: {session.get('agentModel') or '-'}")
    print(f"Phase: {session.get('agentPhase') or '-'} | Owner: {session.get('controllerOwner') or '-'}")
    print(f"Score: {session.get('playerOneWins', 0)} x {session.get('playerTwoWins', 0)} | First to {session.get('roundsToChampion', 0) or 5}")
    print(f"Intent: {session.get('intentMode') or '-'}")
    print(f"Why: {compact_line(str(session.get('intentReason') or session.get('agentNote') or '-'))}")
    print(f"Bot feedback: {compact_line(str(session.get('botFeedback') or '-'))}")
    print()
    print(f"Pressure   {meter(compute_pressure(session))}")
    print(f"Danger     {meter(compute_danger(session))}")
    print(f"Adaptation {meter(compute_adaptation(session, recent_events))}")
    print()
    print("Eventos:")
    if recent_events:
        for item in list(recent_events)[-3:]:
            print(f"- {item}")
    else:
        print("- Sem eventos ainda")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--slot", type=int, required=True)
    args = parser.parse_args()

    previous: dict[str, Any] | None = None
    recent_events: deque[str] = deque(maxlen=6)

    while True:
        status, payload = http_get_json("/report")
        if status != 200 or not isinstance(payload, dict) or not payload.get("ok"):
            print_view(args.slot, None, recent_events, error=str(payload.get("error", "unknown")) if isinstance(payload, dict) else "unknown")
            time.sleep(REFRESH_SECONDS)
            continue
        report = payload.get("report") or {}
        session = find_session(report, args.slot)
        if session is not None:
            for event in detect_events(previous, session):
                if event and (not recent_events or recent_events[-1] != event):
                    recent_events.append(event)
        previous = session
        print_view(args.slot, session, recent_events)
        time.sleep(REFRESH_SECONDS)


if __name__ == "__main__":
    raise SystemExit(main())
