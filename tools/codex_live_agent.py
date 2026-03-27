import json
import os
import subprocess
import sys
import time
from pathlib import Path
from typing import Any
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen


BROKER_BASE = os.environ.get("CODEX_BROKER_BASE", "http://127.0.0.1:8765").rstrip("/")
SLOT_ID = int(os.environ.get("CODEX_AGENT_SLOT_ID", "2"))
POLL_INTERVAL_SECONDS = float(os.environ.get("CODEX_AGENT_POLL_INTERVAL_SEC", "0.18"))
IDLE_INTERVAL_SECONDS = float(os.environ.get("CODEX_AGENT_IDLE_INTERVAL_SEC", "0.75"))
TURN_TIMEOUT_SECONDS = float(os.environ.get("CODEX_AGENT_TURN_TIMEOUT_SEC", "25"))
CODEX_MODEL = os.environ.get("CODEX_MODEL", "")
TOOLS_DIR = Path(__file__).resolve().parent
CODEX_PATH = Path(os.environ.get("CODEX_EXE", r"C:\Users\user\.codex\.sandbox-bin\codex.exe"))
SYSTEM_PROMPT_PATH = TOOLS_DIR / "codex_broker_system_prompt.txt"
SCHEMA_PATH = TOOLS_DIR / "codex_broker_output_schema.json"
SYSTEM_PROMPT = SYSTEM_PROMPT_PATH.read_text(encoding="utf-8").strip()


VALID_MODES = {"pressure", "zone", "retreat", "punish", "stabilize"}
VALID_ANTI_PROJECTILE = {"hold", "jump", "dash", "parry_prefer"}


def log(message: str) -> None:
    print(f"[codex-live-agent] {message}", flush=True)


def http_get(path: str) -> tuple[int, Any]:
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


def http_post(path: str, payload: dict[str, Any]) -> tuple[int, Any]:
    raw = json.dumps(payload, ensure_ascii=True, separators=(",", ":")).encode("utf-8")
    request = Request(
        f"{BROKER_BASE}{path}",
        data=raw,
        headers={"Content-Type": "application/json"},
        method="POST",
    )
    try:
        with urlopen(request, timeout=3) as response:
            return response.status, json.loads(response.read().decode("utf-8"))
    except HTTPError as exc:
        body = exc.read().decode("utf-8", errors="replace")
        try:
            return exc.code, json.loads(body)
        except json.JSONDecodeError:
            return exc.code, {"ok": False, "error": body}
    except URLError as exc:
        return 0, {"ok": False, "error": str(exc.reason)}


def clamp01(value: Any, fallback: float) -> float:
    try:
        number = float(value)
    except (TypeError, ValueError):
        return fallback
    return max(0.0, min(1.0, number))


def validate_intent(candidate: Any) -> dict[str, Any] | None:
    if not isinstance(candidate, dict):
        return None

    mode = str(candidate.get("mode", "")).strip().lower()
    anti_projectile = str(candidate.get("antiProjectile", "")).strip().lower()
    if mode not in VALID_MODES or anti_projectile not in VALID_ANTI_PROJECTILE:
        return None

    try:
        preferred_range = max(0, int(candidate.get("preferredRange", 320)))
        focus_target_slot = int(candidate.get("focusTargetSlot", 1))
        expires_in_ms = max(100, int(candidate.get("expiresInMs", 400)))
    except (TypeError, ValueError):
        return None

    return {
        "mode": mode,
        "preferredRange": preferred_range,
        "advanceBias": clamp01(candidate.get("advanceBias"), 0.6),
        "shootBias": clamp01(candidate.get("shootBias"), 0.35),
        "meleeBias": clamp01(candidate.get("meleeBias"), 0.55),
        "dashBias": clamp01(candidate.get("dashBias"), 0.5),
        "jumpBias": clamp01(candidate.get("jumpBias"), 0.18),
        "antiProjectile": anti_projectile,
        "antiAir": bool(candidate.get("antiAir", True)),
        "punishRecovery": bool(candidate.get("punishRecovery", True)),
        "cornerEscapeBias": clamp01(candidate.get("cornerEscapeBias"), 0.35),
        "focusTargetSlot": focus_target_slot,
        "expiresInMs": expires_in_ms,
        "reason": str(candidate.get("reason", "")).strip()[:160],
    }


