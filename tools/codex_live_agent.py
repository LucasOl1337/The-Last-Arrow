import json
import os
import subprocess
import sys
import time
from pathlib import Path
from typing import Any
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen
from codex_memory import MemoryTracker
from codex_trace_store import append_trace_event


BROKER_BASE = os.environ.get("CODEX_BROKER_BASE", "http://127.0.0.1:8765").rstrip("/")
SLOT_ID = int(os.environ.get("CODEX_AGENT_SLOT_ID", "2"))
BOT_ID = os.environ.get("CODEX_BOT_ID", "").strip()
POLL_INTERVAL_SECONDS = float(os.environ.get("CODEX_AGENT_POLL_INTERVAL_SEC", "0.18"))
IDLE_INTERVAL_SECONDS = float(os.environ.get("CODEX_AGENT_IDLE_INTERVAL_SEC", "0.75"))
TURN_TIMEOUT_SECONDS = float(os.environ.get("CODEX_AGENT_TURN_TIMEOUT_SEC", "25"))
CODEX_MODEL = os.environ.get("CODEX_MODEL", "")
CODEX_REASONING_EFFORT = os.environ.get("CODEX_REASONING_EFFORT", "").strip()
CODEX_MODEL_PROVIDER = os.environ.get("CODEX_MODEL_PROVIDER", "openai_codex").strip().lower()
DISPLAY_MODEL = CODEX_MODEL or "codex-cli-default"
OPENROUTER_API_KEY_ENV_VAR = os.environ.get("OPENROUTER_API_KEY_ENV_VAR", "OPENROUTER_API_KEY").strip() or "OPENROUTER_API_KEY"
OPENROUTER_BASE_URL = os.environ.get("OPENROUTER_BASE_URL", "https://openrouter.ai/api/v1").rstrip("/")
OPENROUTER_SITE_URL = os.environ.get("OPENROUTER_SITE_URL", "").strip()
OPENROUTER_APP_NAME = os.environ.get("OPENROUTER_APP_NAME", "The Last Arrow Bot Arena").strip()
TOOLS_DIR = Path(__file__).resolve().parent
CODEX_PATH = Path(os.environ.get("CODEX_EXE", r"C:\Users\user\.codex\.sandbox-bin\codex.exe"))

# ── Multi-account OAuth fallback ────────────────────────────────────────────
# Comma-separated list of CODEX_HOME paths. When the current account fails
# due to quota or auth errors the agent rotates to the next one automatically.
_DEFAULT_CODEX_HOME = str(Path.home() / ".codex")
_DEFAULT_CODEX_HOME_2 = str(Path.home() / ".codex2")
_raw_fallbacks = os.environ.get("CODEX_HOME_FALLBACKS", "").strip()
if _raw_fallbacks:
    CODEX_HOME_CANDIDATES: list[str] = [p.strip() for p in _raw_fallbacks.split(",") if p.strip()]
else:
    # Auto-discover: primary first, then any .codex* sibling that has auth.json
    _home = Path.home()
    CODEX_HOME_CANDIDATES = [_DEFAULT_CODEX_HOME]
    for _entry in sorted(_home.iterdir()):
        if (
            _entry.is_dir()
            and _entry.name.startswith(".codex")
            and str(_entry) != _DEFAULT_CODEX_HOME
            and (_entry / "auth.json").exists()
        ):
            CODEX_HOME_CANDIDATES.append(str(_entry))
    # Also include .codex2 even without auth.json yet so user can log in
    if _DEFAULT_CODEX_HOME_2 not in CODEX_HOME_CANDIDATES and Path(_DEFAULT_CODEX_HOME_2).exists():
        CODEX_HOME_CANDIDATES.append(_DEFAULT_CODEX_HOME_2)

# Errors that indicate quota exhaustion or authentication failure
_QUOTA_ERROR_KEYWORDS = (
    "rate_limit", "quota", "429", "insufficient_quota",
    "billing", "auth", "unauthorized", "401", "403",
    "token limit", "usage limit",
)


def _is_quota_or_auth_error(error: str) -> bool:
    low = error.lower()
    return any(kw in low for kw in _QUOTA_ERROR_KEYWORDS)
