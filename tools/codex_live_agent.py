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
HEARTBEAT_INTERVAL_SECONDS = float(os.environ.get("CODEX_AGENT_HEARTBEAT_INTERVAL_SEC", "4.0"))
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
HEURISTIC_PROVIDER = "heuristic"
HEURISTIC_MODEL = "local-heuristic"


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


def has_movement_stall_feedback(state: dict[str, Any]) -> bool:
    prompt_state_raw = state.get("promptState")
    feedback_raw = state.get("executorFeedback")
    prompt_state = prompt_state_raw if isinstance(prompt_state_raw, dict) else {}
    feedback = feedback_raw if isinstance(feedback_raw, dict) else {}
    values: list[str] = [
        str(feedback.get("botFeedback", "")),
        str(feedback.get("summary", "")),
    ]
    for key in ("events", "memory"):
        source = prompt_state.get(key)
        if isinstance(source, list):
            values.extend(str(item) for item in source)
        elif source:
            values.append(str(source))

    combined = " ".join(values).lower()
    return "movement_stalled" in combined or "movement stalled" in combined


def has_vulnerable_out_of_range_feedback(state: dict[str, Any]) -> bool:
    prompt_state_raw = state.get("promptState")
    feedback_raw = state.get("executorFeedback")
    prompt_state = prompt_state_raw if isinstance(prompt_state_raw, dict) else {}
    feedback = feedback_raw if isinstance(feedback_raw, dict) else {}
    values: list[str] = [
        str(feedback.get("botFeedback", "")),
        str(feedback.get("summary", "")),
    ]
    events = prompt_state.get("events")
    if isinstance(events, list):
        values.extend(str(item) for item in events)

    combined = " ".join(values).lower()
    return "vulnerable target out of range" in combined


def has_shot_out_of_range_feedback(state: dict[str, Any]) -> bool:
    feedback_raw = state.get("executorFeedback")
    feedback = feedback_raw if isinstance(feedback_raw, dict) else {}
    combined = " ".join([
        str(feedback.get("botFeedback", "")),
        str(feedback.get("summary", "")),
    ]).lower()
    return "shot attempted out of range" in combined


def has_empty_shot_feedback(state: dict[str, Any]) -> bool:
    feedback_raw = state.get("executorFeedback")
    feedback = feedback_raw if isinstance(feedback_raw, dict) else {}
    combined = " ".join([
        str(feedback.get("botFeedback", "")),
        str(feedback.get("summary", "")),
    ]).lower()
    return "shot attempted without arrows" in combined


def has_missed_arrow_recovery_feedback(state: dict[str, Any]) -> bool:
    feedback_raw = state.get("executorFeedback")
    feedback = feedback_raw if isinstance(feedback_raw, dict) else {}
    combined = " ".join([
        str(feedback.get("botFeedback", "")),
        str(feedback.get("summary", "")),
    ]).lower()
    return "missed arrow recovery" in combined


def has_recover_arrow_feedback(state: dict[str, Any]) -> bool:
    feedback_raw = state.get("executorFeedback")
    feedback = feedback_raw if isinstance(feedback_raw, dict) else {}
    combined = " ".join([
        str(feedback.get("botFeedback", "")),
        str(feedback.get("summary", "")),
    ]).lower()
    return "recover arrow at" in combined


def has_missed_anti_air_feedback(state: dict[str, Any]) -> bool:
    feedback_raw = state.get("executorFeedback")
    feedback = feedback_raw if isinstance(feedback_raw, dict) else {}
    combined = " ".join([
        str(feedback.get("botFeedback", "")),
        str(feedback.get("summary", "")),
    ]).lower()
    return "missed anti-air" in combined


def has_anti_air_opportunity_feedback(state: dict[str, Any]) -> bool:
    feedback_raw = state.get("executorFeedback")
    feedback = feedback_raw if isinstance(feedback_raw, dict) else {}
    combined = " ".join([
        str(feedback.get("botFeedback", "")),
        str(feedback.get("summary", "")),
    ]).lower()
    return "anti-air opportunity" in combined


def has_missed_projectile_defense_feedback(state: dict[str, Any]) -> bool:
    feedback_raw = state.get("executorFeedback")
    feedback = feedback_raw if isinstance(feedback_raw, dict) else {}
    combined = " ".join([
        str(feedback.get("botFeedback", "")),
        str(feedback.get("summary", "")),
    ]).lower()
    return "missed projectile defense" in combined


def has_projectile_threat_feedback(state: dict[str, Any]) -> bool:
    feedback_raw = state.get("executorFeedback")
    feedback = feedback_raw if isinstance(feedback_raw, dict) else {}
    combined = " ".join([
        str(feedback.get("botFeedback", "")),
        str(feedback.get("summary", "")),
    ]).lower()
    return "projectile threat" in combined


def has_missed_punish_window_feedback(state: dict[str, Any]) -> bool:
    feedback_raw = state.get("executorFeedback")
    feedback = feedback_raw if isinstance(feedback_raw, dict) else {}
    combined = " ".join([
        str(feedback.get("botFeedback", "")),
        str(feedback.get("summary", "")),
    ]).lower()
    return "missed punish window" in combined


def has_punish_window_available_feedback(state: dict[str, Any]) -> bool:
    feedback_raw = state.get("executorFeedback")
    feedback = feedback_raw if isinstance(feedback_raw, dict) else {}
    combined = " ".join([
        str(feedback.get("botFeedback", "")),
        str(feedback.get("summary", "")),
    ]).lower()
    return "punish window available" in combined


def has_missed_corner_escape_feedback(state: dict[str, Any]) -> bool:
    feedback_raw = state.get("executorFeedback")
    feedback = feedback_raw if isinstance(feedback_raw, dict) else {}
    combined = " ".join([
        str(feedback.get("botFeedback", "")),
        str(feedback.get("summary", "")),
    ]).lower()
    return "missed corner escape" in combined


def has_missed_ultimate_escape_feedback(state: dict[str, Any]) -> bool:
    feedback_raw = state.get("executorFeedback")
    feedback = feedback_raw if isinstance(feedback_raw, dict) else {}
    combined = " ".join([
        str(feedback.get("botFeedback", "")),
        str(feedback.get("summary", "")),
    ]).lower()
    return "missed ultimate escape" in combined


def has_missed_melee_escape_feedback(state: dict[str, Any]) -> bool:
    feedback_raw = state.get("executorFeedback")
    feedback = feedback_raw if isinstance(feedback_raw, dict) else {}
    combined = " ".join([
        str(feedback.get("botFeedback", "")),
        str(feedback.get("summary", "")),
    ]).lower()
    return "missed melee escape" in combined


def has_missed_ranged_response_feedback(state: dict[str, Any]) -> bool:
    feedback_raw = state.get("executorFeedback")
    feedback = feedback_raw if isinstance(feedback_raw, dict) else {}
    combined = " ".join([
        str(feedback.get("botFeedback", "")),
        str(feedback.get("summary", "")),
    ]).lower()
    return "missed ranged response" in combined


def resolve_target_visible(feedback: dict[str, Any], target: dict[str, Any]) -> bool:
    if "targetVisible" in feedback:
        return bool(feedback.get("targetVisible"))

    return bool(target.get("slotId")) or bool(target.get("botId")) or bool(target.get("displayName"))