def run_codex_command(command: list[str], capture_thread_id: bool) -> tuple[str | None, dict[str, Any] | None, str]:
    try:
        completed = subprocess.run(
            command,
            capture_output=True,
            text=True,
            encoding="utf-8",
            timeout=TURN_TIMEOUT_SECONDS,
            check=False,
        )
    except subprocess.TimeoutExpired:
        return None, None, "codex_timeout"
    except OSError as exc:
        return None, None, f"codex_exec_failed:{exc}"

    thread_id = None
    final_text = None
    for raw_line in completed.stdout.splitlines():
        try:
            event = json.loads(raw_line)
        except json.JSONDecodeError:
            continue

        if capture_thread_id and event.get("type") == "thread.started":
            thread_id = event.get("thread_id")

        if event.get("type") == "item.completed":
            item = event.get("item") or {}
            if item.get("type") == "agent_message" and item.get("text"):
                final_text = item["text"]

    if not final_text:
        return thread_id, None, "missing_agent_message"

    try:
        parsed = json.loads(final_text)
    except json.JSONDecodeError:
        return thread_id, None, "invalid_json_response"

    intent = validate_intent(parsed)
    if intent is None:
        return thread_id, None, "invalid_schema_response"

    return thread_id, intent, ""


def run_codex_new(prompt: str) -> tuple[str | None, dict[str, Any] | None, str]:
    command = [
        str(CODEX_PATH),
        "exec",
        "--json",
        "--skip-git-repo-check",
        "--sandbox",
        "read-only",
        "--cd",
        str(TOOLS_DIR),
        "--output-schema",
        str(SCHEMA_PATH),
    ]
    if CODEX_MODEL:
        command.extend(["--model", CODEX_MODEL])
    command.append(prompt)
    return run_codex_command(command, capture_thread_id=True)


def run_codex_resume(session_id: str, prompt: str) -> tuple[dict[str, Any] | None, str]:
    command = [
        str(CODEX_PATH),
        "exec",
        "resume",
        session_id,
        "--json",
        "--skip-git-repo-check",
    ]
    if CODEX_MODEL:
        command.extend(["--model", CODEX_MODEL])
    command.append(prompt)
    _, parsed, error = run_codex_command(command, capture_thread_id=False)
    return parsed, error


def build_start_prompt(payload: dict[str, Any]) -> str:
    compact = json.dumps(payload, ensure_ascii=True, separators=(",", ":"))
    return (
        f"{SYSTEM_PROMPT}\n\n"
        "You are now the live external player for slot 2 in an ongoing match.\n"
        "Return only one tactical intent JSON object.\n"
        "Be aggressive enough to kill the opponent. Do not idle.\n"
        "If the target is visible and not forcing immediate defense, prefer pressure or punish over stabilize.\n"
        "Use stabilize only for genuine danger states such as corner escape, low-health reset, or active projectile threat.\n"
        "State payload:\n"
        f"{compact}\n"
    )


def build_tick_prompt(payload: dict[str, Any]) -> str:
    compact = json.dumps(payload, ensure_ascii=True, separators=(",", ":"))
    return (
        "You are still controlling the same live fighter in the same match.\n"
        "Update the tactical intent for the next short horizon.\n"
        "Do not return safe defaults unless the state truly demands it.\n"
        "If the opponent is targetable, bias toward plans that can actually create attacks soon.\n"
        "Avoid repeated stabilize outputs when the last inputs produced no offense.\n"
        "Return only one JSON object matching the schema.\n"
        "State payload:\n"
        f"{compact}\n"
    )


def build_warmup_prompt() -> str:
    return (
        f"{SYSTEM_PROMPT}\n\n"
        "This is a warmup turn for a future live match.\n"
        "Return one aggressive but stable default intent JSON object for a generic neutral opening.\n"
        "Assume the target is visible at mid range and can be pressured.\n"
    )


