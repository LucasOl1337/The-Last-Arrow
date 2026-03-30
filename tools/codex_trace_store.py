import json
import time
from pathlib import Path
from typing import Any


TOOLS_DIR = Path(__file__).resolve().parent
TRACE_DIR = TOOLS_DIR / "bot_memory" / "traces"
TRACE_FILE = TRACE_DIR / "trace_events.jsonl"


def now_iso() -> str:
    return time.strftime("%Y-%m-%dT%H:%M:%S")


def append_trace_event(event_type: str, payload: dict[str, Any]) -> None:
    TRACE_DIR.mkdir(parents=True, exist_ok=True)
    envelope = {
        "timestamp": now_iso(),
        "eventType": event_type,
        "payload": payload,
    }
    with TRACE_FILE.open("a", encoding="utf-8") as handle:
        handle.write(json.dumps(envelope, ensure_ascii=True) + "\n")


def read_trace_events(limit: int = 200, slot_id: int = 0) -> list[dict[str, Any]]:
    if not TRACE_FILE.exists():
        return []
    try:
        lines = [line for line in TRACE_FILE.read_text(encoding="utf-8", errors="replace").splitlines() if line.strip()]
    except OSError:
        return []

    events: list[dict[str, Any]] = []
    for line in reversed(lines):
        try:
            event = json.loads(line)
        except json.JSONDecodeError:
            continue
        if not isinstance(event, dict):
            continue
        payload = event.get("payload", {})
        if slot_id > 0 and int((payload or {}).get("slotId", 0) or 0) != slot_id:
            continue
        events.append(event)
        if len(events) >= limit:
            break
    events.reverse()
    return events