def resolve_runtime_provider(selected_provider: str, codex_available: bool) -> str:
    normalized = str(selected_provider or "openai_codex").strip().lower()
    if normalized == HEURISTIC_PROVIDER:
        return HEURISTIC_PROVIDER
    if normalized not in {"openai_codex", "openrouter", "ollama"}:
        normalized = "openai_codex"
    if normalized in {"openai_codex", "ollama"} and not codex_available:
        return HEURISTIC_PROVIDER
    return normalized


def resolve_agent_model(runtime_provider: str) -> str:
    if runtime_provider == HEURISTIC_PROVIDER:
        return HEURISTIC_MODEL
    return DISPLAY_MODEL


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
    has_prompt_projectiles = bool(prompt_state.get("dangerousProjectiles"))

    round_reset = bool(arena.get("roundResetPending")) or bool(feedback.get("roundResetPending"))
    target_visible = resolve_target_visible(feedback, target)
    projectile_risk = bool(feedback.get("projectileThreatActive")) or has_prompt_projectiles
    self_cornered = bool(feedback.get("selfCornered")) or bool(arena.get("selfCornered"))
    target_cornered = bool(feedback.get("targetCornered")) or bool(arena.get("targetCornered"))
    in_melee = bool(arena.get("targetInMeleeRange"))
    in_shoot = bool(arena.get("targetInShootRange"))
    target_above = bool(arena.get("targetAbove"))
    target_melee_threat = bool(feedback.get("targetMeleeThreatActive")) or bool(target.get("isMeleeActive"))
    target_ranged_threat = bool(feedback.get("targetRangedThreatActive"))
    target_ultimate_threat = bool(feedback.get("targetUltimateThreatActive")) or bool(target.get("isUltimateActive"))
    target_vulnerable = bool(target.get("isHitStunned"))
    self_hitstunned = bool(self_state.get("isHitStunned"))
    self_arrows = max(0, int(self_state.get("arrows", 0) or 0))
    target_arrows = max(0, int(target.get("arrows", 0) or 0))
    arrow_lead = self_arrows - target_arrows
    try:
        horizontal_distance_value = float(arena.get("horizontalDistance", 9999.0) or 9999.0)
    except (TypeError, ValueError):
        horizontal_distance_value = 9999.0
    movement_stalled = has_movement_stall_feedback(state)
    vulnerable_out_of_range = has_vulnerable_out_of_range_feedback(state)
    shot_out_of_range = has_shot_out_of_range_feedback(state)
    empty_shot = has_empty_shot_feedback(state)
    missed_arrow_recovery = has_missed_arrow_recovery_feedback(state)
    recover_arrow = has_recover_arrow_feedback(state)
    missed_anti_air = has_missed_anti_air_feedback(state)
    anti_air_opportunity = has_anti_air_opportunity_feedback(state)
    missed_projectile_defense = has_missed_projectile_defense_feedback(state)
    projectile_threat_feedback = has_projectile_threat_feedback(state)
    missed_punish_window = has_missed_punish_window_feedback(state)
    punish_window_available = has_punish_window_available_feedback(state)
    missed_corner_escape = has_missed_corner_escape_feedback(state)
    missed_ultimate_escape = has_missed_ultimate_escape_feedback(state)
    missed_melee_escape = has_missed_melee_escape_feedback(state)
    missed_ranged_response = has_missed_ranged_response_feedback(state)
    try:
        dash_cooldown_left = float(self_state.get("dashCooldownLeft", 0.0) or 0.0)
    except (TypeError, ValueError):
        dash_cooldown_left = 9999.0
    can_dash = dash_cooldown_left <= 0.01 and not bool(self_state.get("isDashing"))
    self_grounded = bool(self_state.get("isGrounded", True))
    can_parry_projectile = bool(self_state.get("canParryProjectile", False))

    if round_reset or self_hitstunned:
        return tuned

    if target_visible and "waiting_for_target" in str(tuned.get("reason", "")).lower():
        tuned["mode"] = "pressure"
        tuned["preferredRange"] = 320 if horizontal_distance_value <= 520 else min(420, int(horizontal_distance_value))
        tuned["advanceBias"] = max(tuned["advanceBias"], 0.72)
        tuned["shootBias"] = max(tuned["shootBias"], 0.5)
        tuned["meleeBias"] = max(tuned["meleeBias"], 0.48)
        tuned["dashBias"] = max(tuned["dashBias"], 0.62 if can_dash else 0.38)
        tuned["jumpBias"] = max(tuned["jumpBias"], 0.24)
        tuned["antiProjectile"] = "hold"
        tuned["cornerEscapeBias"] = min(tuned["cornerEscapeBias"], 0.32)
        tuned["reason"] = "target_reacquired"
        return tuned

    if movement_stalled and target_visible and not projectile_risk:
        tuned["mode"] = "retreat"
        tuned["preferredRange"] = max(tuned["preferredRange"], 320)
        tuned["advanceBias"] = min(tuned["advanceBias"], 0.18)
        tuned["shootBias"] = min(tuned["shootBias"], 0.28)
        tuned["meleeBias"] = min(tuned["meleeBias"], 0.24)
        tuned["dashBias"] = max(tuned["dashBias"], 0.92 if can_dash else 0.62)
        tuned["jumpBias"] = max(tuned["jumpBias"], 0.78 if self_grounded else 0.28)
        tuned["antiProjectile"] = "hold"
        tuned["cornerEscapeBias"] = max(tuned["cornerEscapeBias"], 0.92)
        tuned["reason"] = "heuristic_movement_stall_escape"
        return tuned

    if missed_projectile_defense or projectile_threat_feedback:
        if can_dash:
            defensive_anti_projectile = "dash"
        elif self_grounded:
            defensive_anti_projectile = "jump"
        elif can_parry_projectile:
            defensive_anti_projectile = "parry_prefer"
        else:
            defensive_anti_projectile = "hold"
        tuned["mode"] = "retreat"
        tuned["preferredRange"] = max(tuned["preferredRange"], 300)
        tuned["advanceBias"] = min(tuned["advanceBias"], 0.16)
        tuned["shootBias"] = min(tuned["shootBias"], 0.28)
        tuned["meleeBias"] = min(tuned["meleeBias"], 0.22)
        tuned["dashBias"] = max(tuned["dashBias"], 0.94 if can_dash else 0.52)
        tuned["jumpBias"] = max(tuned["jumpBias"], 0.56 if self_grounded else 0.22)
        tuned["antiProjectile"] = defensive_anti_projectile
        tuned["cornerEscapeBias"] = max(tuned["cornerEscapeBias"], 0.82)
        tuned["reason"] = "missed_projectile_defense" if missed_projectile_defense else "projectile_threat_feedback"
        return tuned

    if missed_corner_escape and self_cornered:
        tuned["mode"] = "retreat"
        tuned["preferredRange"] = max(tuned["preferredRange"], 320)
        tuned["advanceBias"] = min(tuned["advanceBias"], 0.18)
        tuned["shootBias"] = min(tuned["shootBias"], 0.32)
        tuned["meleeBias"] = min(tuned["meleeBias"], 0.26)
        tuned["dashBias"] = max(tuned["dashBias"], 0.9 if can_dash else 0.6)
        tuned["jumpBias"] = max(tuned["jumpBias"], 0.58 if self_grounded else 0.24)
        tuned["antiProjectile"] = "hold"
        tuned["cornerEscapeBias"] = max(tuned["cornerEscapeBias"], 0.94)
        tuned["reason"] = "missed_corner_escape"
        return tuned

    if not target_visible or projectile_risk:
        return tuned

    if target_ultimate_threat or missed_ultimate_escape:
        tuned["mode"] = "retreat"
        tuned["preferredRange"] = max(tuned["preferredRange"], 360)
        tuned["advanceBias"] = min(tuned["advanceBias"], 0.12)
        tuned["shootBias"] = min(tuned["shootBias"], 0.28)
        tuned["meleeBias"] = min(tuned["meleeBias"], 0.22)
        tuned["dashBias"] = max(tuned["dashBias"], 0.94 if can_dash else 0.64)
        tuned["jumpBias"] = max(tuned["jumpBias"], 0.42 if self_grounded else 0.24)
        tuned["antiProjectile"] = "hold"
        tuned["cornerEscapeBias"] = max(tuned["cornerEscapeBias"], 0.78)
        tuned["reason"] = "missed_ultimate_escape" if missed_ultimate_escape else "target_ultimate_threat"
        return tuned

    if target_melee_threat or missed_melee_escape:
        tuned["mode"] = "retreat"
        tuned["preferredRange"] = max(tuned["preferredRange"], 260)
        tuned["advanceBias"] = min(tuned["advanceBias"], 0.18)
        tuned["shootBias"] = min(tuned["shootBias"], 0.34)
        tuned["meleeBias"] = min(tuned["meleeBias"], 0.26)
        tuned["dashBias"] = max(tuned["dashBias"], 0.88 if can_dash else 0.58)
        tuned["jumpBias"] = max(tuned["jumpBias"], 0.34 if self_grounded else 0.18)
        tuned["antiProjectile"] = "hold"
        tuned["cornerEscapeBias"] = max(tuned["cornerEscapeBias"], 0.7)
        tuned["reason"] = "missed_melee_escape" if missed_melee_escape else "target_melee_threat"
        return tuned

    if target_ranged_threat or missed_ranged_response:
        tuned["mode"] = "retreat" if self_arrows <= 0 else "pressure"
        tuned["preferredRange"] = max(tuned["preferredRange"], 300)
        tuned["advanceBias"] = min(tuned["advanceBias"], 0.24) if self_arrows <= 0 else max(tuned["advanceBias"], 0.62)
        tuned["shootBias"] = min(tuned["shootBias"], 0.28) if self_arrows <= 0 else max(tuned["shootBias"], 0.58 if in_shoot else 0.4)
        tuned["meleeBias"] = min(tuned["meleeBias"], 0.28)
        tuned["dashBias"] = max(tuned["dashBias"], 0.86 if can_dash else 0.56)
        tuned["jumpBias"] = max(tuned["jumpBias"], 0.42 if self_grounded else 0.18)
        tuned["antiProjectile"] = "dash" if can_dash else "hold"
        tuned["cornerEscapeBias"] = max(tuned["cornerEscapeBias"], 0.72 if self_cornered else 0.38)
        tuned["reason"] = "missed_ranged_response" if missed_ranged_response else "target_ranged_threat"
        return tuned

    if (missed_anti_air or anti_air_opportunity) and target_above:
        anti_air_shoot_bias = 0.42
        if in_shoot and self_arrows > 0:
            anti_air_shoot_bias = 0.72 if anti_air_opportunity else 0.68
        anti_air_jump_bias = 0.34
        if self_grounded:
            anti_air_jump_bias = 0.56 if anti_air_opportunity else 0.74
        tuned["mode"] = "pressure"
        tuned["preferredRange"] = min(tuned["preferredRange"], 300)
        tuned["advanceBias"] = max(tuned["advanceBias"], 0.78)
        tuned["shootBias"] = max(tuned["shootBias"], anti_air_shoot_bias)
        tuned["meleeBias"] = min(tuned["meleeBias"], 0.42)
        tuned["dashBias"] = max(tuned["dashBias"], 0.68 if can_dash else 0.48)
        tuned["jumpBias"] = max(tuned["jumpBias"], anti_air_jump_bias)
        tuned["antiAir"] = True
        tuned["antiProjectile"] = "hold"
        tuned["cornerEscapeBias"] = min(tuned["cornerEscapeBias"], 0.3)
        tuned["reason"] = "missed_anti_air" if missed_anti_air else "anti_air_opportunity"
        return tuned

    if (missed_punish_window or punish_window_available) and (in_melee or in_shoot):
        tuned["mode"] = "punish"
        tuned["preferredRange"] = min(tuned["preferredRange"], 160)
        tuned["advanceBias"] = max(tuned["advanceBias"], 0.92)
        tuned["shootBias"] = max(tuned["shootBias"], 0.68 if in_shoot and self_arrows > 0 else 0.42)
        tuned["meleeBias"] = max(tuned["meleeBias"], 0.92 if in_melee else 0.74)
        tuned["dashBias"] = max(tuned["dashBias"], 0.84 if can_dash else 0.58)
        tuned["jumpBias"] = min(max(tuned["jumpBias"], 0.16), 0.32)
        tuned["antiProjectile"] = "hold"
        tuned["cornerEscapeBias"] = min(tuned["cornerEscapeBias"], 0.2)
        tuned["reason"] = "missed_punish_window" if missed_punish_window else "punish_window_available"
        return tuned

    if vulnerable_out_of_range:
        tuned["mode"] = "pressure"
        tuned["preferredRange"] = min(tuned["preferredRange"], 220)
        tuned["advanceBias"] = max(tuned["advanceBias"], 0.94)
        tuned["shootBias"] = max(tuned["shootBias"], 0.56) if in_shoot else min(tuned["shootBias"], 0.42)
        tuned["meleeBias"] = max(tuned["meleeBias"], 0.82 if in_melee else 0.72)
        tuned["dashBias"] = max(tuned["dashBias"], 0.86 if can_dash else 0.58)
        tuned["jumpBias"] = max(tuned["jumpBias"], 0.24)
        tuned["antiProjectile"] = "hold"
        tuned["cornerEscapeBias"] = min(tuned["cornerEscapeBias"], 0.24)
        tuned["reason"] = "vulnerable_out_of_range"
        return tuned

    if shot_out_of_range:
        tuned["mode"] = "pressure"
        tuned["preferredRange"] = min(tuned["preferredRange"], 300)
        tuned["advanceBias"] = max(tuned["advanceBias"], 0.9)
        tuned["shootBias"] = max(tuned["shootBias"], 0.56) if in_shoot else min(tuned["shootBias"], 0.42)
        tuned["meleeBias"] = max(tuned["meleeBias"], 0.68 if in_melee else 0.58)
        tuned["dashBias"] = max(tuned["dashBias"], 0.82 if can_dash else 0.54)
        tuned["jumpBias"] = max(tuned["jumpBias"], 0.24)
        tuned["antiProjectile"] = "hold"
        tuned["cornerEscapeBias"] = min(tuned["cornerEscapeBias"], 0.28)
        tuned["reason"] = "shot_out_of_range"
        return tuned

    if empty_shot:
        tuned["mode"] = "retreat"
        tuned["preferredRange"] = max(tuned["preferredRange"], 320)
        tuned["advanceBias"] = min(tuned["advanceBias"], 0.18)
        tuned["shootBias"] = min(tuned["shootBias"], 0.08)
        tuned["meleeBias"] = min(tuned["meleeBias"], 0.28)
        tuned["dashBias"] = max(tuned["dashBias"], 0.82 if can_dash else 0.54)
        tuned["jumpBias"] = max(tuned["jumpBias"], 0.32 if self_grounded else 0.16)
        tuned["antiProjectile"] = "parry_prefer" if bool(self_state.get("canParryProjectile", False)) else "hold"
        tuned["cornerEscapeBias"] = max(tuned["cornerEscapeBias"], 0.84)
        tuned["reason"] = "empty_shot_recover_arrow"
        return tuned

    if missed_arrow_recovery:
        tuned["mode"] = "retreat"
        tuned["preferredRange"] = max(tuned["preferredRange"], 300)
        tuned["advanceBias"] = min(tuned["advanceBias"], 0.2)
        tuned["shootBias"] = min(tuned["shootBias"], 0.16)
        tuned["meleeBias"] = min(tuned["meleeBias"], 0.3)
        tuned["dashBias"] = max(tuned["dashBias"], 0.82 if can_dash else 0.54)
        tuned["jumpBias"] = max(tuned["jumpBias"], 0.34 if self_grounded else 0.16)
        tuned["antiProjectile"] = "hold"
        tuned["cornerEscapeBias"] = max(tuned["cornerEscapeBias"], 0.84)
        tuned["reason"] = "missed_arrow_recovery"
        return tuned

    if recover_arrow and self_arrows <= 0:
        tuned["mode"] = "retreat"
        tuned["preferredRange"] = max(tuned["preferredRange"], 300)
        tuned["advanceBias"] = min(tuned["advanceBias"], 0.18)
        tuned["shootBias"] = min(tuned["shootBias"], 0.14)
        tuned["meleeBias"] = min(tuned["meleeBias"], 0.28)
        tuned["dashBias"] = max(tuned["dashBias"], 0.84 if can_dash else 0.54)
        tuned["jumpBias"] = max(tuned["jumpBias"], 0.34 if self_grounded else 0.16)
        tuned["antiProjectile"] = "hold"
        tuned["cornerEscapeBias"] = max(tuned["cornerEscapeBias"], 0.86)
        tuned["reason"] = "recover_arrow_feedback"
        return tuned

    if target_vulnerable:
        tuned["mode"] = "punish"
        tuned["preferredRange"] = min(tuned["preferredRange"], 180)
        tuned["advanceBias"] = max(tuned["advanceBias"], 0.9)
        tuned["shootBias"] = max(tuned["shootBias"], 0.58 if in_shoot else 0.4)
        tuned["meleeBias"] = max(tuned["meleeBias"], 0.88 if in_melee else 0.76)
        tuned["dashBias"] = max(tuned["dashBias"], 0.8)
        tuned["jumpBias"] = max(tuned["jumpBias"], 0.22)
        tuned["antiProjectile"] = "hold"
        tuned["cornerEscapeBias"] = min(tuned["cornerEscapeBias"], 0.2)
        tuned["reason"] = "target_vulnerable"
        return tuned

    if target_arrows <= 0 and self_arrows > 0:
        tuned["mode"] = "pressure"
        tuned["preferredRange"] = min(tuned["preferredRange"], 180)
        tuned["advanceBias"] = max(tuned["advanceBias"], 0.92)
        tuned["shootBias"] = max(tuned["shootBias"], 0.58 if in_shoot else 0.42)
        tuned["meleeBias"] = max(tuned["meleeBias"], 0.86 if in_melee else 0.74)
        tuned["dashBias"] = max(tuned["dashBias"], 0.8)
        tuned["jumpBias"] = max(tuned["jumpBias"], 0.26)
        tuned["antiProjectile"] = "hold"
        tuned["cornerEscapeBias"] = min(tuned["cornerEscapeBias"], 0.22)
        tuned["reason"] = "last_arrow_pressure"
        return tuned

    if self_arrows <= 0 and target_arrows > 0:
        tuned["mode"] = "retreat"
        tuned["preferredRange"] = max(tuned["preferredRange"], 320)
        tuned["advanceBias"] = min(tuned["advanceBias"], 0.2)
        tuned["shootBias"] = min(tuned["shootBias"], 0.22)
        tuned["meleeBias"] = min(tuned["meleeBias"], 0.3)
        tuned["dashBias"] = max(tuned["dashBias"], 0.78)
        tuned["jumpBias"] = max(tuned["jumpBias"], 0.34 if not self_cornered else 0.2)
        tuned["antiProjectile"] = "parry_prefer" if bool(self_state.get("canParryProjectile", False)) else "hold"
        tuned["cornerEscapeBias"] = max(tuned["cornerEscapeBias"], 0.78)
        tuned["reason"] = "arrow_disadvantage"
        return tuned

    if arrow_lead > 0 and target_arrows <= 1 and self_arrows > 0:
        tuned["mode"] = "pressure"
        tuned["preferredRange"] = min(tuned["preferredRange"], 240)
        tuned["advanceBias"] = max(tuned["advanceBias"], 0.88)
        tuned["shootBias"] = max(tuned["shootBias"], 0.56 if in_shoot else 0.38)
        tuned["meleeBias"] = max(tuned["meleeBias"], 0.8 if in_melee else 0.66)
        tuned["dashBias"] = max(tuned["dashBias"], 0.72)
        tuned["jumpBias"] = max(tuned["jumpBias"], 0.24)
        tuned["antiProjectile"] = "hold"
        tuned["cornerEscapeBias"] = min(tuned["cornerEscapeBias"], 0.26)
        tuned["reason"] = "arrow_advantage"
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