# ────────────────────────────────────────────────────────────────────────────
SYSTEM_PROMPT_PATH = TOOLS_DIR / "codex_broker_system_prompt.txt"
SCHEMA_PATH = TOOLS_DIR / "codex_broker_output_schema.json"
SYSTEM_PROMPT = SYSTEM_PROMPT_PATH.read_text(encoding="utf-8").strip()


VALID_MODES = {"pressure", "zone", "retreat", "punish", "stabilize"}
VALID_ANTI_PROJECTILE = {"hold", "jump", "dash", "parry_prefer"}


def log(message: str) -> None:
    print(f"[codex-live-agent] {message}", flush=True)


def now_ms() -> int:
    return int(time.time() * 1000)


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
        "advanceBias": clamp01(candidate.get("advanceBias"), 0.82),
        "shootBias": clamp01(candidate.get("shootBias"), 0.4),
        "meleeBias": clamp01(candidate.get("meleeBias"), 0.74),
        "dashBias": clamp01(candidate.get("dashBias"), 0.7),
        "jumpBias": clamp01(candidate.get("jumpBias"), 0.22),
        "antiProjectile": anti_projectile,
        "antiAir": bool(candidate.get("antiAir", True)),
        "punishRecovery": bool(candidate.get("punishRecovery", True)),
        "cornerEscapeBias": clamp01(candidate.get("cornerEscapeBias"), 0.22),
        "focusTargetSlot": focus_target_slot,
        "expiresInMs": expires_in_ms,
        "reason": str(candidate.get("reason", "")).strip()[:160],
    }


def apply_aggression_bias(intent: dict[str, Any], state: dict[str, Any]) -> dict[str, Any]:
    tuned = dict(intent)
    prompt_state = state.get("promptState") or {}
    feedback = state.get("executorFeedback") or {}
    arena = prompt_state.get("arena") or {}
    target = prompt_state.get("target") or {}
    self_state = prompt_state.get("self") or {}

    round_reset = bool(arena.get("roundResetPending")) or bool(feedback.get("roundResetPending"))
    target_visible = bool(feedback.get("targetVisible"))
    projectile_risk = bool(feedback.get("projectileThreatActive"))
    self_cornered = bool(arena.get("selfCornered"))
    target_cornered = bool(arena.get("targetCornered"))
    in_melee = bool(arena.get("targetInMeleeRange"))
    in_shoot = bool(arena.get("targetInShootRange"))
    target_above = bool(arena.get("targetAbove"))
    target_vulnerable = bool(target.get("isHitStunned")) or bool(target.get("isMeleeActive")) or bool(target.get("isUltimateActive"))
    self_hitstunned = bool(self_state.get("isHitStunned"))

    if round_reset or self_hitstunned:
        return tuned

    if not target_visible or projectile_risk:
        return tuned

    if tuned["mode"] in {"stabilize", "retreat"}:
        if target_vulnerable or in_melee or target_cornered:
            tuned["mode"] = "punish"
        else:
            tuned["mode"] = "pressure"
    elif tuned["mode"] == "zone" and (in_shoot or target_cornered or horizontal_distance(arena := prompt_state.get("arena") or {}) <= 420):
        tuned["mode"] = "pressure"

    if target_vulnerable:
        tuned["mode"] = "punish"
        tuned["preferredRange"] = min(tuned["preferredRange"], 160)
        tuned["advanceBias"] = max(tuned["advanceBias"], 0.92)
        tuned["meleeBias"] = max(tuned["meleeBias"], 0.9)
        tuned["dashBias"] = max(tuned["dashBias"], 0.8)
    elif in_melee:
        tuned["mode"] = "pressure"
        tuned["preferredRange"] = min(tuned["preferredRange"], 140)
        tuned["advanceBias"] = max(tuned["advanceBias"], 0.86)
        tuned["meleeBias"] = max(tuned["meleeBias"], 0.84)
    elif target_cornered:
        tuned["mode"] = "pressure"
        tuned["preferredRange"] = min(tuned["preferredRange"], 220)
        tuned["advanceBias"] = max(tuned["advanceBias"], 0.88)
        tuned["shootBias"] = max(tuned["shootBias"], 0.58)
        tuned["dashBias"] = max(tuned["dashBias"], 0.76)
        tuned["meleeBias"] = max(tuned["meleeBias"], 0.82)
    elif in_shoot:
        tuned["mode"] = "pressure"
        tuned["preferredRange"] = min(tuned["preferredRange"], 300)
        tuned["advanceBias"] = max(tuned["advanceBias"], 0.84)
        tuned["shootBias"] = max(tuned["shootBias"], 0.56)
        tuned["dashBias"] = max(tuned["dashBias"], 0.74)
    else:
        tuned["mode"] = "pressure"
        tuned["preferredRange"] = min(tuned["preferredRange"], 340)
        tuned["advanceBias"] = max(tuned["advanceBias"], 0.84)
        tuned["dashBias"] = max(tuned["dashBias"], 0.72)

    if target_above:
        tuned["jumpBias"] = max(tuned["jumpBias"], 0.34)
        tuned["antiAir"] = True

    if not self_cornered:
        tuned["cornerEscapeBias"] = min(tuned["cornerEscapeBias"], 0.28)

    if tuned["mode"] in {"pressure", "punish"} and "aggressive" not in tuned["reason"].lower():
        tuned["reason"] = (tuned["reason"] or "forced aggressive follow-up")[:120]

    return tuned


