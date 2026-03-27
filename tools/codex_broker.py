import json
import os
import subprocess
import threading
import time
from copy import deepcopy
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import parse_qs, urlparse
from pathlib import Path
from typing import Any


BROKER_HOST = os.environ.get("CODEX_BROKER_HOST", "127.0.0.1")
BROKER_PORT = int(os.environ.get("CODEX_BROKER_PORT", "8765"))
CODEX_PATH = Path(os.environ.get("CODEX_EXE", r"C:\Users\user\.codex\.sandbox-bin\codex.exe"))
TOOLS_DIR = Path(__file__).resolve().parent
PROMPT_PATH = TOOLS_DIR / "codex_broker_system_prompt.txt"
SCHEMA_PATH = TOOLS_DIR / "codex_broker_output_schema.json"
CODEX_TIMEOUT_SECONDS = float(os.environ.get("CODEX_TURN_TIMEOUT_SEC", "45"))
CODEX_MODEL = os.environ.get("CODEX_MODEL", "")
REPORT_INTERVAL_SECONDS = float(os.environ.get("CODEX_BROKER_REPORT_INTERVAL_SEC", "1.0"))
REPORT_HEARTBEAT_SECONDS = float(os.environ.get("CODEX_BROKER_REPORT_HEARTBEAT_SEC", "5.0"))
SYSTEM_PROMPT = PROMPT_PATH.read_text(encoding="utf-8").strip()

DEFAULT_INTENT = {
    "mode": "stabilize",
    "preferredRange": 320,
    "advanceBias": 0.5,
    "shootBias": 0.5,
    "meleeBias": 0.5,
    "dashBias": 0.5,
    "jumpBias": 0.35,
    "antiProjectile": "hold",
    "antiAir": True,
    "punishRecovery": True,
    "cornerEscapeBias": 0.65,
    "focusTargetSlot": 2,
    "expiresInMs": 400,
    "reason": "default_safe_plan",
}

VALID_MODES = {"pressure", "zone", "retreat", "punish", "stabilize"}
VALID_ANTI_PROJECTILE = {"hold", "jump", "dash", "parry_prefer"}


def now_ms() -> int:
    return int(time.time() * 1000)


def clamp01(value: Any, fallback: float) -> float:
    try:
        number = float(value)
    except (TypeError, ValueError):
        return fallback
    return max(0.0, min(1.0, number))


def describe_controller(source: str) -> str:
    normalized = (source or "").strip().lower()
    if normalized.startswith("codex_"):
        return "Codex"
    if normalized == "heuristic_fallback":
        return "LocalHeuristic"
    if normalized == "human":
        return "Human"
    return normalized or "Unknown"


def compact_input(input_payload: dict[str, Any] | None) -> str:
    if not isinstance(input_payload, dict):
        return "-"

    buttons: list[str] = []
    if input_payload.get("jumpPressed"):
        buttons.append("JUMP")
    if input_payload.get("shootPressed") or input_payload.get("shootHeld"):
        buttons.append("SHOOT")
    if input_payload.get("meleePressed"):
        buttons.append("MELEE")
    if input_payload.get("ultimatePressed"):
        buttons.append("ULT")
    if input_payload.get("dashPrimaryPressed") or input_payload.get("dashSecondaryPressed"):
        buttons.append("DASH")

    aim = input_payload.get("aim") or {}
    axis = float(input_payload.get("axis", 0.0))
    aim_x = float(aim.get("x", 0.0)) if isinstance(aim, dict) else 0.0
    aim_y = float(aim.get("y", 0.0)) if isinstance(aim, dict) else 0.0
    button_text = ",".join(buttons) if buttons else "-"
    return f"axis={axis:+.2f} aim=({aim_x:+.2f},{aim_y:+.2f}) btns={button_text}"


def log_event(label: str, **fields: Any) -> None:
    parts = [f"{key}={value}" for key, value in fields.items()]
    suffix = f" | {' '.join(parts)}" if parts else ""
    print(f"[broker] {label}{suffix}", flush=True)