def build_heuristic_intent(state: dict[str, Any]) -> dict[str, Any]:
    def as_dict(value: Any) -> dict[str, Any]:
        return value if isinstance(value, dict) else {}

    def as_list(value: Any) -> list[Any]:
        return value if isinstance(value, list) else []

    def read_float(source: dict[str, Any], key: str, fallback: float) -> float:
        try:
            return float(source.get(key, fallback))
        except (TypeError, ValueError):
            return fallback

    def read_int(source: dict[str, Any], key: str, fallback: int) -> int:
        try:
            return int(source.get(key, fallback))
        except (TypeError, ValueError):
            return fallback

    prompt_state = as_dict(state.get("promptState"))
    feedback = as_dict(state.get("executorFeedback"))
    self_state = as_dict(prompt_state.get("self"))
    target_state = as_dict(prompt_state.get("target"))
    arena = as_dict(prompt_state.get("arena"))
    events = [str(item) for item in as_list(prompt_state.get("events"))]
    dangerous_projectiles = [as_dict(item) for item in as_list(prompt_state.get("dangerousProjectiles"))]

    target_visible = resolve_target_visible(feedback, target_state)
    self_dead = bool(self_state.get("isDead", False))
    round_reset = bool(arena.get("roundResetPending", False)) or bool(feedback.get("roundResetPending", False)) or "round_reset_started" in events

    target_slot = read_int(target_state, "slotId", 1) or 1
    horizontal_distance = read_float(arena, "horizontalDistance", 9999.0)
    vertical_distance = read_float(arena, "verticalDistance", 9999.0)
    target_in_melee = bool(arena.get("targetInMeleeRange", False))
    target_in_ultimate = bool(arena.get("targetInUltimateRange", False))
    target_in_shoot = bool(arena.get("targetInShootRange", False))
    target_cornered = bool(feedback.get("targetCornered", False)) or bool(arena.get("targetCornered", False))
    self_cornered = bool(feedback.get("selfCornered", False)) or bool(arena.get("selfCornered", False))
    target_above = bool(arena.get("targetAbove", False))
    self_grounded = bool(self_state.get("isGrounded", True))
    self_arrows = max(0, read_int(self_state, "arrows", 0))
    target_arrows = max(0, read_int(target_state, "arrows", 0))
    arrow_lead = self_arrows - target_arrows
    self_has_arrows = self_arrows > 0
    can_shoot = self_has_arrows and read_float(self_state, "shootCooldownLeft", 0.0) <= 0.01
    can_melee = read_float(self_state, "meleeCooldownLeft", 0.0) <= 0.01 and not bool(self_state.get("isMeleeActive", False))
    can_dash = read_float(self_state, "dashCooldownLeft", 0.0) <= 0.01 and not bool(self_state.get("isDashing", False))
    can_ultimate = read_float(self_state, "ultimateCooldownLeft", 0.0) <= 0.01 and not bool(self_state.get("isUltimateActive", False))
    target_melee_threat = bool(feedback.get("targetMeleeThreatActive", False)) or bool(target_state.get("isMeleeActive", False))
    target_ranged_threat = bool(feedback.get("targetRangedThreatActive", False))
    target_ultimate_threat = bool(feedback.get("targetUltimateThreatActive", False)) or bool(target_state.get("isUltimateActive", False))
    target_vulnerable = bool(target_state.get("isHitStunned", False)) or "target_became_vulnerable" in events
    movement_stalled = has_movement_stall_feedback(state)
    vulnerable_out_of_range = has_vulnerable_out_of_range_feedback(state)
    shot_out_of_range = has_shot_out_of_range_feedback(state)
    empty_shot = has_empty_shot_feedback(state)
    missed_arrow_recovery = has_missed_arrow_recovery_feedback(state)
    recover_arrow = has_recover_arrow_feedback(state)
    missed_anti_air = has_missed_anti_air_feedback(state)
    anti_air_opportunity = has_anti_air_opportunity_feedback(state)
    missed_projectile_defense = has_missed_projectile_defense_feedback(state)
    projectile_threat_feedback = has_projectile_threat_feedback(state)
    missed_punish_window = has_missed_punish_window_feedback(state)
    punish_window_available = has_punish_window_available_feedback(state)
    missed_corner_escape = has_missed_corner_escape_feedback(state)
    missed_ultimate_escape = has_missed_ultimate_escape_feedback(state)
    missed_melee_escape = has_missed_melee_escape_feedback(state)
    missed_ranged_response = has_missed_ranged_response_feedback(state)

    projectile_eta: float | None = None
    for projectile in dangerous_projectiles:
        try:
            eta = float(projectile.get("etaSeconds", -1.0))
        except (TypeError, ValueError):
            continue
        if eta < 0.0:
            continue
        projectile_eta = eta if projectile_eta is None else min(projectile_eta, eta)

    intent: dict[str, Any] = {
        "mode": "pressure",
        "preferredRange": 320,
        "advanceBias": 0.72,
        "shootBias": 0.5,
        "meleeBias": 0.62,
        "dashBias": 0.6,
        "jumpBias": 0.24,
        "antiProjectile": "hold",
        "antiAir": target_above,
        "punishRecovery": True,
        "cornerEscapeBias": 0.28,
        "focusTargetSlot": target_slot,
        "expiresInMs": 360,
        "reason": "heuristic_neutral_pressure",
    }

    if not target_visible or self_dead:
        intent.update({
            "mode": "stabilize",
            "preferredRange": 280,
            "advanceBias": 0.2,
            "shootBias": 0.2,
            "meleeBias": 0.2,
            "dashBias": 0.15,
            "jumpBias": 0.15,
            "antiProjectile": "hold",
            "antiAir": target_above,
            "cornerEscapeBias": 0.4,
            "reason": "heuristic_waiting_for_target",
        })
        return intent

    if round_reset:
        intent.update({
            "mode": "stabilize",
            "preferredRange": 260,
            "advanceBias": 0.2,
            "shootBias": 0.25,
            "meleeBias": 0.25,
            "dashBias": 0.2,
            "jumpBias": 0.2,
            "antiProjectile": "hold",
            "cornerEscapeBias": 0.75,
            "reason": "heuristic_round_reset",
        })
        return intent

    if projectile_eta is not None:
        if projectile_eta <= 0.2 and can_dash:
            intent.update({
                "mode": "retreat",
                "preferredRange": 260,
                "advanceBias": 0.15,
                "shootBias": 0.25,
                "meleeBias": 0.2,
                "dashBias": 0.95,
                "jumpBias": 0.25,
                "antiProjectile": "dash",
                "cornerEscapeBias": 0.8,
                "reason": "heuristic_projectile_dash",
            })
        elif projectile_eta <= 0.35 and self_grounded:
            intent.update({
                "mode": "retreat",
                "preferredRange": 260,
                "advanceBias": 0.18,
                "shootBias": 0.3,
                "meleeBias": 0.2,
                "dashBias": 0.55,
                "jumpBias": 0.9,
                "antiProjectile": "jump",
                "cornerEscapeBias": 0.72,
                "reason": "heuristic_projectile_jump",
            })
        else:
            intent.update({
                "mode": "stabilize",
                "preferredRange": 280,
                "advanceBias": 0.25,
                "shootBias": 0.35,
                "meleeBias": 0.25,
                "dashBias": 0.3,
                "jumpBias": 0.3,
                "antiProjectile": "parry_prefer" if bool(self_state.get("canParryProjectile", False)) else "hold",
                "cornerEscapeBias": 0.62,
                "reason": "heuristic_projectile_hold",
        })
        return intent

    if missed_projectile_defense or projectile_threat_feedback:
        if can_dash:
            anti_projectile = "dash"
        elif self_grounded:
            anti_projectile = "jump"
        elif bool(self_state.get("canParryProjectile", False)):
            anti_projectile = "parry_prefer"
        else:
            anti_projectile = "hold"

        intent.update({
            "mode": "retreat",
            "preferredRange": 300,
            "advanceBias": 0.16,
            "shootBias": 0.28,
            "meleeBias": 0.22,
            "dashBias": 0.94 if can_dash else 0.52,
            "jumpBias": 0.56 if self_grounded else 0.22,
            "antiProjectile": anti_projectile,
            "cornerEscapeBias": 0.82,
            "reason": "heuristic_missed_projectile_defense" if missed_projectile_defense else "heuristic_projectile_threat_feedback",
        })
        return intent

    if target_ultimate_threat or missed_ultimate_escape:
        intent.update({
            "mode": "retreat",
            "preferredRange": 360,
            "advanceBias": 0.1,
            "shootBias": 0.24,
            "meleeBias": 0.18,
            "dashBias": 0.95 if can_dash else 0.62,
            "jumpBias": 0.42 if self_grounded else 0.2,
            "antiProjectile": "hold",
            "cornerEscapeBias": 0.82,
            "reason": "heuristic_missed_ultimate_escape" if missed_ultimate_escape else "heuristic_ultimate_escape",
        })
        return intent

    if target_melee_threat or missed_melee_escape:
        intent.update({
            "mode": "retreat",
            "preferredRange": 280,
            "advanceBias": 0.16,
            "shootBias": 0.32,
            "meleeBias": 0.22,
            "dashBias": 0.9 if can_dash else 0.56,
            "jumpBias": 0.35 if self_grounded else 0.16,
            "antiProjectile": "hold",
            "cornerEscapeBias": 0.76,
            "reason": "heuristic_missed_melee_escape" if missed_melee_escape else "heuristic_melee_escape",
        })
        return intent

    if (target_ranged_threat or missed_ranged_response) and not target_vulnerable:
        if self_arrows > 0 and can_shoot and target_in_shoot:
            intent.update({
                "mode": "pressure",
                "preferredRange": 260,
                "advanceBias": 0.62,
                "shootBias": 0.66,
                "meleeBias": 0.34,
                "dashBias": 0.86 if can_dash else 0.54,
                "jumpBias": 0.38 if self_grounded else 0.16,
                "antiProjectile": "dash" if can_dash else "hold",
                "cornerEscapeBias": 0.4,
                "reason": "heuristic_missed_ranged_response" if missed_ranged_response else "heuristic_ranged_interrupt",
            })
            return intent

        intent.update({
            "mode": "retreat",
            "preferredRange": 320,
            "advanceBias": 0.18,
            "shootBias": 0.22,
            "meleeBias": 0.24,
            "dashBias": 0.84 if can_dash else 0.52,
            "jumpBias": 0.42 if self_grounded else 0.18,
            "antiProjectile": "dash" if can_dash else "hold",
            "cornerEscapeBias": 0.76 if self_cornered else 0.44,
            "reason": "heuristic_missed_ranged_response" if missed_ranged_response else "heuristic_ranged_dodge",
        })
        return intent

    if movement_stalled:
        intent.update({
            "mode": "retreat",
            "preferredRange": 320,
            "advanceBias": 0.12,
            "shootBias": 0.24,
            "meleeBias": 0.2,
            "dashBias": 0.94 if can_dash else 0.62,
            "jumpBias": 0.78 if self_grounded else 0.28,
            "antiProjectile": "hold",
            "cornerEscapeBias": 0.92,
            "reason": "heuristic_movement_stall_escape",
        })
        return intent

    if (missed_anti_air or anti_air_opportunity) and target_above:
        anti_air_shoot_bias = 0.42
        if target_in_shoot and can_shoot:
            anti_air_shoot_bias = 0.74 if anti_air_opportunity else 0.72
        anti_air_jump_bias = 0.34
        if self_grounded:
            anti_air_jump_bias = 0.58 if anti_air_opportunity else 0.78
        intent.update({
            "mode": "pressure",
            "preferredRange": 280,
            "advanceBias": 0.78,
            "shootBias": anti_air_shoot_bias,
            "meleeBias": 0.36,
            "dashBias": 0.72 if can_dash else 0.48,
            "jumpBias": anti_air_jump_bias,
            "antiAir": True,
            "antiProjectile": "hold",
            "cornerEscapeBias": 0.24,
            "reason": "heuristic_missed_anti_air" if missed_anti_air else "heuristic_anti_air_opportunity",
        })
        return intent

    if (missed_punish_window or punish_window_available) and (target_in_melee or target_in_shoot):
        intent.update({
            "mode": "punish",
            "preferredRange": min(160, max(120, int(horizontal_distance))),
            "advanceBias": 0.92,
            "shootBias": 0.68 if target_in_shoot and can_shoot else 0.42,
            "meleeBias": 0.92 if target_in_melee and can_melee else 0.74,
            "dashBias": 0.84 if can_dash else 0.58,
            "jumpBias": 0.18,
            "antiProjectile": "hold",
            "cornerEscapeBias": 0.18,
            "reason": "heuristic_missed_punish_window" if missed_punish_window else "heuristic_punish_window_available",
        })
        return intent

    if missed_corner_escape and self_cornered:
        intent.update({
            "mode": "retreat",
            "preferredRange": 320,
            "advanceBias": 0.16,
            "shootBias": 0.28,
            "meleeBias": 0.24,
            "dashBias": 0.9 if can_dash else 0.6,
            "jumpBias": 0.6 if self_grounded else 0.24,
            "antiProjectile": "hold",
            "cornerEscapeBias": 0.94,
            "reason": "heuristic_missed_corner_escape",
        })
        return intent

    if vulnerable_out_of_range:
        intent.update({
            "mode": "pressure",
            "preferredRange": 180,
            "advanceBias": 0.94,
            "shootBias": 0.56 if target_in_shoot and can_shoot else 0.38,
            "meleeBias": 0.84 if can_melee else 0.68,
            "dashBias": 0.86 if can_dash else 0.56,
            "jumpBias": 0.24,
            "antiProjectile": "hold",
            "cornerEscapeBias": 0.22,
            "reason": "heuristic_close_vulnerable_target",
        })
        return intent

    if shot_out_of_range:
        intent.update({
            "mode": "pressure",
            "preferredRange": 260,
            "advanceBias": 0.9,
            "shootBias": 0.56 if target_in_shoot and can_shoot else 0.36,
            "meleeBias": 0.7 if can_melee else 0.52,
            "dashBias": 0.82 if can_dash else 0.52,
            "jumpBias": 0.24,
            "antiProjectile": "hold",
            "cornerEscapeBias": 0.26,
            "reason": "heuristic_close_shot_range",
        })
        return intent

    if empty_shot:
        intent.update({
            "mode": "retreat",
            "preferredRange": 320,
            "advanceBias": 0.14,
            "shootBias": 0.06,
            "meleeBias": 0.24,
            "dashBias": 0.84 if can_dash else 0.52,
            "jumpBias": 0.34 if self_grounded else 0.16,
            "antiProjectile": "parry_prefer" if bool(self_state.get("canParryProjectile", False)) else "hold",
            "cornerEscapeBias": 0.86,
            "reason": "heuristic_recover_arrow_after_empty_shot",
        })
        return intent

    if missed_arrow_recovery:
        intent.update({
            "mode": "retreat",
            "preferredRange": 300,
            "advanceBias": 0.18,
            "shootBias": 0.14,
            "meleeBias": 0.28,
            "dashBias": 0.82 if can_dash else 0.52,
            "jumpBias": 0.34 if self_grounded else 0.16,
            "antiProjectile": "hold",
            "cornerEscapeBias": 0.86,
            "reason": "heuristic_recover_missed_arrow",
        })
        return intent

    if recover_arrow and self_arrows <= 0:
        intent.update({
            "mode": "retreat",
            "preferredRange": 300,
            "advanceBias": 0.16,
            "shootBias": 0.14,
            "meleeBias": 0.28,
            "dashBias": 0.84 if can_dash else 0.54,
            "jumpBias": 0.34 if self_grounded else 0.16,
            "antiProjectile": "hold",
            "cornerEscapeBias": 0.86,
            "reason": "heuristic_recover_arrow_feedback",
        })
        return intent

    if self_cornered and horizontal_distance < 280:
        intent.update({
            "mode": "retreat",
            "preferredRange": 260,
            "advanceBias": 0.2,
            "shootBias": 0.4 if can_shoot else 0.2,
            "meleeBias": 0.3,
            "dashBias": 0.82,
            "jumpBias": 0.35 if self_grounded else 0.15,
            "antiProjectile": "hold",
            "cornerEscapeBias": 0.85,
            "reason": "heuristic_escape_corner",
        })
        return intent

    if target_arrows <= 0 and self_arrows > 0 and not target_vulnerable:
        intent.update({
            "mode": "pressure",
            "preferredRange": 180,
            "advanceBias": 0.92,
            "shootBias": 0.58 if can_shoot else 0.4,
            "meleeBias": 0.88 if can_melee else 0.72,
            "dashBias": 0.82 if can_dash else 0.56,
            "jumpBias": 0.26,
            "antiProjectile": "hold",
            "cornerEscapeBias": 0.2,
            "reason": "heuristic_last_arrow_pressure",
        })
        return intent

    if self_arrows <= 0 and target_arrows > 0 and not target_vulnerable:
        intent.update({
            "mode": "retreat",
            "preferredRange": 320,
            "advanceBias": 0.18,
            "shootBias": 0.2,
            "meleeBias": 0.28,
            "dashBias": 0.78 if can_dash else 0.45,
            "jumpBias": 0.36 if self_grounded else 0.18,
            "antiProjectile": "parry_prefer" if bool(self_state.get("canParryProjectile", False)) else "hold",
            "cornerEscapeBias": 0.82,
            "reason": "heuristic_arrow_disadvantage",
        })
        return intent

    if arrow_lead > 0 and target_arrows <= 1 and not target_vulnerable:
        intent.update({
            "mode": "pressure",
            "preferredRange": 240,
            "advanceBias": 0.84,
            "shootBias": 0.62 if can_shoot else 0.38,
            "meleeBias": 0.82 if can_melee else 0.68,
            "dashBias": 0.72 if can_dash else 0.5,
            "jumpBias": 0.24,
            "antiProjectile": "hold",
            "cornerEscapeBias": 0.24,
            "reason": "heuristic_arrow_advantage",
        })
        return intent

    if target_vulnerable and (target_in_melee or target_in_ultimate or target_in_shoot):
        intent.update({
            "mode": "punish",
            "preferredRange": min(180, max(120, int(horizontal_distance))),
            "advanceBias": 0.9,
            "shootBias": 0.68 if can_shoot else 0.42,
            "meleeBias": 0.92 if can_melee else 0.74,
            "dashBias": 0.84 if can_dash else 0.6,
            "jumpBias": 0.18,
            "antiProjectile": "hold",
            "cornerEscapeBias": 0.18,
            "reason": "heuristic_punish_window",
        })
        return intent

    if target_cornered:
        intent.update({
            "mode": "pressure",
            "preferredRange": 220,
            "advanceBias": 0.88,
            "shootBias": 0.64 if can_shoot else 0.42,
            "meleeBias": 0.84,
            "dashBias": 0.74,
            "jumpBias": 0.28,
            "antiProjectile": "hold",
            "cornerEscapeBias": 0.22,
            "reason": "heuristic_corner_pressure",
        })
        return intent

    if target_in_melee and can_melee:
        intent.update({
            "mode": "pressure",
            "preferredRange": 140,
            "advanceBias": 0.86,
            "shootBias": 0.36 if can_shoot else 0.24,
            "meleeBias": 0.92,
            "dashBias": 0.68,
            "jumpBias": 0.2,
            "antiProjectile": "hold",
            "cornerEscapeBias": 0.25,
            "reason": "heuristic_melee_pressure",
        })
        return intent

    if target_in_shoot and self_has_arrows and (horizontal_distance <= 420 or can_shoot):
        intent.update({
            "mode": "zone",
            "preferredRange": 420,
            "advanceBias": 0.56,
            "shootBias": 0.84,
            "meleeBias": 0.24,
            "dashBias": 0.46,
            "jumpBias": 0.3,
            "antiProjectile": "hold",
            "cornerEscapeBias": 0.26,
            "reason": "heuristic_zone_spacing",
        })
        return intent

    if target_above:
        intent.update({
            "mode": "pressure",
            "preferredRange": 300,
            "advanceBias": 0.74,
            "shootBias": 0.78 if can_shoot else 0.42,
            "meleeBias": 0.44,
            "dashBias": 0.58,
            "jumpBias": 0.42,
            "antiAir": True,
            "antiProjectile": "hold",
            "cornerEscapeBias": 0.24,
            "reason": "heuristic_anti_air",
        })
        return intent

    if horizontal_distance > 520 and can_dash:
        intent.update({
            "mode": "pressure",
            "preferredRange": 360,
            "advanceBias": 0.9,
            "shootBias": 0.58 if can_shoot else 0.28,
            "meleeBias": 0.54,
            "dashBias": 0.82,
            "jumpBias": 0.26,
            "antiProjectile": "hold",
            "cornerEscapeBias": 0.24,
            "reason": "heuristic_close_distance",
        })
        return intent

    if horizontal_distance < 220:
        intent.update({
            "mode": "stabilize",
            "preferredRange": 260,
            "advanceBias": 0.28,
            "shootBias": 0.4 if can_shoot else 0.24,
            "meleeBias": 0.46,
            "dashBias": 0.38,
            "jumpBias": 0.22,
            "antiProjectile": "hold",
            "cornerEscapeBias": 0.34,
            "reason": "heuristic_hold_space",
        })
        return intent

    intent.update({
        "mode": "pressure",
        "preferredRange": 320,
        "advanceBias": 0.78,
        "shootBias": 0.54 if can_shoot else 0.28,
        "meleeBias": 0.6,
        "dashBias": 0.66,
        "jumpBias": 0.24,
        "antiProjectile": "hold",
        "cornerEscapeBias": 0.28,
        "reason": "heuristic_default_pressure",
    })
    return intent


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
        "Treat executorFeedback.botFeedback as the executor's live diagnosis of what just happened; correct the next intent around it.\n"
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


