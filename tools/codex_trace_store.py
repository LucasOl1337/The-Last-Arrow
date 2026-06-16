import json
import os
import re
import time
from pathlib import Path
from typing import Any


TOOLS_DIR = Path(__file__).resolve().parent
TRACE_DIR = TOOLS_DIR / "bot_memory" / "traces"
TRACE_FILE = TRACE_DIR / "trace_events.jsonl"
MAX_TRACE_FILE_BYTES = int(os.environ.get("CODEX_TRACE_MAX_BYTES", str(5 * 1024 * 1024)))
MAX_TRACE_STRING_CHARS = int(os.environ.get("CODEX_TRACE_MAX_STRING_CHARS", "4096"))
MAX_TRACE_LIST_ITEMS = int(os.environ.get("CODEX_TRACE_MAX_LIST_ITEMS", "50"))
MAX_TRACE_DEPTH = int(os.environ.get("CODEX_TRACE_MAX_DEPTH", "8"))
REDACTED_VALUE = "[REDACTED]"
SECRET_PATTERN = re.compile(
    r"(?i)(bearer\s+)[a-z0-9._~+/=-]{8,}|(sk-[a-z0-9_-]{8,})"
)
SENSITIVE_KEY_PARTS = (
    "apikey",
    "authorization",
    "password",
    "secret",
    "accesstoken",
    "refreshtoken",
    "bearer",
)


def now_iso() -> str:
    return time.strftime("%Y-%m-%dT%H:%M:%S")


def append_trace_event(event_type: str, payload: dict[str, Any]) -> None:
    TRACE_DIR.mkdir(parents=True, exist_ok=True)
    rotate_trace_file_if_needed()
    envelope = {
        "timestamp": now_iso(),
        "eventType": event_type,
        "payload": sanitize_trace_payload(payload),
    }
    with TRACE_FILE.open("a", encoding="utf-8") as handle:
        handle.write(json.dumps(envelope, ensure_ascii=True) + "\n")


def sanitize_trace_payload(payload: dict[str, Any]) -> dict[str, Any]:
    if not isinstance(payload, dict):
        return {}
    sanitized = sanitize_trace_value(payload)
    return sanitized if isinstance(sanitized, dict) else {}


def sanitize_trace_value(value: Any, key: str = "", depth: int = 0) -> Any:
    if is_sensitive_trace_key(key):
        return REDACTED_VALUE

    if depth > MAX_TRACE_DEPTH:
        return "[truncated max depth]"

    if isinstance(value, dict):
        return {
            str(child_key): sanitize_trace_value(child_value, str(child_key), depth + 1)
            for child_key, child_value in value.items()
        }

    if isinstance(value, (list, tuple)):
        return sanitize_trace_sequence(value, depth)

    if isinstance(value, str):
        return truncate_trace_string(scrub_trace_string(value))

    if value is None or isinstance(value, (bool, int, float)):
        return value

    return truncate_trace_string(scrub_trace_string(str(value)))


def sanitize_trace_sequence(values: list[Any] | tuple[Any, ...], depth: int) -> list[Any]:
    if MAX_TRACE_LIST_ITEMS < 0:
        limit = len(values)
    else:
        limit = min(len(values), MAX_TRACE_LIST_ITEMS)

    sanitized = [sanitize_trace_value(item, depth=depth + 1) for item in values[:limit]]
    remaining = len(values) - limit
    if remaining > 0:
        sanitized.append(f"[truncated {remaining} items]")
    return sanitized


def is_sensitive_trace_key(key: str) -> bool:
    normalized = re.sub(r"[^a-z0-9]", "", key.lower())
    return any(part in normalized for part in SENSITIVE_KEY_PARTS)


def scrub_trace_string(value: str) -> str:
    return SECRET_PATTERN.sub(replace_secret_match, value)


def replace_secret_match(match: re.Match[str]) -> str:
    bearer_prefix = match.group(1)
    if bearer_prefix:
        return f"{bearer_prefix}[REDACTED]"
    return REDACTED_VALUE


def truncate_trace_string(value: str) -> str:
    if MAX_TRACE_STRING_CHARS < 0 or len(value) <= MAX_TRACE_STRING_CHARS:
        return value
    remaining = len(value) - MAX_TRACE_STRING_CHARS
    return f"{value[:MAX_TRACE_STRING_CHARS]}... [truncated {remaining} chars]"


def rotate_trace_file_if_needed() -> None:
    if MAX_TRACE_FILE_BYTES <= 0 or not TRACE_FILE.exists():
        return

    try:
        if TRACE_FILE.stat().st_size < MAX_TRACE_FILE_BYTES:
            return
    except OSError:
        return

    archive_path = TRACE_FILE.with_suffix(".1.jsonl")
    try:
        if archive_path.exists():
            archive_path.unlink()
        TRACE_FILE.replace(archive_path)
    except OSError:
        return


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