def validate_intent(candidate: Any) -> dict[str, Any] | None:
    if not isinstance(candidate, dict):
        return None

    mode = str(candidate.get("mode", "")).strip().lower()
    anti_projectile = str(candidate.get("antiProjectile", "")).strip().lower()
    if mode not in VALID_MODES or anti_projectile not in VALID_ANTI_PROJECTILE:
        return None

    try:
        preferred_range = max(0, int(candidate.get("preferredRange", DEFAULT_INTENT["preferredRange"])))
        focus_target_slot = int(candidate.get("focusTargetSlot", DEFAULT_INTENT["focusTargetSlot"]))
        expires_in_ms = max(100, int(candidate.get("expiresInMs", DEFAULT_INTENT["expiresInMs"])))
    except (TypeError, ValueError):
        return None

    return {
        "mode": mode,
        "preferredRange": preferred_range,
        "advanceBias": clamp01(candidate.get("advanceBias"), DEFAULT_INTENT["advanceBias"]),
        "shootBias": clamp01(candidate.get("shootBias"), DEFAULT_INTENT["shootBias"]),
        "meleeBias": clamp01(candidate.get("meleeBias"), DEFAULT_INTENT["meleeBias"]),
        "dashBias": clamp01(candidate.get("dashBias"), DEFAULT_INTENT["dashBias"]),
        "jumpBias": clamp01(candidate.get("jumpBias"), DEFAULT_INTENT["jumpBias"]),
        "antiProjectile": anti_projectile,
        "antiAir": bool(candidate.get("antiAir", DEFAULT_INTENT["antiAir"])),
        "punishRecovery": bool(candidate.get("punishRecovery", DEFAULT_INTENT["punishRecovery"])),
        "cornerEscapeBias": clamp01(candidate.get("cornerEscapeBias"), DEFAULT_INTENT["cornerEscapeBias"]),
        "focusTargetSlot": focus_target_slot,
        "expiresInMs": expires_in_ms,
        "reason": str(candidate.get("reason", "")).strip()[:160],
    }


def build_start_prompt(prompt_state: dict[str, Any]) -> str:
    payload = json.dumps(prompt_state, ensure_ascii=True, separators=(",", ":"))
    return (
        f"{SYSTEM_PROMPT}\n\n"
        "You are now taking control of the fighter for an indefinite live match.\n"
        "Keep a persistent internal model of the opponent and adapt over time.\n"
        "Return the opening tactical intent as one JSON object matching the schema.\n"
        "Current translated combat state:\n"
        f"{payload}\n"
    )


def build_tick_prompt(prompt_state: dict[str, Any], executor_feedback: dict[str, Any], force_refresh: bool) -> str:
    payload = json.dumps(
        {
            "promptState": prompt_state,
            "executorFeedback": executor_feedback,
            "forceRefresh": force_refresh,
        },
        ensure_ascii=True,
        separators=(",", ":"),
    )
    return (
        "You are still controlling the same live fighter in the same ongoing session.\n"
        "Update the tactical intent for the next short horizon.\n"
        "Keep the response as one JSON object only.\n"
        "Live update payload:\n"
        f"{payload}\n"
    )


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