def horizontal_distance(arena: dict[str, Any]) -> float:
    try:
        return abs(float(arena.get("horizontalDistance", 9999.0)))
    except (TypeError, ValueError):
        return 9999.0


def _extract_openrouter_text(content: Any) -> str:
    if isinstance(content, str):
        return content.strip()
    if isinstance(content, list):
        parts: list[str] = []
        for item in content:
            if isinstance(item, dict) and item.get("type") == "text":
                parts.append(str(item.get("text", "")))
        return "".join(parts).strip()
    return ""


def run_openrouter_turn(messages: list[dict[str, str]]) -> tuple[dict[str, Any] | None, str, dict[str, Any]]:
    api_key = os.environ.get(OPENROUTER_API_KEY_ENV_VAR, "").strip()
    if not api_key:
        return None, f"openrouter_missing_api_key_env:{OPENROUTER_API_KEY_ENV_VAR}", {"stdout": "", "stderr": "", "returncode": -1}
    if not CODEX_MODEL:
        return None, "openrouter_missing_model", {"stdout": "", "stderr": "", "returncode": -1}

    payload = {
        "model": CODEX_MODEL,
        "messages": messages,
        "response_format": {"type": "json_object"},
        "temperature": 0.2,
        "max_tokens": 240,
    }
    raw = json.dumps(payload, ensure_ascii=True).encode("utf-8")
    headers = {
        "Authorization": f"Bearer {api_key}",
        "Content-Type": "application/json",
    }
    if OPENROUTER_SITE_URL:
        headers["HTTP-Referer"] = OPENROUTER_SITE_URL
    if OPENROUTER_APP_NAME:
        headers["X-OpenRouter-Title"] = OPENROUTER_APP_NAME
    request = Request(f"{OPENROUTER_BASE_URL}/chat/completions", data=raw, headers=headers, method="POST")
    try:
        with urlopen(request, timeout=TURN_TIMEOUT_SECONDS) as response:
            response_text = response.read().decode("utf-8")
    except HTTPError as exc:
        body = exc.read().decode("utf-8", errors="replace")
        return None, f"openrouter_http_{exc.code}", {"stdout": body, "stderr": "", "returncode": exc.code}
    except (URLError, TimeoutError, OSError) as exc:
        return None, f"openrouter_request_failed:{exc}", {"stdout": "", "stderr": str(exc), "returncode": -1}

    try:
        parsed = json.loads(response_text)
    except json.JSONDecodeError:
        return None, "openrouter_invalid_json", {"stdout": response_text, "stderr": "", "returncode": 0}

    message_content = _extract_openrouter_text((((parsed.get("choices") or [])[0] or {}).get("message") or {}).get("content"))
    if not message_content:
        return None, "openrouter_missing_message", {"stdout": response_text, "stderr": "", "returncode": 0}
    try:
        intent_payload = json.loads(message_content)
    except json.JSONDecodeError:
        return None, "openrouter_invalid_intent_json", {"stdout": response_text, "stderr": "", "returncode": 0}

    intent = validate_intent(intent_payload)
    if intent is None:
        return None, "openrouter_invalid_schema_response", {"stdout": response_text, "stderr": "", "returncode": 0}
    return intent, "", {"stdout": response_text, "stderr": "", "returncode": 0}