def format_prompt_payload(state: dict[str, Any]) -> dict[str, Any]:
    prompt_state = state.get("promptState") or {}
    feedback = state.get("executorFeedback") or {}
    return {
        "slotId": state.get("slotId", SLOT_ID),
        "sessionId": state.get("sessionId", ""),
        "frame": state.get("frame", -1),
        "forceRefresh": bool(state.get("forceRefresh", False)),
        "self": prompt_state.get("self") or {},
        "target": prompt_state.get("target") or {},
        "arena": prompt_state.get("arena") or {},
        "dangerousProjectiles": prompt_state.get("dangerousProjectiles") or [],
        "events": prompt_state.get("events") or [],
        "memory": prompt_state.get("memory") or [],
        "executorFeedback": feedback,
        "lastIntent": state.get("lastIntent") or {},
    }


def should_request_turn(state: dict[str, Any], last_frame: int, last_turn_at: float) -> bool:
    frame = int(state.get("frame", -1))
    if frame < 0 or frame == last_frame:
        return False
    if bool(state.get("forceRefresh", False)):
        return True
    if last_frame < 0:
        return True
    if frame - last_frame >= 12:
        return True
    return time.time() - last_turn_at >= 0.55


def post_intent(session_id: str, intent: dict[str, Any]) -> bool:
    status, payload = http_post(
        "/agent/action",
        {
            "sessionId": session_id,
            "intent": intent,
        },
    )
    return status == 200 and isinstance(payload, dict)


def main() -> int:
    if not CODEX_PATH.exists():
        log(f"codex executable not found: {CODEX_PATH}")
        return 1

    log(f"starting live agent for slot {SLOT_ID} via {BROKER_BASE}")
    codex_session_id = ""
    broker_session_id = ""
    last_frame = -1
    last_turn_at = 0.0

    thread_id, warmup_intent, warmup_error = run_codex_new(build_warmup_prompt())
    if thread_id and warmup_intent is not None:
        codex_session_id = thread_id
        log(f"warmup ready session={codex_session_id[:8]} mode={warmup_intent['mode']}")
    else:
        log(f"warmup skipped error={warmup_error or 'unknown'}")

    while True:
        status, state = http_get(f"/agent/next?slotId={SLOT_ID}")
        if status == 404:
            if broker_session_id:
                log("broker session ended; waiting for a new one")
            broker_session_id = ""
            codex_session_id = ""
            last_frame = -1
            last_turn_at = 0.0
            time.sleep(IDLE_INTERVAL_SECONDS)
            continue

        if status != 200 or not isinstance(state, dict) or not state.get("ok", True):
            log(f"broker poll failed: status={status} error={state.get('error') if isinstance(state, dict) else state}")
            time.sleep(IDLE_INTERVAL_SECONDS)
            continue

        current_broker_session = str(state.get("sessionId", "")).strip()
        if not current_broker_session:
            time.sleep(IDLE_INTERVAL_SECONDS)
            continue

        if current_broker_session != broker_session_id:
            broker_session_id = current_broker_session
            last_frame = -1
            last_turn_at = 0.0
            log(f"attached to broker session {broker_session_id}")

        if not should_request_turn(state, last_frame, last_turn_at):
            time.sleep(POLL_INTERVAL_SECONDS)
            continue

        payload = format_prompt_payload(state)
        if not codex_session_id:
            thread_id, intent, error = run_codex_new(build_start_prompt(payload))
            if not thread_id or intent is None:
                log(f"codex start failed: {error}")
                time.sleep(IDLE_INTERVAL_SECONDS)
                continue
            codex_session_id = thread_id
            log(f"codex session started {codex_session_id[:8]} mode={intent['mode']} reason={intent['reason']}")
        else:
            intent, error = run_codex_resume(codex_session_id, build_tick_prompt(payload))
            if intent is None:
                log(f"codex resume failed: {error}")
                time.sleep(POLL_INTERVAL_SECONDS)
                continue

        last_frame = int(state.get("frame", -1))
        last_turn_at = time.time()
        if post_intent(broker_session_id, intent):
            log(f"posted action frame={last_frame} mode={intent['mode']} reason={intent['reason']}")
        else:
            log("failed to post action to broker")

        time.sleep(POLL_INTERVAL_SECONDS)


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except KeyboardInterrupt:
        log("stopped")
        raise SystemExit(0)