def run_codex_command(command: list[str], capture_thread_id: bool) -> tuple[str | None, dict[str, Any] | None, str]:
    try:
        completed = subprocess.run(
            command,
            capture_output=True,
            text=True,
            encoding="utf-8",
            timeout=CODEX_TIMEOUT_SECONDS,
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


class BrokerSession:
    def __init__(self, slot_id: int, session_id: str, initial_intent: dict[str, Any] | None):
        self.slot_id = slot_id
        self.session_id = session_id
        self.lock = threading.Lock()
        self.cached_intent = deepcopy(initial_intent) if initial_intent else deepcopy(DEFAULT_INTENT)
        self.cached_at_ms = now_ms()
        self.generated_at_frame = -1
        self.status = "ready"
        self.last_error = ""
        self.inflight = False
        self.pending_payload: dict[str, Any] | None = None

    def snapshot(self) -> dict[str, Any]:
        with self.lock:
            return {
                "status": self.status,
                "sessionId": self.session_id,
                "generatedAtUnixMs": self.cached_at_ms,
                "generatedAtFrame": self.generated_at_frame,
                "isFresh": True,
                "intent": deepcopy(self.cached_intent),
                "error": self.last_error,
            }

    def queue_tick(self, payload: dict[str, Any]) -> None:
        with self.lock:
            if self.inflight:
                self.pending_payload = payload
                return

            self.inflight = True

        worker = threading.Thread(target=self._run_tick_loop, args=(payload,), daemon=True)
        worker.start()

    def reset(self, reason: str) -> None:
        with self.lock:
            self.status = "reset"
            self.last_error = reason
            self.pending_payload = None

    def _run_tick_loop(self, payload: dict[str, Any]) -> None:
        current_payload = payload
        while current_payload is not None:
            prompt = build_tick_prompt(
                current_payload.get("promptState") or {},
                current_payload.get("executorFeedback") or {},
                bool(current_payload.get("forceRefresh")),
            )
            parsed, error = run_codex_resume(self.session_id, prompt)
            with self.lock:
                if parsed is not None:
                    self.cached_intent = parsed
                    self.cached_at_ms = now_ms()
                    self.generated_at_frame = int(current_payload.get("frame", -1))
                    self.status = "updated"
                    self.last_error = ""
                else:
                    self.status = "stale"
                    self.last_error = error

                current_payload = self.pending_payload
                self.pending_payload = None
                if current_payload is None:
                    self.inflight = False


class AgentDrivenSession:
    def __init__(self, slot_id: int, session_id: str, initial_prompt_state: dict[str, Any]):
        self.slot_id = slot_id
        self.session_id = session_id
        self.lock = threading.Lock()
        self.prompt_state = deepcopy(initial_prompt_state)
        self.executor_feedback: dict[str, Any] = {}
        self.updated_at_ms = now_ms()
        self.frame = int(initial_prompt_state.get("frame", -1))
        self.force_refresh = True
        self.cached_intent = deepcopy(DEFAULT_INTENT)
        self.intent_updated_at_ms = 0
        self.agent_action_count = 0
        self.last_error = ""
        self.reset_reason = ""
        self.stopped = False

    def state_payload(self) -> dict[str, Any]:
        with self.lock:
            return {
                "ok": True,
                "sessionId": self.session_id,
                "slotId": self.slot_id,
                "frame": self.frame,
                "updatedAtUnixMs": self.updated_at_ms,
                "forceRefresh": self.force_refresh,
                "promptState": deepcopy(self.prompt_state),
                "executorFeedback": deepcopy(self.executor_feedback),
                "lastIntent": deepcopy(self.cached_intent),
                "lastIntentUpdatedAtUnixMs": self.intent_updated_at_ms,
                "lastError": self.last_error,
                "resetReason": self.reset_reason,
                "stopped": self.stopped,
                "agentActionCount": self.agent_action_count,
            }

    def report_payload(self) -> dict[str, Any]:
        with self.lock:
            input_payload = deepcopy((self.executor_feedback or {}).get("reportedInput") or {})
            source = str((self.executor_feedback or {}).get("source", "")).strip()
            has_agent_action = self.agent_action_count > 0 and self.intent_updated_at_ms > 0
            controller_owner = describe_controller(source)
            if source.startswith("codex_") and not has_agent_action:
                controller_owner = "BrokerDefault"
            return {
                "sessionId": self.session_id,
                "slotId": self.slot_id,
                "frame": self.frame,
                "updatedAtUnixMs": self.updated_at_ms,
                "intentUpdatedAtUnixMs": self.intent_updated_at_ms,
                "forceRefresh": self.force_refresh,
                "stopped": self.stopped,
                "controllerSource": source or "unknown",
                "controllerOwner": controller_owner,
                "summary": str((self.executor_feedback or {}).get("summary", "")),
                "intentMode": str((self.cached_intent or {}).get("mode", "")),
                "intentReason": str((self.cached_intent or {}).get("reason", "")),
                "feedbackIntentMode": str((self.executor_feedback or {}).get("intentMode", "")),
                "feedbackIntentReason": str((self.executor_feedback or {}).get("intentReason", "")),
                "intentAgeMs": (self.executor_feedback or {}).get("intentAgeMs", -1),
                "projectileThreatActive": bool((self.executor_feedback or {}).get("projectileThreatActive", False)),
                "targetVisible": bool((self.executor_feedback or {}).get("targetVisible", False)),
                "roundResetPending": bool((self.executor_feedback or {}).get("roundResetPending", False)),
                "lastInput": input_payload,
                "lastInputSummary": compact_input(input_payload),
                "agentActionCount": self.agent_action_count,
                "hasAgentAction": has_agent_action,
                "lastError": self.last_error,
                "resetReason": self.reset_reason,
            }

    def publish_state(self, payload: dict[str, Any]) -> dict[str, Any]:
        with self.lock:
            self.frame = int(payload.get("frame", self.frame))
            self.force_refresh = bool(payload.get("forceRefresh", False))
            self.prompt_state = deepcopy(payload.get("promptState") or self.prompt_state)
            self.executor_feedback = deepcopy(payload.get("executorFeedback") or self.executor_feedback)
            self.updated_at_ms = now_ms()
            self.reset_reason = ""
            return self.intent_envelope()

    def publish_action(self, intent: dict[str, Any]) -> dict[str, Any]:
        with self.lock:
            self.cached_intent = deepcopy(intent)
            self.intent_updated_at_ms = now_ms()
            self.agent_action_count += 1
            self.last_error = ""
            self.force_refresh = False
            return self.intent_envelope()

    def reset(self, reason: str) -> None:
        with self.lock:
            self.reset_reason = reason
            self.force_refresh = True

    def stop(self) -> None:
        with self.lock:
            self.stopped = True

    def intent_envelope(self) -> dict[str, Any]:
        has_agent_action = self.agent_action_count > 0 and self.intent_updated_at_ms > 0
        return {
            "status": "ready",
            "sessionId": self.session_id,
            "generatedAtUnixMs": self.intent_updated_at_ms,
            "generatedAtFrame": self.frame,
            "isFresh": self.intent_updated_at_ms > 0,
            "hasAgentAction": has_agent_action,
            "controllerOwner": "Codex" if has_agent_action else "BrokerDefault",
            "intent": deepcopy(self.cached_intent),
            "error": self.last_error,
        }


SESSIONS: dict[str, BrokerSession] = {}
SESSIONS_LOCK = threading.Lock()
AGENT_SESSIONS: dict[str, AgentDrivenSession] = {}
AGENT_SESSION_BY_SLOT: dict[int, str] = {}
AGENT_LOCK = threading.Lock()


def collect_report_snapshot() -> dict[str, Any]:
    with AGENT_LOCK:
        agent_sessions = [session.report_payload() for session in AGENT_SESSIONS.values()]

    with SESSIONS_LOCK:
        strategy_sessions = list(SESSIONS.keys())

    agent_sessions.sort(key=lambda item: (item.get("slotId", 0), item.get("sessionId", "")))
    return {
        "generatedAtUnixMs": now_ms(),
        "strategySessionCount": len(strategy_sessions),
        "agentSessionCount": len(agent_sessions),
        "agentSessions": agent_sessions,
    }


def build_console_report(snapshot: dict[str, Any]) -> str:
    header = (
        f"[broker] active report | agentSessions={snapshot.get('agentSessionCount', 0)} "
        f"strategySessions={snapshot.get('strategySessionCount', 0)}"
    )
    lines = [header]
    for session in snapshot.get("agentSessions", []):
        lines.append(
            "[broker] "
            f"slot={session.get('slotId')} "
            f"owner={session.get('controllerOwner')} "
            f"source={session.get('controllerSource')} "
            f"frame={session.get('frame')} "
            f"intent={session.get('intentMode') or '-'} "
            f"why={session.get('intentReason') or '-'} "
            f"input={session.get('lastInputSummary') or '-'}"
        )
    return "\n".join(lines)


def reporter_loop() -> None:
    last_digest = ""
    last_printed_at = 0.0
    while True:
        snapshot = collect_report_snapshot()
        digest = json.dumps(snapshot, ensure_ascii=True, sort_keys=True, separators=(",", ":"))
        now = time.time()
        should_print = digest != last_digest
        if not should_print and snapshot.get("agentSessionCount", 0) > 0:
            should_print = now - last_printed_at >= REPORT_HEARTBEAT_SECONDS

        if should_print:
            print(build_console_report(snapshot), flush=True)
            last_digest = digest
            last_printed_at = now

        time.sleep(REPORT_INTERVAL_SECONDS)


class BrokerHandler(BaseHTTPRequestHandler):
    server_version = "CodexBroker/0.1"

    def do_GET(self) -> None:
        if self.path == "/health":
            self._write_json(200, {"ok": True, "sessions": len(SESSIONS), "agentSessions": len(AGENT_SESSIONS)})
            return

        if self.path == "/report":
            self._write_json(200, {"ok": True, "report": collect_report_snapshot()})
            return

        parsed = urlparse(self.path)
        if parsed.path == "/agent/next":
            self._handle_agent_next(parse_qs(parsed.query))
            return

        self._write_json(404, {"ok": False, "error": "not_found"})

    def do_POST(self) -> None:
        try:
            payload = self._read_json()
        except ValueError as exc:
            self._write_json(400, {"ok": False, "error": str(exc)})
            return

        if self.path == "/session/start":
            self._handle_session_start(payload)
            return

        if self.path == "/strategy/tick":
            self._handle_strategy_tick(payload)
            return

        if self.path == "/session/reset":
            self._handle_session_reset(payload)
            return

        if self.path == "/session/stop":
            self._handle_session_stop(payload)
            return

        if self.path == "/agent/session/start":
            self._handle_agent_session_start(payload)
            return

        if self.path == "/agent/state":
            self._handle_agent_state(payload)
            return

        if self.path == "/agent/action":
            self._handle_agent_action(payload)
            return

        if self.path == "/agent/session/reset":
            self._handle_agent_session_reset(payload)
            return

        if self.path == "/agent/session/stop":
            self._handle_agent_session_stop(payload)
            return

        self._write_json(404, {"ok": False, "error": "not_found"})

    def log_message(self, format: str, *args: Any) -> None:
        return

    def _handle_session_start(self, payload: dict[str, Any]) -> None:
        slot_id = int(payload.get("slotId", 2))
        prompt_state = payload.get("promptState") or {}
        thread_id, intent, error = run_codex_new(build_start_prompt(prompt_state))
        if not thread_id:
            self._write_json(502, {"ok": False, "error": error or "session_start_failed"})
            return

        session = BrokerSession(slot_id, thread_id, intent)
        with SESSIONS_LOCK:
            SESSIONS[thread_id] = session

        log_event("strategy_session_started", slot=slot_id, session=thread_id[:8], mode=session.cached_intent.get("mode", "-"))
        self._write_json(200, session.snapshot())

    def _handle_strategy_tick(self, payload: dict[str, Any]) -> None:
        session_id = str(payload.get("sessionId", "")).strip()
        with SESSIONS_LOCK:
            session = SESSIONS.get(session_id)

        if session is None:
            self._write_json(404, {"ok": False, "error": "unknown_session"})
            return

        session.queue_tick(payload)
        self._write_json(200, session.snapshot())

    def _handle_session_reset(self, payload: dict[str, Any]) -> None:
        session_id = str(payload.get("sessionId", "")).strip()
        with SESSIONS_LOCK:
            session = SESSIONS.get(session_id)

        if session is None:
            self._write_json(404, {"ok": False, "error": "unknown_session"})
            return

        session.reset(str(payload.get("reason", "round_reset")))
        log_event("strategy_session_reset", slot=session.slot_id, session=session_id[:8], reason=payload.get("reason", "round_reset"))
        self._write_json(200, {"ok": True, "sessionId": session_id})

    def _handle_session_stop(self, payload: dict[str, Any]) -> None:
        session_id = str(payload.get("sessionId", "")).strip()
        with SESSIONS_LOCK:
            session = SESSIONS.pop(session_id, None)

        removed = session is not None
        log_event("strategy_session_stopped", session=session_id[:8], removed=removed)
        self._write_json(200, {"ok": True, "sessionId": session_id, "removed": session is not None})

    def _handle_agent_session_start(self, payload: dict[str, Any]) -> None:
        slot_id = int(payload.get("slotId", 2))
        prompt_state = payload.get("promptState") or {}
        session_id = f"agent-slot-{slot_id}-{now_ms()}"
        session = AgentDrivenSession(slot_id, session_id, prompt_state)
        with AGENT_LOCK:
            AGENT_SESSIONS[session_id] = session
            AGENT_SESSION_BY_SLOT[slot_id] = session_id
        log_event("agent_session_started", slot=slot_id, session=session_id[:16], frame=prompt_state.get("frame", -1))
        self._write_json(200, session.intent_envelope())

    def _handle_agent_state(self, payload: dict[str, Any]) -> None:
        session_id = str(payload.get("sessionId", "")).strip()
        with AGENT_LOCK:
            session = AGENT_SESSIONS.get(session_id)

        if session is None:
            self._write_json(404, {"ok": False, "error": "unknown_agent_session"})
            return

        envelope = session.publish_state(payload)
        report = session.report_payload()
        log_event(
            "agent_state",
            slot=report["slotId"],
            source=report["controllerSource"],
            owner=report["controllerOwner"],
            frame=report["frame"],
            input=report["lastInputSummary"],
        )
        self._write_json(200, envelope)

    def _handle_agent_action(self, payload: dict[str, Any]) -> None:
        session_id = str(payload.get("sessionId", "")).strip()
        with AGENT_LOCK:
            session = AGENT_SESSIONS.get(session_id)

        if session is None:
            self._write_json(404, {"ok": False, "error": "unknown_agent_session"})
            return

        intent = validate_intent(payload.get("intent"))
        if intent is None:
            self._write_json(400, {"ok": False, "error": "invalid_intent"})
            return

        envelope = session.publish_action(intent)
        log_event("agent_action", slot=session.slot_id, mode=intent["mode"], why=intent["reason"] or "-", session=session_id[:16])
        self._write_json(200, envelope)

    def _handle_agent_session_reset(self, payload: dict[str, Any]) -> None:
        session_id = str(payload.get("sessionId", "")).strip()
        with AGENT_LOCK:
            session = AGENT_SESSIONS.get(session_id)

        if session is None:
            self._write_json(404, {"ok": False, "error": "unknown_agent_session"})
            return

        session.reset(str(payload.get("reason", "round_reset")))
        log_event("agent_session_reset", slot=session.slot_id, session=session_id[:16], reason=payload.get("reason", "round_reset"))
        self._write_json(200, {"ok": True, "sessionId": session_id})

    def _handle_agent_session_stop(self, payload: dict[str, Any]) -> None:
        session_id = str(payload.get("sessionId", "")).strip()
        removed = False
        with AGENT_LOCK:
            session = AGENT_SESSIONS.pop(session_id, None)
            if session is not None:
                removed = True
                session.stop()
                if AGENT_SESSION_BY_SLOT.get(session.slot_id) == session_id:
                    AGENT_SESSION_BY_SLOT.pop(session.slot_id, None)
        log_event("agent_session_stopped", session=session_id[:16], removed=removed)
        self._write_json(200, {"ok": True, "sessionId": session_id, "removed": removed})

    def _handle_agent_next(self, query: dict[str, list[str]]) -> None:
        slot_values = query.get("slotId") or []
        if not slot_values:
            self._write_json(400, {"ok": False, "error": "missing_slotId"})
            return

        try:
            slot_id = int(slot_values[0])
        except ValueError:
            self._write_json(400, {"ok": False, "error": "invalid_slotId"})
            return

        with AGENT_LOCK:
            session_id = AGENT_SESSION_BY_SLOT.get(slot_id)
            session = AGENT_SESSIONS.get(session_id) if session_id else None

        if session is None:
            self._write_json(404, {"ok": False, "error": "no_active_agent_session"})
            return

        self._write_json(200, session.state_payload())

    def _read_json(self) -> dict[str, Any]:
        length = int(self.headers.get("Content-Length", "0"))
        raw = self.rfile.read(length) if length > 0 else b"{}"
        try:
            parsed = json.loads(raw.decode("utf-8"))
        except json.JSONDecodeError as exc:
            raise ValueError(f"invalid_json:{exc.msg}") from exc

        if not isinstance(parsed, dict):
            raise ValueError("invalid_payload")
        return parsed

    def _write_json(self, status_code: int, payload: dict[str, Any]) -> None:
        encoded = json.dumps(payload, ensure_ascii=True, separators=(",", ":")).encode("utf-8")
        self.send_response(status_code)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(encoded)))
        self.end_headers()
        self.wfile.write(encoded)


def main() -> None:
    server = ThreadingHTTPServer((BROKER_HOST, BROKER_PORT), BrokerHandler)
    reporter = threading.Thread(target=reporter_loop, daemon=True)
    reporter.start()
    print(f"Codex broker listening on http://{BROKER_HOST}:{BROKER_PORT}", flush=True)
    server.serve_forever()


if __name__ == "__main__":
    main()