def run_codex_command(command: list[str], capture_thread_id: bool, codex_home: str = "") -> tuple[str | None, dict[str, Any] | None, str, dict[str, Any]]:
    creationflags = 0
    startupinfo = None
    if os.name == "nt":
        creationflags = getattr(subprocess, "CREATE_NO_WINDOW", 0)
        startupinfo = subprocess.STARTUPINFO()
        startupinfo.dwFlags |= subprocess.STARTF_USESHOWWINDOW
    env = os.environ.copy()
    if codex_home:
        env["CODEX_HOME"] = codex_home
    try:
        completed = subprocess.run(
            command,
            capture_output=True,
            text=True,
            encoding="utf-8",
            timeout=TURN_TIMEOUT_SECONDS,
            check=False,
            env=env,
            creationflags=creationflags,
            startupinfo=startupinfo,
        )
    except subprocess.TimeoutExpired:
        return None, None, "codex_timeout", {"stdout": "", "stderr": "", "returncode": -1}
    except OSError as exc:
        return None, None, f"codex_exec_failed:{exc}", {"stdout": "", "stderr": str(exc), "returncode": -1}

    thread_id = None
    final_text = None
    failure_reason = ""
    meta = {
        "stdout": completed.stdout,
        "stderr": completed.stderr,
        "returncode": completed.returncode,
    }
    for raw_line in completed.stdout.splitlines():
        try:
            event = json.loads(raw_line)
        except json.JSONDecodeError:
            continue

        if capture_thread_id and event.get("type") == "thread.started":
            thread_id = event.get("thread_id")

        if event.get("type") == "error":
            failure_reason = str(event.get("message") or "").strip()

        if event.get("type") == "turn.failed":
            error_payload = event.get("error") or {}
            failure_reason = str(error_payload.get("message") or failure_reason).strip()

        if event.get("type") == "item.completed":
            item = event.get("item") or {}
            if item.get("type") == "agent_message" and item.get("text"):
                final_text = item["text"]

    if not final_text:
        if failure_reason:
            return thread_id, None, f"codex_error:{failure_reason}", meta
        stderr_text = (completed.stderr or "").strip()
        if stderr_text:
            last_stderr_line = stderr_text.splitlines()[-1].strip()
            if last_stderr_line:
                return thread_id, None, f"codex_error:{last_stderr_line}", meta
        return thread_id, None, "missing_agent_message", meta

    try:
        parsed = json.loads(final_text)
    except json.JSONDecodeError:
        return thread_id, None, "invalid_json_response", meta

    intent = validate_intent(parsed)
    if intent is None:
        return thread_id, None, "invalid_schema_response", meta

    return thread_id, intent, "", meta


def run_codex_new(prompt: str, codex_home: str = "") -> tuple[str | None, dict[str, Any] | None, str, dict[str, Any]]:
    if CODEX_MODEL_PROVIDER == "openrouter":
        intent, error, meta = run_openrouter_turn([
            {"role": "system", "content": SYSTEM_PROMPT},
            {"role": "user", "content": prompt},
        ])
        synthetic_thread_id = f"openrouter-slot-{SLOT_ID}-{now_ms()}"
        return synthetic_thread_id, intent, error, meta

    command = [
        str(CODEX_PATH),
    ]
    if CODEX_REASONING_EFFORT:
        command.extend(["-c", f'model_reasoning_effort="{CODEX_REASONING_EFFORT}"'])
    command.extend([
        "exec",
        "--json",
        "--skip-git-repo-check",
        "--sandbox",
        "read-only",
        "--cd",
        str(TOOLS_DIR),
        "--output-schema",
        str(SCHEMA_PATH),
    ])
    if CODEX_MODEL_PROVIDER == "ollama":
        command.extend(["--oss", "--local-provider", "ollama"])
    if CODEX_MODEL:
        command.extend(["--model", CODEX_MODEL])
    command.append(prompt)
    return run_codex_command(command, capture_thread_id=True, codex_home=codex_home)