def should_send_idle_heartbeat(
    session_id: str,
    last_heartbeat_session_id: str,
    *,
    last_heartbeat_at: float,
    now: float,
    interval_seconds: float,
) -> bool:
    if not session_id:
        return False
    if session_id != last_heartbeat_session_id:
        return True
    return now - last_heartbeat_at >= max(0.25, interval_seconds)


def dict_or_empty(value: Any) -> dict[str, Any]:
    return value if isinstance(value, dict) else {}


def build_idle_heartbeat_note(state: dict[str, Any]) -> str:
    last_intent = dict_or_empty(state.get("lastIntent"))
    last_mode = str(last_intent.get("mode", "") or "-")
    last_reason = str(last_intent.get("reason", "") or "no-reason")
    return f"Idle heartbeat frame {state.get('frame', '-')}; last {last_mode} ({last_reason})"


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
    model: str = "",
) -> None:
    if not session_id:
        return

    payload: dict[str, Any] = {
        "sessionId": session_id,
        "model": str(model).strip() or DISPLAY_MODEL,
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
    runtime_provider = resolve_runtime_provider(CODEX_MODEL_PROVIDER, CODEX_PATH.exists())
    heuristic_mode = runtime_provider == HEURISTIC_PROVIDER
    agent_model = resolve_agent_model(runtime_provider)

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

    if heuristic_mode:
        log(f"codex executable not found: {CODEX_PATH}; using local heuristic fallback")
    log(f"starting live agent for slot {SLOT_ID} bot={BOT_ID or '-'} via {BROKER_BASE} provider={runtime_provider}")
    log(f"[auth-fallback] contas disponiveis: {CODEX_HOME_CANDIDATES}")

    codex_session_id = f"heuristic-slot-{SLOT_ID}-{now_ms()}" if heuristic_mode else ""
    broker_session_id = ""
    last_frame = -1
    last_turn_at = 0.0
    last_idle_heartbeat_at = 0.0
    last_idle_heartbeat_session_id = ""
    warmup_posted_session_id = ""
    memory = MemoryTracker(bot_id=BOT_ID, slot_id=SLOT_ID)

    warmup_intent: dict[str, Any] | None = None
    warmup_error = ""
    warmup_meta: dict[str, Any] = {"stdout": "", "stderr": "", "returncode": 0}
    if not heuristic_mode:
        warmup_prompt = build_warmup_prompt()
        append_trace_event("warmup_request", {
            "slotId": SLOT_ID,
            "botId": BOT_ID,
            "provider": runtime_provider,
            "model": agent_model,
            "prompt": warmup_prompt,
        })
        thread_id, warmup_intent, warmup_error, warmup_meta = run_codex_new(warmup_prompt, codex_home=current_codex_home())
        append_trace_event("warmup_response", {
            "slotId": SLOT_ID,
            "botId": BOT_ID,
            "provider": runtime_provider,
            "model": agent_model,
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
    else:
        log("warmup skipped: local heuristic fallback does not need a Codex session")

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
                "provider": runtime_provider,
                "model": agent_model,
            })
            send_heartbeat(
                broker_session_id,
                codex_session_id,
                "idle",
                thinking=False,
                note="Attached to broker session",
                model=agent_model,
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
                        model=agent_model,
                    )
                else:
                    log("failed to post warmup action to broker")

        if not should_request_turn(state, last_frame, last_turn_at):
            idle_now = time.time()
            if should_send_idle_heartbeat(
                broker_session_id,
                last_idle_heartbeat_session_id,
                last_heartbeat_at=last_idle_heartbeat_at,
                now=idle_now,
                interval_seconds=HEARTBEAT_INTERVAL_SECONDS,
            ):
                send_heartbeat(
                    broker_session_id,
                    codex_session_id,
                    "idle",
                    thinking=False,
                    note=build_idle_heartbeat_note(state),
                    model=agent_model,
                )
                last_idle_heartbeat_at = idle_now
                last_idle_heartbeat_session_id = broker_session_id
            time.sleep(POLL_INTERVAL_SECONDS)
            continue

        frame = int(state.get("frame", -1))
        payload = format_prompt_payload(state, memory)
        turn_started_ms = now_ms()

        if heuristic_mode:
            append_trace_event("heuristic_request", {
                "slotId": SLOT_ID,
                "botId": BOT_ID or payload.get("botId", ""),
                "brokerSessionId": broker_session_id,
                "codexSessionId": codex_session_id,
                "frame": frame,
                "provider": runtime_provider,
                "model": agent_model,
                "payload": payload,
            })
            send_heartbeat(
                broker_session_id,
                codex_session_id,
                "thinking",
                thinking=True,
                note=f"Heuristic thinking for frame {frame}",
                turn_started_ms=turn_started_ms,
                model=agent_model,
            )
            intent = apply_aggression_bias(build_heuristic_intent(state), state)
            append_trace_event("heuristic_response", {
                "slotId": SLOT_ID,
                "botId": BOT_ID or payload.get("botId", ""),
                "brokerSessionId": broker_session_id,
                "codexSessionId": codex_session_id,
                "frame": frame,
                "provider": runtime_provider,
                "model": agent_model,
                "intent": intent,
            })
        elif not codex_session_id:
            prompt = build_start_prompt(payload)
            append_trace_event("codex_request", {
                "slotId": SLOT_ID,
                "botId": BOT_ID or payload.get("botId", ""),
                "brokerSessionId": broker_session_id,
                "codexSessionId": codex_session_id,
                "frame": frame,
                "requestType": "start",
                "provider": runtime_provider,
                "model": agent_model,
                "prompt": prompt,
            })
            send_heartbeat(
                broker_session_id,
                codex_session_id,
                "thinking",
                thinking=True,
                note=f"Thinking for frame {frame}",
                turn_started_ms=turn_started_ms,
                model=agent_model,
            )
            thread_id, intent, error, meta = run_codex_new(prompt, codex_home=current_codex_home())
            append_trace_event("codex_response", {
                "slotId": SLOT_ID,
                "botId": BOT_ID or payload.get("botId", ""),
                "brokerSessionId": broker_session_id,
                "codexSessionId": thread_id or "",
                "frame": frame,
                "requestType": "start",
                "provider": runtime_provider,
                "model": agent_model,
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
                    model=agent_model,
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
                "frame": frame,
                "requestType": "resume",
                "provider": runtime_provider,
                "model": agent_model,
                "prompt": prompt,
            })
            send_heartbeat(
                broker_session_id,
                codex_session_id,
                "thinking",
                thinking=True,
                note=f"Thinking for frame {frame}",
                turn_started_ms=turn_started_ms,
                model=agent_model,
            )
            intent, error, meta = run_codex_resume(codex_session_id, prompt, codex_home=current_codex_home())
            append_trace_event("codex_response", {
                "slotId": SLOT_ID,
                "botId": BOT_ID or payload.get("botId", ""),
                "brokerSessionId": broker_session_id,
                "codexSessionId": codex_session_id,
                "frame": frame,
                "requestType": "resume",
                "provider": runtime_provider,
                "model": agent_model,
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
                    model=agent_model,
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
            model=agent_model,
        )
        last_frame = frame
        last_turn_at = time.time()
        if post_intent(broker_session_id, intent):
            log(f"posted action frame={last_frame} mode={intent['mode']} reason={intent['reason']}")
            append_trace_event("intent_posted", {
                "slotId": SLOT_ID,
                "botId": BOT_ID or payload.get("botId", ""),
                "brokerSessionId": broker_session_id,
                "codexSessionId": codex_session_id,
                "frame": last_frame,
                "provider": runtime_provider,
                "model": agent_model,
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
                model=agent_model,
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
                model=agent_model,
            )

        time.sleep(POLL_INTERVAL_SECONDS)


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except KeyboardInterrupt:
        log("stopped")
        raise SystemExit(0)