def run_codex_resume(session_id: str, prompt: str, codex_home: str = "") -> tuple[dict[str, Any] | None, str, dict[str, Any]]:
    if CODEX_MODEL_PROVIDER == "openrouter":
        intent, error, meta = run_openrouter_turn([
            {"role": "system", "content": SYSTEM_PROMPT},
            {"role": "user", "content": prompt},
        ])
        return intent, error, meta

    command = [
        str(CODEX_PATH),
    ]
    if CODEX_REASONING_EFFORT:
        command.extend(["-c", f'model_reasoning_effort="{CODEX_REASONING_EFFORT}"'])
    command.extend([
        "exec",
        "resume",
        session_id,
        "--json",
        "--skip-git-repo-check",
    ])
    if CODEX_MODEL_PROVIDER == "ollama":
        command.extend(["--oss", "--local-provider", "ollama"])
    if CODEX_MODEL:
        command.extend(["--model", CODEX_MODEL])
    command.append(prompt)
    _, parsed, error, meta = run_codex_command(command, capture_thread_id=False, codex_home=codex_home)
    return parsed, error, meta


def build_start_prompt(payload: dict[str, Any]) -> str:
    compact = json.dumps(payload, ensure_ascii=True, separators=(",", ":"))
    return (
        f"{SYSTEM_PROMPT}\n\n"
        f"You are now the live external player for slot {SLOT_ID} in an ongoing match.\n"
        "Return only one tactical intent JSON object.\n"
        "Be aggressive enough to kill the opponent. Do not idle.\n"
        "If the target is visible and the round is live, default to pressure or punish.\n"
        "Prefer forcing contact, corner carry, anti-air pressure, or direct punish windows over safe spacing.\n"
        "When coachMemory shows repeated opponent escape habits, preempt them with pressure, anti-air, or dash catch.\n"
        "When coachMemory contains latestRoundReview, latestSeriesReview, latestSeriesPlan, latestMatchReview, or nextMatchPlan, treat them as concrete coaching from prior rounds and series.\n"
        "Respect botProfile first. Use globalKnowledgeSummary only as shared gameplay context.\n"
        "Use gameplayConcerns as reality checks about the game state, but still play to win the current round.\n"
        "Use stabilize only for genuine danger states such as round reset, corner escape, or active projectile threat.\n"
        "State payload:\n"
        f"{compact}\n"
    )


def build_tick_prompt(payload: dict[str, Any]) -> str:
    compact = json.dumps(payload, ensure_ascii=True, separators=(",", ":"))
    return (
        "You are still controlling the same live fighter in the same match.\n"
        "Update the tactical intent for the next short horizon.\n"
        "Do not return safe defaults unless the state truly demands it.\n"
        "If the opponent is targetable, bias toward plans that create attacks immediately.\n"
        "Avoid repeated stabilize outputs when the last inputs produced no offense.\n"
        "Exploit coachMemory aggressively: if the opponent often jumps, anti-air; if they dash out, dash-catch; if they projectile often, punish startup.\n"
        "If you already lost similar situations recently, change the line now instead of repeating it.\n"
        "If latestRoundReview, latestSeriesReview, latestSeriesPlan, latestMatchReview, or nextMatchPlan exists, treat it as reusable coaching and adapt the next intent around it.\n"
        "Respect botProfile first. Use globalKnowledgeSummary as shared context, but do not let it erase the bot's own style.\n"
        "If the target is visible, there is no round reset, and no immediate projectile threat exists, do not choose stabilize.\n"
        "Return only one JSON object matching the schema.\n"
        "State payload:\n"
        f"{compact}\n"
    )


def build_warmup_prompt() -> str:
    return (
        f"{SYSTEM_PROMPT}\n\n"
        "This is a warmup turn for a future live match.\n"
        "Return one aggressive default intent JSON object for a generic neutral opening.\n"
        "Assume the target is visible at mid range and can be pressured.\n"
    )


def format_prompt_payload(state: dict[str, Any], memory: MemoryTracker) -> dict[str, Any]:
    prompt_state = state.get("promptState") or {}
    feedback = state.get("executorFeedback") or {}
    return {
        "botId": BOT_ID or str(prompt_state.get("botId", "") or ((prompt_state.get("self") or {}).get("botId", "")) or ""),
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
        "coachMemory": memory.prompt_payload(),
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
    if frame - last_frame >= 6:
        return True
    return time.time() - last_turn_at >= 0.28


def post_intent(session_id: str, intent: dict[str, Any]) -> bool:
    status, payload = http_post(
        "/agent/action",
        {
            "sessionId": session_id,
            "intent": intent,
        },
    )
    return status == 200 and isinstance(payload, dict)


def send_heartbeat(
    session_id: str,
    codex_session_id: str,
    phase: str,
    *,
    thinking: bool,
    note: str = "",
    error: str = "",
    turn_started_ms: int = 0,
    turn_completed_ms: int = 0,
) -> None:
    if not session_id:
        return

    payload: dict[str, Any] = {
        "sessionId": session_id,
        "model": DISPLAY_MODEL,
        "phase": phase,
        "thinking": thinking,
        "note": note[:160],
        "error": error[:160],
        "codexSessionId": codex_session_id,
    }
    if turn_started_ms > 0:
        payload["turnStartedUnixMs"] = turn_started_ms
    if turn_completed_ms > 0:
        payload["turnCompletedUnixMs"] = turn_completed_ms
    http_post("/agent/heartbeat", payload)


def main() -> int:
    if not CODEX_PATH.exists():
        log(f"codex executable not found: {CODEX_PATH}")
        return 1

    # ── Account rotation state ────────────────────────────────────────────
    account_index = 0
    account_consecutive_failures = 0
    ACCOUNT_FAILURE_THRESHOLD = 3  # falhas consecutivas antes de rotacionar

    def current_codex_home() -> str:
        if not CODEX_HOME_CANDIDATES:
            return ""
        return CODEX_HOME_CANDIDATES[account_index % len(CODEX_HOME_CANDIDATES)]

    def rotate_account(reason: str) -> bool:
        nonlocal account_index, account_consecutive_failures
        if len(CODEX_HOME_CANDIDATES) <= 1:
            log(f"[auth-fallback] sem contas alternativas para rotacionar ({reason})")
            return False
        old_home = current_codex_home()
        account_index = (account_index + 1) % len(CODEX_HOME_CANDIDATES)
        account_consecutive_failures = 0
        new_home = current_codex_home()
        log(f"[auth-fallback] rotacionando conta: {old_home} -> {new_home} | motivo: {reason}")
        return True
    # ─────────────────────────────────────────────────────────────────────

    log(f"starting live agent for slot {SLOT_ID} bot={BOT_ID or '-'} via {BROKER_BASE}")
    log(f"[auth-fallback] contas disponiveis: {CODEX_HOME_CANDIDATES}")
    codex_session_id = ""
    broker_session_id = ""
    last_frame = -1
    last_turn_at = 0.0
    warmup_posted_session_id = ""
    memory = MemoryTracker(bot_id=BOT_ID, slot_id=SLOT_ID)

    warmup_prompt = build_warmup_prompt()
    append_trace_event("warmup_request", {
        "slotId": SLOT_ID,
        "botId": BOT_ID,
        "provider": CODEX_MODEL_PROVIDER,
        "model": CODEX_MODEL or "codex-cli-default",
        "prompt": warmup_prompt,
    })
    thread_id, warmup_intent, warmup_error, warmup_meta = run_codex_new(warmup_prompt, codex_home=current_codex_home())
    append_trace_event("warmup_response", {
        "slotId": SLOT_ID,
        "botId": BOT_ID,
        "provider": CODEX_MODEL_PROVIDER,
        "model": CODEX_MODEL or "codex-cli-default",
        "threadId": thread_id or "",
        "intent": warmup_intent,
        "error": warmup_error,
        "stdout": warmup_meta.get("stdout", ""),
        "stderr": warmup_meta.get("stderr", ""),
        "returncode": warmup_meta.get("returncode", -1),
    })
    if thread_id and warmup_intent is not None:
        codex_session_id = thread_id
        log(f"warmup ready session={codex_session_id[:8]} mode={warmup_intent['mode']}")
    else:
        log(f"warmup skipped error={warmup_error or 'unknown'}")
        if warmup_error and _is_quota_or_auth_error(warmup_error):
            rotate_account(warmup_error)

    while True:
        status, state = http_get(f"/agent/next?slotId={SLOT_ID}")
        if status == 404:
            if broker_session_id:
                log("broker session ended; waiting for a new one")
            broker_session_id = ""
            last_frame = -1
            last_turn_at = 0.0
            time.sleep(IDLE_INTERVAL_SECONDS)
            continue

        if status != 200 or not isinstance(state, dict) or not state.get("ok", True):
            log(f"broker poll failed: status={status} error={state.get('error') if isinstance(state, dict) else state}")
            time.sleep(IDLE_INTERVAL_SECONDS)
            continue

        memory.update(state)

        current_broker_session = str(state.get("sessionId", "")).strip()
        if not current_broker_session:
            time.sleep(IDLE_INTERVAL_SECONDS)
            continue

        if current_broker_session != broker_session_id:
            broker_session_id = current_broker_session
            last_frame = -1
            last_turn_at = 0.0
            log(f"attached to broker session {broker_session_id}")
            append_trace_event("broker_session_attached", {
                "slotId": SLOT_ID,
                "botId": BOT_ID,
                "brokerSessionId": broker_session_id,
                "codexSessionId": codex_session_id,
            })
            send_heartbeat(
                broker_session_id,
                codex_session_id,
                "idle",
                thinking=False,
                note="Attached to broker session",
            )
            if warmup_intent is not None and warmup_posted_session_id != broker_session_id:
                if post_intent(broker_session_id, warmup_intent):
                    warmup_posted_session_id = broker_session_id
                    log(f"posted warmup action mode={warmup_intent['mode']} reason={warmup_intent['reason']}")
                    send_heartbeat(
                        broker_session_id,
                        codex_session_id,
                        "idle",
                        thinking=False,
                        note=f"Posted warmup {warmup_intent['mode']} ({warmup_intent['reason'] or 'no-reason'})",
                    )
                else:
                    log("failed to post warmup action to broker")

        if not should_request_turn(state, last_frame, last_turn_at):
            time.sleep(POLL_INTERVAL_SECONDS)
            continue

        payload = format_prompt_payload(state, memory)
        append_trace_event("prompt_payload_created", {
            "slotId": SLOT_ID,
            "botId": BOT_ID or payload.get("botId", ""),
            "brokerSessionId": broker_session_id,
            "codexSessionId": codex_session_id,
            "frame": int(state.get("frame", -1)),
            "payload": payload,
        })
        turn_started_ms = now_ms()
        send_heartbeat(
            broker_session_id,
            codex_session_id,
            "thinking",
            thinking=True,
            note=f"Thinking for frame {int(state.get('frame', -1))}",
            turn_started_ms=turn_started_ms,
        )
        if not codex_session_id:
            prompt = build_start_prompt(payload)
            append_trace_event("codex_request", {
                "slotId": SLOT_ID,
                "botId": BOT_ID or payload.get("botId", ""),
                "brokerSessionId": broker_session_id,
                "codexSessionId": codex_session_id,
                "frame": int(state.get("frame", -1)),
                "requestType": "start",
                "provider": CODEX_MODEL_PROVIDER,
                "model": CODEX_MODEL or "codex-cli-default",
                "prompt": prompt,
            })
            thread_id, intent, error, meta = run_codex_new(prompt, codex_home=current_codex_home())
            append_trace_event("codex_response", {
                "slotId": SLOT_ID,
                "botId": BOT_ID or payload.get("botId", ""),
                "brokerSessionId": broker_session_id,
                "codexSessionId": thread_id or "",
                "frame": int(state.get("frame", -1)),
                "requestType": "start",
                "provider": CODEX_MODEL_PROVIDER,
                "model": CODEX_MODEL or "codex-cli-default",
                "intent": intent,
                "error": error,
                "codexHome": current_codex_home(),
                "stdout": meta.get("stdout", ""),
                "stderr": meta.get("stderr", ""),
                "returncode": meta.get("returncode", -1),
            })
            if not thread_id or intent is None:
                log(f"codex start failed: {error}")
                account_consecutive_failures += 1
                if _is_quota_or_auth_error(error) or account_consecutive_failures >= ACCOUNT_FAILURE_THRESHOLD:
                    rotated = rotate_account(error)
                    if rotated:
                        codex_session_id = ""  # reinicia sessao na nova conta
                send_heartbeat(
                    broker_session_id,
                    codex_session_id,
                    "error",
                    thinking=False,
                    note="Codex start failed",
                    error=error,
                    turn_started_ms=turn_started_ms,
                    turn_completed_ms=now_ms(),
                )
                time.sleep(IDLE_INTERVAL_SECONDS)
                continue
            account_consecutive_failures = 0
            codex_session_id = thread_id
            intent = apply_aggression_bias(intent, state)
            log(f"codex session started {codex_session_id[:8]} mode={intent['mode']} reason={intent['reason']}")
        else:
            prompt = build_tick_prompt(payload)
            append_trace_event("codex_request", {
                "slotId": SLOT_ID,
                "botId": BOT_ID or payload.get("botId", ""),
                "brokerSessionId": broker_session_id,
                "codexSessionId": codex_session_id,
                "frame": int(state.get("frame", -1)),
                "requestType": "resume",
                "provider": CODEX_MODEL_PROVIDER,
                "model": CODEX_MODEL or "codex-cli-default",
                "prompt": prompt,
            })
            intent, error, meta = run_codex_resume(codex_session_id, prompt, codex_home=current_codex_home())
            append_trace_event("codex_response", {
                "slotId": SLOT_ID,
                "botId": BOT_ID or payload.get("botId", ""),
                "brokerSessionId": broker_session_id,
                "codexSessionId": codex_session_id,
                "frame": int(state.get("frame", -1)),
                "requestType": "resume",
                "provider": CODEX_MODEL_PROVIDER,
                "model": CODEX_MODEL or "codex-cli-default",
                "intent": intent,
                "error": error,
                "codexHome": current_codex_home(),
                "stdout": meta.get("stdout", ""),
                "stderr": meta.get("stderr", ""),
                "returncode": meta.get("returncode", -1),
            })
            if intent is None:
                log(f"codex resume failed: {error}")
                account_consecutive_failures += 1
                if _is_quota_or_auth_error(error) or account_consecutive_failures >= ACCOUNT_FAILURE_THRESHOLD:
                    rotated = rotate_account(error)
                    if rotated:
                        codex_session_id = ""  # abre nova sessao na nova conta
                send_heartbeat(
                    broker_session_id,
                    codex_session_id,
                    "error",
                    thinking=False,
                    note="Codex resume failed",
                    error=error,
                    turn_started_ms=turn_started_ms,
                    turn_completed_ms=now_ms(),
                )
                time.sleep(POLL_INTERVAL_SECONDS)
                continue
            account_consecutive_failures = 0

        intent = apply_aggression_bias(intent, state)

        send_heartbeat(
            broker_session_id,
            codex_session_id,
            "posting_action",
            thinking=False,
            note=f"Posting {intent['mode']} ({intent['reason'] or 'no-reason'})",
            turn_started_ms=turn_started_ms,
        )
        last_frame = int(state.get("frame", -1))
        last_turn_at = time.time()
        if post_intent(broker_session_id, intent):
            log(f"posted action frame={last_frame} mode={intent['mode']} reason={intent['reason']}")
            append_trace_event("intent_posted", {
                "slotId": SLOT_ID,
                "botId": BOT_ID or payload.get("botId", ""),
                "brokerSessionId": broker_session_id,
                "codexSessionId": codex_session_id,
                "frame": last_frame,
                "intent": intent,
            })
            send_heartbeat(
                broker_session_id,
                codex_session_id,
                "idle",
                thinking=False,
                note=f"Posted {intent['mode']} ({intent['reason'] or 'no-reason'})",
                turn_started_ms=turn_started_ms,
                turn_completed_ms=now_ms(),
            )
        else:
            log("failed to post action to broker")
            send_heartbeat(
                broker_session_id,
                codex_session_id,
                "error",
                thinking=False,
                note="Failed to post action to broker",
                error="post_action_failed",
                turn_started_ms=turn_started_ms,
                turn_completed_ms=now_ms(),
            )

        time.sleep(POLL_INTERVAL_SECONDS)


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except KeyboardInterrupt:
        log("stopped")
        raise SystemExit(0)
