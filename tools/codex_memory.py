import json
import time
from copy import deepcopy
from pathlib import Path
from typing import Any

try:
    from bot_manager import (
        BotManager,
        GlobalKnowledgeStore,
        _write_json_atomic,
        _write_text_atomic,
        compact_line,
        now_iso,
        safe_float,
        safe_int,
    )
except ModuleNotFoundError:
    from tools.bot_manager import (
        BotManager,
        GlobalKnowledgeStore,
        _write_json_atomic,
        _write_text_atomic,
        compact_line,
        now_iso,
        safe_float,
        safe_int,
    )


def slot_label(slot_id: int) -> str:
    if slot_id == 1:
        return "Slot 1"
    if slot_id == 2:
        return "Slot 2"
    return f"Slot {slot_id}"


def default_private_profile() -> dict[str, Any]:
    return {
        "updatedAt": now_iso(),
        "observationCount": 0,
        "opponentPatterns": {
            "midRangeProjectileThreats": 0,
            "jumpEscapes": 0,
            "dashEscapes": 0,
            "meleePressureStarts": 0,
            "ultimateActivations": 0,
            "vulnerabilityWindows": 0,
        },
        "selfFindings": {
            "deathsLogged": 0,
            "projectileDeaths": 0,
            "cornerDeaths": 0,
            "roundResetMistakes": 0,
            "closeRangeDeaths": 0,
            "movementStalls": 0,
        },
    }


class MemoryTracker:
    def __init__(
        self,
        bot_id: str = "",
        *,
        slot_id: int | None = None,
        manager: BotManager | None = None,
    ) -> None:
        self.manager = manager or BotManager()
        self.global_knowledge = GlobalKnowledgeStore()
        self.slot_id = safe_int(slot_id, 0)

        if compact_line(bot_id):
            self.bot_profile = self.manager.ensure_bot(compact_line(bot_id))
        elif self.slot_id > 0:
            self.bot_profile = self.manager.resolve_slot_bot(self.slot_id)
        else:
            self.bot_profile = self.manager.ensure_bot("bot-default", display_name="Bot Default")

        self.bot_id = str(self.bot_profile.get("botId", "bot-default"))
        self.bot_dir = self.manager.bot_dir(self.bot_id)
        self.reports_dir = self.bot_dir / "match_reports"
        self.reports_dir.mkdir(parents=True, exist_ok=True)

        self.events_log = self.bot_dir / "events.jsonl"
        self.death_reviews_log = self.bot_dir / "death_reviews.jsonl"
        self.round_reviews_log = self.bot_dir / "round_reviews.jsonl"
        self.series_reviews_log = self.bot_dir / "series_reviews.jsonl"
        self.private_profile_path = self.bot_dir / "current_opponent_profile.json"
        self.latest_round_report_path = self.bot_dir / "latest_round_review.md"
        self.latest_series_report_path = self.bot_dir / "latest_series_review.md"
        self.latest_series_plan_path = self.bot_dir / "latest_series_plan.md"

        self.profile = self._load_private_profile()
        self.last_agent_state: dict[str, Any] | None = None
        self.last_frame = -1
        self.latest_death_review = self._load_last_jsonl(self.death_reviews_log)
        self.latest_round_review = self._load_last_jsonl(self.round_reviews_log)
        self.latest_series_review = self._load_last_jsonl(self.series_reviews_log)
        self.latest_series_plan = self._build_latest_series_plan_payload(self.latest_series_review)
        self.latest_bot_feedback = self._load_latest_event(self.events_log, "bot_feedback")
        self.current_match: dict[str, Any] | None = None
        self.current_round: dict[str, Any] | None = None
        self._last_round_signature: tuple[Any, ...] | None = None
        self._last_series_signature: tuple[Any, ...] | None = None

    def _load_private_profile(self) -> dict[str, Any]:
        if not self.private_profile_path.exists():
            payload = default_private_profile()
            self._save_private_profile(payload)
            return payload

        try:
            payload = json.loads(self.private_profile_path.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError):
            payload = default_private_profile()

        payload.setdefault("updatedAt", now_iso())
        payload.setdefault("observationCount", 0)
        defaults = default_private_profile()
        payload.setdefault("opponentPatterns", defaults["opponentPatterns"])
        payload.setdefault("selfFindings", defaults["selfFindings"])
        if not isinstance(payload.get("opponentPatterns"), dict):
            payload["opponentPatterns"] = defaults["opponentPatterns"]
        if not isinstance(payload.get("selfFindings"), dict):
            payload["selfFindings"] = defaults["selfFindings"]
        for key, value in defaults["opponentPatterns"].items():
            payload["opponentPatterns"].setdefault(key, value)
        for key, value in defaults["selfFindings"].items():
            payload["selfFindings"].setdefault(key, value)
        self._save_private_profile(payload)
        return payload

    def _save_private_profile(self, payload: dict[str, Any]) -> None:
        payload["updatedAt"] = now_iso()
        _write_json_atomic(self.private_profile_path, payload)

    @staticmethod
    def _append_jsonl(path: Path, payload: dict[str, Any]) -> None:
        path.parent.mkdir(parents=True, exist_ok=True)
        with path.open("a", encoding="utf-8") as handle:
            handle.write(json.dumps(payload, ensure_ascii=True) + "\n")

    @staticmethod
    def _load_last_jsonl(path: Path) -> dict[str, Any] | None:
        if not path.exists():
            return None
        try:
            lines = [line for line in path.read_text(encoding="utf-8", errors="replace").splitlines() if line.strip()]
        except OSError:
            return None
        if not lines:
            return None
        try:
            payload = json.loads(lines[-1])
        except json.JSONDecodeError:
            return None
        return payload if isinstance(payload, dict) else None

    @staticmethod
    def _load_latest_event(path: Path, event_type: str) -> dict[str, Any] | None:
        if not path.exists():
            return None
        try:
            lines = [line for line in path.read_text(encoding="utf-8", errors="replace").splitlines() if line.strip()]
        except OSError:
            return None
        for line in reversed(lines):
            try:
                payload = json.loads(line)
            except json.JSONDecodeError:
                continue
            if isinstance(payload, dict) and payload.get("type") == event_type:
                return payload
        return None

    @staticmethod
    def _copy_counter(counter: dict[str, Any]) -> dict[str, int]:
        return {str(key): safe_int(value) for key, value in (counter or {}).items()}

    @staticmethod
    def _increment_counter(counter: dict[str, int], key: str, amount: int = 1) -> None:
        normalized = compact_line(key) or "unknown"
        counter[normalized] = safe_int(counter.get(normalized), 0) + amount

    @staticmethod
    def _diff_counter(current: dict[str, Any], baseline: dict[str, Any]) -> dict[str, int]:
        result: dict[str, int] = {}
        keys = set((current or {}).keys()) | set((baseline or {}).keys())
        for key in keys:
            result[str(key)] = max(0, safe_int((current or {}).get(key), 0) - safe_int((baseline or {}).get(key), 0))
        return result

    @staticmethod
    def _append_unique(values: list[str], value: str) -> None:
        normalized = compact_line(value)
        if normalized and normalized not in values:
            values.append(normalized)

    @staticmethod
    def _top_items(counter: dict[str, Any], limit: int) -> list[str]:
        pairs = sorted(
            ((str(key), safe_int(value, 0)) for key, value in (counter or {}).items() if safe_int(value, 0) > 0),
            key=lambda item: (-item[1], item[0]),
        )
        return [f"{key} ({value})" for key, value in pairs[:limit]]

    @staticmethod
    def _slot_wins(arena: dict[str, Any], slot_id: int) -> int:
        if slot_id == 1:
            return safe_int(arena.get("playerOneWins"), 0)
        if slot_id == 2:
            return safe_int(arena.get("playerTwoWins"), 0)
        return 0

    def _refresh_bot_profile(self) -> dict[str, Any]:
        self.bot_profile = self.manager.get_profile(self.bot_id)
        return self.bot_profile

    def update(self, agent_state: dict[str, Any] | None) -> None:
        if not isinstance(agent_state, dict) or not agent_state.get("ok"):
            return

        frame = safe_int(agent_state.get("frame"), -1)
        session_id = str(agent_state.get("sessionId", "") or "")
        previous = self.last_agent_state
        if previous is not None and frame == self.last_frame and session_id == str(previous.get("sessionId", "")):
            return

        if self.slot_id <= 0:
            self.slot_id = safe_int(agent_state.get("slotId"), 0)

        self._ensure_match_context(agent_state)
        self._observe(agent_state, previous)
        self._observe_match(agent_state)
        self._detect_death(agent_state, previous)
        self._detect_round_completion(agent_state)
        self._detect_series_completion(agent_state)

        self.last_agent_state = agent_state
        self.last_frame = frame

    def _ensure_match_context(self, current: dict[str, Any]) -> None:
        session_id = str(current.get("sessionId", "") or "")
        prompt = current.get("promptState") or {}
        arena = prompt.get("arena") or {}

        if self.current_match is not None and session_id != str(self.current_match.get("sessionId", "")):
            self.current_match = None
            self.current_round = None
            self._last_round_signature = None
            self._last_series_signature = None

        if self.current_match is not None or not session_id:
            return

        if safe_int(arena.get("pendingChampionSlot"), 0) > 0:
            return

        self.current_match = self._create_match_state(current)
        self.current_round = self._create_round_state(current, round_number=1)
        self._last_round_signature = None
        self._last_series_signature = None

    def _create_match_state(self, current: dict[str, Any]) -> dict[str, Any]:
        prompt = current.get("promptState") or {}
        arena = prompt.get("arena") or {}
        target = prompt.get("target") or {}
        bot = self._refresh_bot_profile()
        slot_id = safe_int(current.get("slotId"), self.slot_id or 2)
        opponent_slot_id = safe_int(target.get("slotId"), 1 if slot_id == 2 else 2)
        return {
            "seriesId": time.strftime("series-%Y%m%d-%H%M%S"),
            "sessionId": str(current.get("sessionId", "") or ""),
            "slotId": slot_id,
            "botId": self.bot_id,
            "botDisplayName": str(bot.get("displayName", self.bot_id)),
            "generation": safe_int(bot.get("generation"), 1),
            "opponentSlotId": opponent_slot_id,
            "opponentDisplayName": compact_line(str(target.get("displayName", "") or slot_label(opponent_slot_id))),
            "startedAt": now_iso(),
            "roundsToChampion": max(1, safe_int(arena.get("roundsToChampion"), 1)),
            "seedLabels": [],
            "sampleCount": 0,
            "visibleFrames": 0,
            "projectileThreatFrames": 0,
            "meleeThreatFrames": 0,
            "rangedThreatFrames": 0,
            "ultimateThreatFrames": 0,
            "corneredFrames": 0,
            "fallbackFrames": 0,
            "roundResetFrames": 0,
            "dangerousProjectileFrames": 0,
            "intentCounts": {},
            "sourceCounts": {},
            "eventCounts": {},
            "deathReviews": [],
            "rounds": [],
            "baselinePatterns": self._copy_counter(self.profile.get("opponentPatterns", {})),
            "baselineFindings": self._copy_counter(self.profile.get("selfFindings", {})),
        }

    def _create_round_state(self, current: dict[str, Any], round_number: int) -> dict[str, Any]:
        prompt = current.get("promptState") or {}
        arena = prompt.get("arena") or {}
        slot_id = self.current_match["slotId"] if self.current_match else safe_int(current.get("slotId"), self.slot_id or 2)
        opponent_slot_id = self.current_match["opponentSlotId"] if self.current_match else (1 if slot_id == 2 else 2)
        return {
            "roundNumber": round_number,
            "startedAt": now_iso(),
            "startFrame": safe_int(current.get("frame"), -1),
            "seedLabel": compact_line(str(arena.get("currentRespawnSeedLabel", "") or f"Seed {safe_int(arena.get('currentRespawnSeedIndex'), 0) + 1}")),
            "seedIndex": safe_int(arena.get("currentRespawnSeedIndex"), 0),
            "startBotWins": self._slot_wins(arena, slot_id),
            "startOpponentWins": self._slot_wins(arena, opponent_slot_id),
            "sampleCount": 0,
            "visibleFrames": 0,
            "projectileThreatFrames": 0,
            "meleeThreatFrames": 0,
            "rangedThreatFrames": 0,
            "ultimateThreatFrames": 0,
            "corneredFrames": 0,
            "fallbackFrames": 0,
            "intentCounts": {},
            "sourceCounts": {},
            "eventCounts": {},
            "deathReviews": [],
        }

    def _observe(self, current: dict[str, Any], previous: dict[str, Any] | None) -> None:
        prompt = current.get("promptState") or {}
        arena = prompt.get("arena") or {}
        target = prompt.get("target") or {}
        events = prompt.get("events") or []
        previous_target = ((previous or {}).get("promptState") or {}).get("target") or {}
        patterns = self.profile["opponentPatterns"]
        findings = self.profile["selfFindings"]

        self.profile["observationCount"] = safe_int(self.profile.get("observationCount"), 0) + 1
        horizontal_distance = abs(safe_float(arena.get("horizontalDistance"), 0.0))
        target_velocity_y = safe_float((target.get("velocity") or {}).get("y"), 0.0)

        if "projectile_threat_spiked" in events and 160.0 <= horizontal_distance <= 760.0:
            patterns["midRangeProjectileThreats"] = safe_int(patterns.get("midRangeProjectileThreats"), 0) + 1
            self._append_jsonl(
                self.events_log,
                {
                    "timestamp": now_iso(),
                    "type": "projectile_threat_spiked",
                    "frame": safe_int(current.get("frame"), -1),
                    "sessionId": str(current.get("sessionId", "") or ""),
                    "horizontalDistance": horizontal_distance,
                    "botId": self.bot_id,
                },
            )

        self._observe_bot_feedback(current, previous)

        if self._has_new_memory_marker("movement_stalled", current, previous):
            findings["movementStalls"] = safe_int(findings.get("movementStalls"), 0) + 1
            feedback = current.get("executorFeedback") or {}
            intent_mode = compact_line(str(feedback.get("intentMode", "") or ((current.get("lastIntent") or {}).get("mode", "")) or "unknown"))
            self._append_jsonl(
                self.events_log,
                {
                    "timestamp": now_iso(),
                    "type": "movement_stalled",
                    "frame": safe_int(current.get("frame"), -1),
                    "sessionId": str(current.get("sessionId", "") or ""),
                    "botId": self.bot_id,
                    "intentMode": intent_mode,
                },
            )

        if not bool(target.get("isGrounded", True)) and bool(previous_target.get("isGrounded", True)) and target_velocity_y > 0.1:
            patterns["jumpEscapes"] = safe_int(patterns.get("jumpEscapes"), 0) + 1
        if bool(target.get("isDashing")) and not bool(previous_target.get("isDashing")):
            patterns["dashEscapes"] = safe_int(patterns.get("dashEscapes"), 0) + 1
        if bool(target.get("isMeleeActive")) and not bool(previous_target.get("isMeleeActive")):
            patterns["meleePressureStarts"] = safe_int(patterns.get("meleePressureStarts"), 0) + 1
        if bool(target.get("isUltimateActive")) and not bool(previous_target.get("isUltimateActive")):
            patterns["ultimateActivations"] = safe_int(patterns.get("ultimateActivations"), 0) + 1
        if "target_became_vulnerable" in events:
            patterns["vulnerabilityWindows"] = safe_int(patterns.get("vulnerabilityWindows"), 0) + 1

        self._save_private_profile(self.profile)

    @staticmethod
    def _has_new_memory_marker(marker: str, current: dict[str, Any], previous: dict[str, Any] | None) -> bool:
        current_prompt = current.get("promptState") or {}
        previous_prompt = ((previous or {}).get("promptState") or {})
        current_markers = set(str(item) for item in (current_prompt.get("events") or []))
        current_markers.update(str(item) for item in (current_prompt.get("memory") or []))
        if marker not in current_markers:
            return False

        current_session = str(current.get("sessionId", "") or "")
        previous_session = str((previous or {}).get("sessionId", "") or "")
        if current_session != previous_session:
            return True

        previous_markers = set(str(item) for item in (previous_prompt.get("events") or []))
        previous_markers.update(str(item) for item in (previous_prompt.get("memory") or []))
        return marker not in previous_markers

    def _observe_bot_feedback(self, current: dict[str, Any], previous: dict[str, Any] | None) -> None:
        feedback = current.get("executorFeedback") or {}
        current_text = compact_line(str(feedback.get("botFeedback", "") or ""))[:320]
        if not current_text:
            return

        previous_feedback = ((previous or {}).get("executorFeedback") or {})
        previous_text = compact_line(str(previous_feedback.get("botFeedback", "") or ""))[:320]
        current_session = str(current.get("sessionId", "") or "")
        previous_session = str((previous or {}).get("sessionId", "") or "")
        if current_text == previous_text and current_session == previous_session:
            return

        payload = {
            "timestamp": now_iso(),
            "type": "bot_feedback",
            "frame": safe_int(current.get("frame"), -1),
            "sessionId": current_session,
            "botId": self.bot_id,
            "feedback": current_text,
            "source": compact_line(str(feedback.get("source", "") or "unknown")),
            "intentMode": compact_line(str(feedback.get("intentMode", "") or ((current.get("lastIntent") or {}).get("mode", "")) or "unknown")),
        }
        self.latest_bot_feedback = payload
        self._append_jsonl(self.events_log, payload)

    def _observe_match(self, current: dict[str, Any]) -> None:
        if self.current_match is None or self.current_round is None:
            return

        prompt = current.get("promptState") or {}
        arena = prompt.get("arena") or {}
        feedback = current.get("executorFeedback") or {}
        events = prompt.get("events") or []
        source = compact_line(str(feedback.get("source", "") or "unknown"))
        intent_mode = compact_line(str(feedback.get("intentMode", "") or ((current.get("lastIntent") or {}).get("mode", "")) or "unknown"))

        self.current_match["sampleCount"] += 1
        self.current_round["sampleCount"] += 1
        if bool(feedback.get("targetVisible")):
            self.current_match["visibleFrames"] += 1
            self.current_round["visibleFrames"] += 1
        if bool(feedback.get("projectileThreatActive")):
            self.current_match["projectileThreatFrames"] += 1
            self.current_round["projectileThreatFrames"] += 1
        if bool(feedback.get("targetMeleeThreatActive")):
            self.current_match["meleeThreatFrames"] += 1
            self.current_round["meleeThreatFrames"] += 1
        if bool(feedback.get("targetRangedThreatActive")):
            self.current_match["rangedThreatFrames"] += 1
            self.current_round["rangedThreatFrames"] += 1
        if bool(feedback.get("targetUltimateThreatActive")):
            self.current_match["ultimateThreatFrames"] += 1
            self.current_round["ultimateThreatFrames"] += 1
        if bool(feedback.get("selfCornered")):
            self.current_match["corneredFrames"] += 1
            self.current_round["corneredFrames"] += 1
        if bool(feedback.get("roundResetPending")):
            self.current_match["roundResetFrames"] += 1
        if prompt.get("dangerousProjectiles"):
            self.current_match["dangerousProjectileFrames"] += 1
        if not source.startswith("codex_"):
            self.current_match["fallbackFrames"] += 1
            self.current_round["fallbackFrames"] += 1

        self._increment_counter(self.current_match["intentCounts"], intent_mode)
        self._increment_counter(self.current_round["intentCounts"], intent_mode)
        self._increment_counter(self.current_match["sourceCounts"], source)
        self._increment_counter(self.current_round["sourceCounts"], source)

        for event in events:
            event_name = compact_line(str(event))
            if not event_name:
                continue
            self._increment_counter(self.current_match["eventCounts"], event_name)
            self._increment_counter(self.current_round["eventCounts"], event_name)

        self._append_unique(self.current_match["seedLabels"], str(arena.get("currentRespawnSeedLabel", "") or ""))

    def _detect_death(self, current: dict[str, Any], previous: dict[str, Any] | None) -> None:
        if previous is None:
            return

        current_prompt = current.get("promptState") or {}
        previous_prompt = previous.get("promptState") or {}
        current_self = current_prompt.get("self") or {}
        previous_self = previous_prompt.get("self") or {}
        if bool(previous_self.get("isDead")) or not bool(current_self.get("isDead")):
            return

        review = self._build_death_review(current)
        self.latest_death_review = review
        self._append_jsonl(self.death_reviews_log, review)

        if self.current_match is not None:
            self.current_match["deathReviews"].append(review)
        if self.current_round is not None:
            self.current_round["deathReviews"].append(review)

        findings = self.profile["selfFindings"]
        findings["deathsLogged"] = safe_int(findings.get("deathsLogged"), 0) + 1
        category = str(review.get("category", "") or "")
        if category == "projectile":
            findings["projectileDeaths"] = safe_int(findings.get("projectileDeaths"), 0) + 1
        elif category == "corner":
            findings["cornerDeaths"] = safe_int(findings.get("cornerDeaths"), 0) + 1
        elif category == "round_reset":
            findings["roundResetMistakes"] = safe_int(findings.get("roundResetMistakes"), 0) + 1
        elif category == "close_range":
            findings["closeRangeDeaths"] = safe_int(findings.get("closeRangeDeaths"), 0) + 1
        self._save_private_profile(self.profile)

    def _build_death_review(self, current: dict[str, Any]) -> dict[str, Any]:
        prompt = current.get("promptState") or {}
        feedback = current.get("executorFeedback") or {}
        arena = prompt.get("arena") or {}
        target = prompt.get("target") or {}
        bot_slot = safe_int(current.get("slotId"), self.slot_id or 2)
        opponent_slot = self.current_match["opponentSlotId"] if self.current_match is not None else (1 if bot_slot == 2 else 2)

        horizontal_distance = abs(safe_float(arena.get("horizontalDistance"), 0.0))
        round_reset = bool(arena.get("roundResetPending")) or bool(feedback.get("roundResetPending"))
        projectile_risk = bool(feedback.get("projectileThreatActive"))
        target_melee = bool(feedback.get("targetMeleeThreatActive")) or bool(target.get("isMeleeActive"))
        target_ranged = bool(feedback.get("targetRangedThreatActive"))
        target_ultimate = bool(feedback.get("targetUltimateThreatActive")) or bool(target.get("isUltimateActive"))
        self_cornered = bool(feedback.get("selfCornered")) or bool(arena.get("selfCornered"))
        target_visible = bool(feedback.get("targetVisible"))

        category = "generic"
        cause = "lost_neutral_exchange"
        better = "Replanejar mais cedo com memoria curta e bloquear repeticao de input sem ganho real."
        if round_reset:
            category, cause = "round_reset", "died_during_round_reset_context"
            better = "Bloquear punish e pressure durante round reset e resetar posicionamento antes de voltar a atacar."
        elif target_ranged:
            category, cause = "projectile", "ranged_startup_not_respected"
            better = "Dodge, quebrar linha ou interromper startup ranged antes de perseguir pickup ou trade."
        elif projectile_risk or target_ultimate:
            category, cause = "projectile", "projectile_threat_not_respected"
            better = "Subir o peso de antiProjectile e reagir antes ao startup do tiro ou ultimate."
        elif self_cornered:
            category, cause = "corner", "corner_trap"
            better = "Dar prioridade maior para escape de canto do que para plano ofensivo."
        elif target_melee and horizontal_distance <= 170.0:
            category, cause = "close_range", "close_range_melee_loss"
            better = "Trocar insistencia no contato por recuo, dash defensivo ou anti-air antes do golpe."
        elif not target_visible:
            cause = "committed_without_target_lock"
            better = "Exigir reconfirmacao visual do alvo antes de continuar o plano ofensivo."

        return {
            "timestamp": now_iso(),
            "sessionId": str(current.get("sessionId", "") or ""),
            "frame": safe_int(current.get("frame"), -1),
            "botId": self.bot_id,
            "botDisplayName": str(self.bot_profile.get("displayName", self.bot_id)),
            "roundNumber": self.current_round["roundNumber"] if self.current_round is not None else 0,
            "intentMode": str(feedback.get("intentMode", "") or ""),
            "intentReason": str(feedback.get("intentReason", "") or ""),
            "summary": compact_line(str(feedback.get("summary", "") or "")),
            "targetVisible": target_visible,
            "horizontalDistance": round(horizontal_distance, 2),
            "category": category,
            "likelyCause": cause,
            "betterResponse": better,
            "botWins": self._slot_wins(arena, bot_slot),
            "opponentWins": self._slot_wins(arena, opponent_slot),
            "seedLabel": compact_line(str(arena.get("currentRespawnSeedLabel", "") or "")),
        }

    def _detect_round_completion(self, current: dict[str, Any]) -> None:
        if self.current_match is None or self.current_round is None:
            return

        arena = (current.get("promptState") or {}).get("arena") or {}
        winner_slot = safe_int(arena.get("pendingRoundWinnerSlot"), 0)
        if winner_slot <= 0:
            return

        signature = (
            str(current.get("sessionId", "") or ""),
            winner_slot,
            safe_int(arena.get("playerOneWins"), 0),
            safe_int(arena.get("playerTwoWins"), 0),
        )
        if signature == self._last_round_signature:
            return

        self._last_round_signature = signature
        round_review = self._build_round_review(current, winner_slot)
        round_report_path = self._write_round_markdown(round_review)
        round_review["reportPath"] = str(round_report_path)
        self.latest_round_review = round_review
        self._append_jsonl(self.round_reviews_log, round_review)
        self.current_match["rounds"].append(round_review)
        self.current_round = self._create_round_state(current, round_number=len(self.current_match["rounds"]) + 1)

    def _build_round_review(self, current: dict[str, Any], winner_slot: int) -> dict[str, Any]:
        prompt = current.get("promptState") or {}
        arena = prompt.get("arena") or {}
        round_state = self.current_round or {}
        slot_id = self.current_match["slotId"]
        opponent_slot_id = self.current_match["opponentSlotId"]
        bot_wins = self._slot_wins(arena, slot_id)
        opponent_wins = self._slot_wins(arena, opponent_slot_id)
        result = "win" if winner_slot == slot_id else "loss"
        dominant_intent = self._top_items(round_state.get("intentCounts", {}), 1)
        dominant_source = self._top_items(round_state.get("sourceCounts", {}), 1)
        category_counts = self._count_review_categories(round_state.get("deathReviews", []))
        better_next_round = self._build_round_adjustments(round_state, category_counts, result)

        summary = (
            f"Round {round_state.get('roundNumber', len(self.current_match['rounds']) + 1)} "
            f"{'won' if result == 'win' else 'lost'} at {bot_wins}-{opponent_wins}. "
            f"Main intent was {(dominant_intent[0] if dominant_intent else 'unknown')}."
        )
        key_events = self._top_items(round_state.get("eventCounts", {}), 4)

        return {
            "timestamp": now_iso(),
            "botId": self.bot_id,
            "botDisplayName": str(self.bot_profile.get("displayName", self.bot_id)),
            "generation": safe_int(self.bot_profile.get("generation"), 1),
            "roundNumber": safe_int(round_state.get("roundNumber"), 0),
            "result": result,
            "winnerSlot": winner_slot,
            "botWins": bot_wins,
            "opponentWins": opponent_wins,
            "seedLabel": str(round_state.get("seedLabel", "") or "-"),
            "sampleCount": safe_int(round_state.get("sampleCount"), 0),
            "visibleFrames": safe_int(round_state.get("visibleFrames"), 0),
            "projectileThreatFrames": safe_int(round_state.get("projectileThreatFrames"), 0),
            "meleeThreatFrames": safe_int(round_state.get("meleeThreatFrames"), 0),
            "rangedThreatFrames": safe_int(round_state.get("rangedThreatFrames"), 0),
            "ultimateThreatFrames": safe_int(round_state.get("ultimateThreatFrames"), 0),
            "corneredFrames": safe_int(round_state.get("corneredFrames"), 0),
            "fallbackFrames": safe_int(round_state.get("fallbackFrames"), 0),
            "dominantIntent": dominant_intent[0] if dominant_intent else "-",
            "dominantSource": dominant_source[0] if dominant_source else "-",
            "keyEvents": key_events,
            "deathCategories": category_counts,
            "summary": summary,
            "betterNextRound": better_next_round,
        }

    def _build_round_adjustments(self, round_state: dict[str, Any], category_counts: dict[str, int], result: str) -> list[str]:
        adjustments: list[str] = []
        visible_frames = safe_int(round_state.get("visibleFrames"), 0)
        projectile_frames = safe_int(round_state.get("projectileThreatFrames"), 0)
        melee_frames = safe_int(round_state.get("meleeThreatFrames"), 0)
        ranged_frames = safe_int(round_state.get("rangedThreatFrames"), 0)
        ultimate_frames = safe_int(round_state.get("ultimateThreatFrames"), 0)
        cornered_frames = safe_int(round_state.get("corneredFrames"), 0)
        fallback_frames = safe_int(round_state.get("fallbackFrames"), 0)
        intent_counts = round_state.get("intentCounts", {}) or {}

        if safe_int(category_counts.get("projectile"), 0) > 0 or projectile_frames > max(4, visible_frames // 3):
            adjustments.append("Abrir o proximo round respeitando mais startup de projetil e ultimate.")
        if ranged_frames > max(4, visible_frames // 4):
            adjustments.append("Responder a startup ranged com dodge, line break ou interrupt antes de perseguir pickup.")
        if melee_frames > max(4, visible_frames // 4) or ultimate_frames > max(2, visible_frames // 6):
            adjustments.append("Recuar de melee/ultimate ativo antes de tentar trade ou punish.")
        if cornered_frames > max(4, visible_frames // 4):
            adjustments.append("Sair do canto mais cedo; corner pressure esta consumindo muitos frames do round.")
        if safe_int(category_counts.get("corner"), 0) > 0:
            adjustments.append("Nao aceitar ficar fixo no canto; escapar antes de retomar pressure.")
        if fallback_frames > max(4, safe_int(round_state.get("sampleCount"), 0) // 4):
            adjustments.append("Encurtar o plano e replanejar mais cedo para reduzir fallback ou waiting frames.")
        if visible_frames > 0 and safe_int(intent_counts.get("stabilize"), 0) > safe_int(intent_counts.get("pressure"), 0):
            adjustments.append("Parar de estabilizar com o alvo visivel; converter mais estados em pressure ou punish.")
        if result == "win" and not adjustments:
            adjustments.append("Repetir a mesma linha de pressao que fechou o round, mas mantendo anti-air pronto.")
        if not adjustments:
            adjustments.append("Abrir o proximo round com plano curto e confirmar vulnerabilidade antes de insistir.")
        return adjustments[:3]

    def _write_round_markdown(self, review: dict[str, Any]) -> Path:
        timestamp_slug = time.strftime("%Y%m%d-%H%M%S")
        result_slug = str(review.get("result", "round"))
        report_path = self.reports_dir / f"{timestamp_slug}-round-{safe_int(review.get('roundNumber'), 0)}-{result_slug}.md"
        lines = [
            "# Codex Round Review",
            "",
            "## Bot",
            f"- Bot: {review.get('botDisplayName', self.bot_id)}",
            f"- Bot ID: `{review.get('botId', self.bot_id)}`",
            f"- Generation: {safe_int(review.get('generation'), 1)}",
            "",
            "## Result",
            f"- Round: {safe_int(review.get('roundNumber'), 0)}",
            f"- Result: {review.get('result', '-')}",
            f"- Score after round: {safe_int(review.get('botWins'), 0)}-{safe_int(review.get('opponentWins'), 0)}",
            f"- Seed: {review.get('seedLabel', '-')}",
            "",
            "## Round Read",
            f"- Dominant intent: {review.get('dominantIntent', '-')}",
            f"- Dominant source: {review.get('dominantSource', '-')}",
            f"- Visible frames: {safe_int(review.get('visibleFrames'), 0)}",
            f"- Projectile threat frames: {safe_int(review.get('projectileThreatFrames'), 0)}",
            f"- Melee threat frames: {safe_int(review.get('meleeThreatFrames'), 0)}",
            f"- Ranged threat frames: {safe_int(review.get('rangedThreatFrames'), 0)}",
            f"- Ultimate threat frames: {safe_int(review.get('ultimateThreatFrames'), 0)}",
            f"- Cornered frames: {safe_int(review.get('corneredFrames'), 0)}",
            f"- Fallback frames: {safe_int(review.get('fallbackFrames'), 0)}",
            f"- Key events: {', '.join(review.get('keyEvents', [])) or '-'}",
            f"- Death categories: {json.dumps(review.get('deathCategories', {}), ensure_ascii=True, sort_keys=True)}",
            "",
            "## Summary",
            f"- {review.get('summary', '-')}",
            "",
            "## Better Next Round",
        ]
        for item in review.get("betterNextRound", []):
            lines.append(f"- {item}")
        markdown = "\n".join(lines) + "\n"
        _write_text_atomic(report_path, markdown)
        _write_text_atomic(self.latest_round_report_path, markdown)
        return report_path

    def _detect_series_completion(self, current: dict[str, Any]) -> None:
        if self.current_match is None:
            return

        arena = (current.get("promptState") or {}).get("arena") or {}
        champion_slot = safe_int(arena.get("pendingChampionSlot"), 0)
        if champion_slot <= 0:
            return

        signature = (
            str(current.get("sessionId", "") or ""),
            champion_slot,
            safe_int(arena.get("playerOneWins"), 0),
            safe_int(arena.get("playerTwoWins"), 0),
            len(self.current_match.get("rounds", [])),
        )
        if signature == self._last_series_signature:
            return

        self._last_series_signature = signature
        review = self._build_series_review(current, champion_slot)
        report_path = self._write_series_markdown(review)
        plan_path = self._write_plan_markdown(review)
        review["reportPath"] = str(report_path)
        review["planPath"] = str(plan_path)
        self.latest_series_review = review
        self.latest_series_plan = self._build_latest_series_plan_payload(review)
        self._append_jsonl(self.series_reviews_log, review)
        self._publish_global_knowledge(review)
        self.manager.evaluate_generation_promotion(self.bot_id, slot_id=safe_int(review.get("slotId"), self.slot_id or 0))

        self.current_match = None
        self.current_round = None
        self._last_round_signature = None

    def _build_series_review(self, current: dict[str, Any], champion_slot: int) -> dict[str, Any]:
        match = self.current_match or {}
        prompt = current.get("promptState") or {}
        arena = prompt.get("arena") or {}
        bot = self._refresh_bot_profile()
        slot_id = safe_int(match.get("slotId"), safe_int(current.get("slotId"), self.slot_id or 2))
        opponent_slot_id = safe_int(match.get("opponentSlotId"), 1 if slot_id == 2 else 2)
        bot_wins = self._slot_wins(arena, slot_id)
        opponent_wins = self._slot_wins(arena, opponent_slot_id)
        result = "won" if champion_slot == slot_id else "lost"
        patterns_delta = self._diff_counter(self.profile.get("opponentPatterns", {}), match.get("baselinePatterns", {}))
        findings_delta = self._diff_counter(self.profile.get("selfFindings", {}), match.get("baselineFindings", {}))
        death_categories = self._count_review_categories(match.get("deathReviews", []))
        opponent_habits = self._build_opponent_habits(patterns_delta)
        bot_could_improve = self._build_bot_improvements(match, patterns_delta, findings_delta, death_categories)
        gameplay_concerns = self._build_gameplay_concerns(match, patterns_delta, findings_delta, death_categories)
        next_series_plan = self._build_next_series_plan(match, patterns_delta, findings_delta, result)
        sample_count = safe_int(match.get("sampleCount"), 0)
        fallback_frames = safe_int(match.get("fallbackFrames"), 0)
        fallback_ratio = fallback_frames / max(1, sample_count)
        fatal_stalls = 1 if fallback_ratio >= 0.35 else 0

        summary = (
            f"{bot.get('displayName', self.bot_id)} {'venceu' if result == 'won' else 'perdeu'} a serie por "
            f"{bot_wins}-{opponent_wins}. "
            f"Oponente mostrou {', '.join(opponent_habits[:2]).lower() if opponent_habits else 'poucos padroes claros'}."
        )

        return {
            "timestamp": now_iso(),
            "seriesId": str(match.get("seriesId", "") or time.strftime("series-%Y%m%d-%H%M%S")),
            "sessionId": str(match.get("sessionId", "") or current.get("sessionId", "")),
            "slotId": slot_id,
            "botId": self.bot_id,
            "botDisplayName": str(bot.get("displayName", self.bot_id)),
            "generation": safe_int(bot.get("generation"), 1),
            "parentBotId": str(bot.get("parentBotId", "") or ""),
            "opponentSlotId": opponent_slot_id,
            "opponentDisplayName": str(match.get("opponentDisplayName", "") or slot_label(opponent_slot_id)),
            "roundsToChampion": max(1, safe_int(match.get("roundsToChampion"), 1)),
            "result": result,
            "championSlot": champion_slot,
            "botWins": bot_wins,
            "opponentWins": opponent_wins,
            "respawnSeedsUsed": list(match.get("seedLabels", [])),
            "sampleCount": sample_count,
            "visibleFrames": safe_int(match.get("visibleFrames"), 0),
            "projectileThreatFrames": safe_int(match.get("projectileThreatFrames"), 0),
            "meleeThreatFrames": safe_int(match.get("meleeThreatFrames"), 0),
            "rangedThreatFrames": safe_int(match.get("rangedThreatFrames"), 0),
            "ultimateThreatFrames": safe_int(match.get("ultimateThreatFrames"), 0),
            "corneredFrames": safe_int(match.get("corneredFrames"), 0),
            "fallbackFrames": fallback_frames,
            "fallbackFrameRatio": round(fallback_ratio, 4),
            "roundResetFrames": safe_int(match.get("roundResetFrames"), 0),
            "dangerousProjectileFrames": safe_int(match.get("dangerousProjectileFrames"), 0),
            "intentCounts": self._copy_counter(match.get("intentCounts", {})),
            "sourceCounts": self._copy_counter(match.get("sourceCounts", {})),
            "eventCounts": self._copy_counter(match.get("eventCounts", {})),
            "rounds": deepcopy(match.get("rounds", [])),
            "opponentHabits": opponent_habits,
            "botCouldImprove": bot_could_improve,
            "gameplayConcerns": gameplay_concerns,
            "nextSeriesPlan": next_series_plan,
            "summary": summary,
            "fatalStalls": fatal_stalls,
        }

    @staticmethod
    def _count_review_categories(reviews: list[dict[str, Any]]) -> dict[str, int]:
        categories: dict[str, int] = {}
        for review in reviews:
            key = compact_line(str(review.get("category", "") or "generic")) or "generic"
            categories[key] = safe_int(categories.get(key), 0) + 1
        return categories

    def _build_opponent_habits(self, patterns_delta: dict[str, int]) -> list[str]:
        habits: list[str] = []
        if safe_int(patterns_delta.get("midRangeProjectileThreats"), 0) > 0:
            habits.append("Pressiona mid range com projetil quando ha espaco.")
        if safe_int(patterns_delta.get("jumpEscapes"), 0) > 0:
            habits.append("Escapa da pressao com pulo em vez de trocar no chao.")
        if safe_int(patterns_delta.get("dashEscapes"), 0) > 0:
            habits.append("Usa dash defensivo para sair da pressao.")
        if safe_int(patterns_delta.get("meleePressureStarts"), 0) > 0:
            habits.append("Inicia pressao curta de melee quando entra no contato.")
        if safe_int(patterns_delta.get("ultimateActivations"), 0) > 0:
            habits.append("Ativa ultimate como janela de conversao ou check de distancia.")
        if not habits:
            habits.append("Ainda nao mostrou habitos muito consistentes nesta serie.")
        return habits[:4]

    def _build_bot_improvements(
        self,
        match: dict[str, Any],
        patterns_delta: dict[str, int],
        findings_delta: dict[str, int],
        death_categories: dict[str, int],
    ) -> list[str]:
        improvements: list[str] = []
        sample_count = safe_int(match.get("sampleCount"), 0)
        fallback_frames = safe_int(match.get("fallbackFrames"), 0)
        if fallback_frames > max(6, sample_count // 8):
            improvements.append("Reduzir fallback com intents mais curtos e replanning mais frequente.")
        if safe_int(findings_delta.get("projectileDeaths"), 0) > 0:
            improvements.append("Respeitar melhor startup de projetil e ultimate antes de insistir em pressure.")
        if safe_int(findings_delta.get("roundResetMistakes"), 0) > 0:
            improvements.append("Bloquear intents ofensivos durante contexto de round reset.")
        if safe_int(death_categories.get("corner"), 0) > 0:
            improvements.append("Dar prioridade maior a escape de corner antes de voltar ao plano ofensivo.")
        if safe_int(patterns_delta.get("jumpEscapes"), 0) > 0:
            improvements.append("Punir melhor fuga por pulo com anti-air ou dash catch.")
        if not improvements:
            improvements.append("Variar menos o plano sem motivo e converter mais cedo quando o alvo estiver vulneravel.")
        return improvements[:5]

    def _build_gameplay_concerns(
        self,
        match: dict[str, Any],
        patterns_delta: dict[str, int],
        findings_delta: dict[str, int],
        death_categories: dict[str, int],
    ) -> list[str]:
        concerns: list[str] = []
        sample_count = safe_int(match.get("sampleCount"), 0)
        fallback_frames = safe_int(match.get("fallbackFrames"), 0)
        if fallback_frames > max(8, sample_count // 5):
            concerns.append("O loop ainda depende demais de fallback heuristico; isso pode mascarar a qualidade real do bot.")
        if safe_int(findings_delta.get("roundResetMistakes"), 0) > 0:
            concerns.append("O contexto de round reset ainda pode estar pouco claro para a camada externa do bot.")
        if safe_int(patterns_delta.get("midRangeProjectileThreats"), 0) > 0 and safe_int(match.get("projectileThreatFrames"), 0) > sample_count // 4:
            concerns.append("O mid range esta pendendo forte para ameaca de projetil; vale revisar fluidez e contraplay desse espaco.")
        if safe_int(death_categories.get("close_range"), 0) > 0:
            concerns.append("As trocas de curta distancia ainda podem estar punitivas demais sem janela clara de resposta.")
        if safe_int(match.get("dangerousProjectileFrames"), 0) > sample_count // 3:
            concerns.append("O jogo gera muitas janelas de projetil perigoso; convem validar se isso esta deixando a luta menos fluida.")
        if not concerns:
            concerns.append("Nao apareceu uma falha sistemica gritante nesta serie; o ganho agora parece mais de tuning e leitura.")
        return concerns[:4]

    def _build_next_series_plan(
        self,
        match: dict[str, Any],
        patterns_delta: dict[str, int],
        findings_delta: dict[str, int],
        result: str,
    ) -> list[str]:
        plan: list[str] = []
        if safe_int(patterns_delta.get("jumpEscapes"), 0) > 0:
            plan.append("Abrir a proxima serie pressionando no chao e preparando anti-air cedo.")
        if safe_int(patterns_delta.get("dashEscapes"), 0) > 0:
            plan.append("Esperar o dash defensivo e punir a recuperacao em vez de perseguir linearmente.")
        if safe_int(findings_delta.get("projectileDeaths"), 0) > 0:
            plan.append("Subir prioridade de antiProjectile e recusar trocas ruins em mid range.")
        if safe_int(match.get("fallbackFrames"), 0) > max(4, safe_int(match.get("sampleCount"), 0) // 8):
            plan.append("Preferir planos mais curtos e replan frequente quando a execucao nao estiver produzindo ofensiva real.")
        if result == "won" and not plan:
            plan.append("Repetir a pressao de abertura que funcionou, mas manter anti-air e corner carry como prioridade.")
        if not plan:
            plan.append("Abrir a proxima serie em pressure, medir escape do oponente cedo e adaptar no segundo contato.")
        return plan[:5]

    def _write_series_markdown(self, review: dict[str, Any]) -> Path:
        timestamp_slug = time.strftime("%Y%m%d-%H%M%S")
        result_slug = "win" if str(review.get("result")) == "won" else "loss"
        report_path = self.reports_dir / f"{timestamp_slug}-{result_slug}-slot{safe_int(review.get('slotId'), 0)}.md"
        lines = [
            "# Codex Series Review",
            "",
            "## Bot",
            f"- Bot: {review.get('botDisplayName', self.bot_id)}",
            f"- Bot ID: `{review.get('botId', self.bot_id)}`",
            f"- Generation: {safe_int(review.get('generation'), 1)}",
            f"- Parent bot: {review.get('parentBotId', '-') or '-'}",
            "",
            "## Result",
            f"- Generated: {review.get('timestamp', '-')}",
            f"- Session: `{review.get('sessionId', '-')}`",
            f"- Series: `{review.get('seriesId', '-')}`",
            f"- Bot slot: {slot_label(safe_int(review.get('slotId'), 0))}",
            f"- Opponent slot: {slot_label(safe_int(review.get('opponentSlotId'), 0))}",
            f"- Opponent label: {review.get('opponentDisplayName', '-')}",
            f"- Final result: {review.get('result', '-')}",
            f"- Score: {safe_int(review.get('botWins'), 0)}-{safe_int(review.get('opponentWins'), 0)}",
            f"- First to: {safe_int(review.get('roundsToChampion'), 1)}",
            "",
            "## Series Parameters",
            f"- Respawn seeds used: {', '.join(review.get('respawnSeedsUsed', [])) or '-'}",
            f"- Samples observed: {safe_int(review.get('sampleCount'), 0)}",
            f"- Target visible frames: {safe_int(review.get('visibleFrames'), 0)}",
            f"- Projectile threat frames: {safe_int(review.get('projectileThreatFrames'), 0)}",
            f"- Melee threat frames: {safe_int(review.get('meleeThreatFrames'), 0)}",
            f"- Ranged threat frames: {safe_int(review.get('rangedThreatFrames'), 0)}",
            f"- Ultimate threat frames: {safe_int(review.get('ultimateThreatFrames'), 0)}",
            f"- Cornered frames: {safe_int(review.get('corneredFrames'), 0)}",
            f"- Fallback frames: {safe_int(review.get('fallbackFrames'), 0)}",
            f"- Fallback frame ratio: {safe_float(review.get('fallbackFrameRatio'), 0.0):.3f}",
            f"- Round reset frames: {safe_int(review.get('roundResetFrames'), 0)}",
            f"- Fatal stalls: {safe_int(review.get('fatalStalls'), 0)}",
            "",
            "## Round by Round",
        ]
        rounds = review.get("rounds", [])
        if rounds:
            for round_review in rounds:
                lines.append(
                    f"- Round {safe_int(round_review.get('roundNumber'), 0)}: "
                    f"{round_review.get('result', '-')} | score {safe_int(round_review.get('botWins'), 0)}-"
                    f"{safe_int(round_review.get('opponentWins'), 0)} | seed {round_review.get('seedLabel', '-')}"
                )
                lines.append(f"  Summary: {round_review.get('summary', '-')}")
                lines.append(f"  Better next round: {', '.join(round_review.get('betterNextRound', [])) or '-'}")
        else:
            lines.append("- No round summary recorded.")

        for title, items in (
            ("Opponent Habits", review.get("opponentHabits", [])),
            ("Bot Could Improve", review.get("botCouldImprove", [])),
            ("Gameplay Concerns", review.get("gameplayConcerns", [])),
            ("Next Series Plan", review.get("nextSeriesPlan", [])),
        ):
            lines.extend(["", f"## {title}"])
            for item in items:
                lines.append(f"- {item}")

        lines.extend(
            [
                "",
                "## Raw Counter Snapshot",
                f"- Intent counts: {json.dumps(review.get('intentCounts', {}), ensure_ascii=True, sort_keys=True)}",
                f"- Source counts: {json.dumps(review.get('sourceCounts', {}), ensure_ascii=True, sort_keys=True)}",
                f"- Event counts: {json.dumps(review.get('eventCounts', {}), ensure_ascii=True, sort_keys=True)}",
                "",
            ]
        )
        markdown = "\n".join(lines)
        _write_text_atomic(report_path, markdown)
        _write_text_atomic(self.latest_series_report_path, markdown)
        return report_path

    def _write_plan_markdown(self, review: dict[str, Any]) -> Path:
        timestamp_slug = time.strftime("%Y%m%d-%H%M%S")
        plan_path = self.reports_dir / f"{timestamp_slug}-next-plan-slot{safe_int(review.get('slotId'), 0)}.md"
        lines = [
            "# Codex Next Series Plan",
            "",
            f"- Bot: {review.get('botDisplayName', self.bot_id)}",
            f"- Bot ID: `{review.get('botId', self.bot_id)}`",
            f"- Based on series: `{review.get('seriesId', '-')}`",
            f"- Result reviewed: {review.get('result', '-')}",
            f"- Score reviewed: {safe_int(review.get('botWins'), 0)}-{safe_int(review.get('opponentWins'), 0)}",
            "",
            "## Immediate Plan",
        ]
        for item in review.get("nextSeriesPlan", []):
            lines.append(f"- {item}")
        lines.extend(["", "## Opponent Habits To Respect"])
        for item in review.get("opponentHabits", []):
            lines.append(f"- {item}")
        lines.extend(["", "## Gameplay Concerns To Watch"])
        for item in review.get("gameplayConcerns", []):
            lines.append(f"- {item}")
        markdown = "\n".join(lines) + "\n"
        _write_text_atomic(plan_path, markdown)
        _write_text_atomic(self.latest_series_plan_path, markdown)
        return plan_path

    def _publish_global_knowledge(self, review: dict[str, Any]) -> None:
        series_id = str(review.get("seriesId", "") or "")
        for concern in review.get("gameplayConcerns", []):
            self.global_knowledge.ingest(
                "gameplay",
                str(concern),
                f"{series_id}: {review.get('summary', '')}",
                self.bot_id,
                severity="medium",
            )
        for habit in review.get("opponentHabits", []):
            self.global_knowledge.ingest(
                "arena/system",
                str(habit),
                f"{series_id}: observed by {review.get('botDisplayName', self.bot_id)}",
                self.bot_id,
                severity="low",
            )

    def _build_latest_series_plan_payload(self, review: dict[str, Any] | None) -> dict[str, Any]:
        if not isinstance(review, dict):
            return {}
        return {
            "botId": self.bot_id,
            "steps": list(review.get("nextSeriesPlan", []))[:5],
            "planPath": str(review.get("planPath", self.latest_series_plan_path)),
            "seriesId": str(review.get("seriesId", "") or ""),
        }

    def profile_rows(self) -> list[tuple[str, str]]:
        bot = self._refresh_bot_profile()
        patterns = self.profile.get("opponentPatterns", {})
        findings = self.profile.get("selfFindings", {})
        return [
            ("Bot", str(bot.get("displayName", self.bot_id))),
            ("Bot ID", self.bot_id),
            ("Generation", str(safe_int(bot.get("generation"), 1))),
            ("Obs count", str(safe_int(self.profile.get("observationCount"), 0))),
            ("Proj pressure", str(safe_int(patterns.get("midRangeProjectileThreats"), 0))),
            ("Jump escapes", str(safe_int(patterns.get("jumpEscapes"), 0))),
            ("Dash escapes", str(safe_int(patterns.get("dashEscapes"), 0))),
            ("Deaths logged", str(safe_int(findings.get("deathsLogged"), 0))),
            ("Move stalls", str(safe_int(findings.get("movementStalls"), 0))),
        ]

    def latest_death_rows(self) -> list[tuple[str, str]]:
        review = self.latest_death_review
        if not review:
            return [("Latest death", "Nenhuma morte revisada ainda.")]
        return [
            ("Cause", str(review.get("likelyCause", "-") or "-")),
            ("Intent", str(review.get("intentMode", "-") or "-")),
            ("Why", str(review.get("intentReason", "-") or "-")),
            ("Context", str(review.get("summary", "-") or "-")),
            ("Better next", str(review.get("betterResponse", "-") or "-")),
        ]

    def latest_round_rows(self) -> list[tuple[str, str]]:
        review = self.latest_round_review
        if not review:
            return [("Latest round", "Nenhuma revisao de round ainda.")]
        return [
            ("Round", str(safe_int(review.get("roundNumber"), 0))),
            ("Result", str(review.get("result", "-") or "-")),
            ("Score", f"{safe_int(review.get('botWins'), 0)}-{safe_int(review.get('opponentWins'), 0)}"),
            ("Summary", str(review.get("summary", "-") or "-")),
            ("Report", str(review.get("reportPath", self.latest_round_report_path) or "-")),
        ]

    def latest_match_rows(self) -> list[tuple[str, str]]:
        review = self.latest_series_review
        if not review:
            return [("Latest series", "Nenhuma revisao de serie ainda.")]
        return [
            ("Result", str(review.get("result", "-") or "-")),
            ("Score", f"{safe_int(review.get('botWins'), 0)}-{safe_int(review.get('opponentWins'), 0)}"),
            ("Summary", str(review.get("summary", "-") or "-")),
            ("Report", str(review.get("reportPath", self.latest_series_report_path) or "-")),
        ]

    def latest_plan_rows(self) -> list[tuple[str, str]]:
        plan = self.latest_series_plan or {}
        if not plan:
            return [("Latest plan", "Nenhum plano de serie ainda.")]
        steps = list(plan.get("steps", []))
        if not steps:
            return [("Latest plan", "Sem plano registrado.")]
        rows = [(f"Plan {index + 1}", str(item)) for index, item in enumerate(steps[:4])]
        rows.append(("Plan file", str(plan.get("planPath", self.latest_series_plan_path))))
        return rows

    def smart_hints(self) -> list[str]:
        patterns = self.profile.get("opponentPatterns", {})
        findings = self.profile.get("selfFindings", {})
        hints: list[str] = []
        if safe_int(patterns.get("midRangeProjectileThreats"), 0) >= 3:
            hints.append("O oponente ja mostrou pressao frequente de projetil em mid range. Vale reforcar antiProjectile e punir startup de tiro.")
        if safe_int(patterns.get("jumpEscapes"), 0) >= 3:
            hints.append("O oponente esta escapando muito com pulo. Isso pede anti-air mais agressivo quando houver pressure.")
        if safe_int(patterns.get("dashEscapes"), 0) >= 3:
            hints.append("Ha padrao de dash defensivo. O bot pode esperar o dash e punir a recuperacao em vez de perseguir linearmente.")
        if safe_int(findings.get("roundResetMistakes"), 0) >= 1:
            hints.append("Ja houve erro em round reset. Convem bloquear intents ofensivos nesse estado antes do prompt.")
        if safe_int(findings.get("projectileDeaths"), 0) >= 2:
            hints.append("As mortes recentes mostram problema com projetil. Vale subir o peso de defesa a distancia e leitura de ETA.")
        if safe_int(findings.get("movementStalls"), 0) >= 1:
            hints.append("Ja houve stall de movimento. Convem variar rota, pular, dashear ou recuar em vez de segurar um eixo sem ganho.")

        latest_feedback = getattr(self, "latest_bot_feedback", None) or {}
        latest_feedback_text = compact_line(str(latest_feedback.get("feedback", "") or ""))
        if latest_feedback_text:
            hints.append(f"Feedback recente do bot: {latest_feedback_text}")

        round_review = self.latest_round_review or {}
        for item in round_review.get("betterNextRound", [])[:2]:
            hints.append(compact_line(str(item)))
        review = self.latest_series_review or {}
        for item in review.get("nextSeriesPlan", [])[:2]:
            hints.append(compact_line(str(item)))
        for item in self.global_knowledge.summary_points(limit=2):
            hints.append(compact_line(str(item)))

        deduped: list[str] = []
        for item in hints:
            normalized = compact_line(item)
            if normalized and normalized not in deduped:
                deduped.append(normalized)
        return deduped[:6]

    def prompt_payload(self) -> dict[str, Any]:
        bot = self._refresh_bot_profile()
        patterns = self.profile.get("opponentPatterns", {})
        findings = self.profile.get("selfFindings", {})
        death = self.latest_death_review or {}
        round_review = self.latest_round_review or {}
        series_review = self.latest_series_review or {}
        series_plan = self.latest_series_plan or {}
        focus_points: list[str] = []

        if safe_int(patterns.get("midRangeProjectileThreats"), 0) >= 2:
            focus_points.append("Opponent often threatens with mid-range projectiles.")
        if safe_int(patterns.get("jumpEscapes"), 0) >= 2:
            focus_points.append("Opponent frequently escapes pressure by jumping.")
        if safe_int(patterns.get("dashEscapes"), 0) >= 2:
            focus_points.append("Opponent frequently uses defensive dash escapes.")
        if safe_int(patterns.get("meleePressureStarts"), 0) >= 2:
            focus_points.append("Opponent initiates close-range pressure often.")
        if safe_int(findings.get("projectileDeaths"), 0) >= 2:
            focus_points.append("Recent deaths came from not respecting projectile threat.")
        if safe_int(findings.get("roundResetMistakes"), 0) >= 1:
            focus_points.append("A prior death happened during round-reset context; do not force offense there.")
        if safe_int(findings.get("movementStalls"), 0) >= 1:
            focus_points.append("Recent movement stalled; replan pathing instead of holding one axis.")

        latest_death = {}
        if death:
            latest_death = {
                "category": str(death.get("category", "") or ""),
                "likelyCause": str(death.get("likelyCause", "") or ""),
                "betterResponse": str(death.get("betterResponse", "") or ""),
            }

        latest_round_review = {}
        if round_review:
            latest_round_review = {
                "roundNumber": safe_int(round_review.get("roundNumber"), 0),
                "result": str(round_review.get("result", "") or ""),
                "summary": str(round_review.get("summary", "") or ""),
                "dominantIntent": str(round_review.get("dominantIntent", "") or ""),
                "betterNextRound": list(round_review.get("betterNextRound", []))[:3],
                "reportPath": str(round_review.get("reportPath", self.latest_round_report_path)),
            }

        latest_series_review = {}
        if series_review:
            latest_series_review = {
                "result": str(series_review.get("result", "") or ""),
                "summary": str(series_review.get("summary", "") or ""),
                "opponentHabits": list(series_review.get("opponentHabits", []))[:4],
                "botCouldImprove": list(series_review.get("botCouldImprove", []))[:4],
                "gameplayConcerns": list(series_review.get("gameplayConcerns", []))[:4],
                "nextSeriesPlan": list(series_review.get("nextSeriesPlan", []))[:5],
                "reportPath": str(series_review.get("reportPath", self.latest_series_report_path)),
                "planPath": str(series_review.get("planPath", self.latest_series_plan_path)),
            }

        latest_series_plan = {
            "steps": list(series_plan.get("steps", []))[:5],
            "planPath": str(series_plan.get("planPath", self.latest_series_plan_path)),
        } if series_plan else {}

        latest_bot_feedback: dict[str, Any] = {}
        feedback = getattr(self, "latest_bot_feedback", None) or {}
        feedback_text = compact_line(str(feedback.get("feedback", "") or ""))
        if feedback_text:
            latest_bot_feedback = dict(feedback)
            latest_bot_feedback["feedback"] = feedback_text
            latest_bot_feedback["frame"] = safe_int(feedback.get("frame"), -1)
            focus_points.append(f"Latest executor bot feedback: {feedback_text}")

        global_summary = self.global_knowledge.summary_points(limit=5)

        return {
            "botProfile": {
                "botId": self.bot_id,
                "displayName": str(bot.get("displayName", self.bot_id)),
                "provider": str(bot.get("provider", "openai_codex") or "openai_codex"),
                "model": str(bot.get("model", "") or ""),
                "reasoningEffort": str(bot.get("reasoningEffort", "") or ""),
                "ollamaHost": str(bot.get("ollamaHost", "") or ""),
                "personaSummary": str(bot.get("personaSummary", "") or ""),
                "playStyle": str(bot.get("playStyle", "") or ""),
                "combatPriorities": list(bot.get("combatPriorities", []))[:6],
                "skills": list(bot.get("skills", []))[:8],
                "notes": list(bot.get("notes", []))[:8],
                "characterPreferences": deepcopy(bot.get("characterPreferences", {})),
                "behaviorFlags": deepcopy(bot.get("behaviorFlags", {})),
                "generation": safe_int(bot.get("generation"), 1),
                "parentBotId": str(bot.get("parentBotId", "") or ""),
                "status": str(bot.get("status", "active") or "active"),
            },
            "focusPoints": focus_points[:4],
            "opponentPatterns": {
                "midRangeProjectileThreats": safe_int(patterns.get("midRangeProjectileThreats"), 0),
                "jumpEscapes": safe_int(patterns.get("jumpEscapes"), 0),
                "dashEscapes": safe_int(patterns.get("dashEscapes"), 0),
                "meleePressureStarts": safe_int(patterns.get("meleePressureStarts"), 0),
                "ultimateActivations": safe_int(patterns.get("ultimateActivations"), 0),
                "vulnerabilityWindows": safe_int(patterns.get("vulnerabilityWindows"), 0),
            },
            "selfFindings": {
                "deathsLogged": safe_int(findings.get("deathsLogged"), 0),
                "projectileDeaths": safe_int(findings.get("projectileDeaths"), 0),
                "cornerDeaths": safe_int(findings.get("cornerDeaths"), 0),
                "roundResetMistakes": safe_int(findings.get("roundResetMistakes"), 0),
                "closeRangeDeaths": safe_int(findings.get("closeRangeDeaths"), 0),
                "movementStalls": safe_int(findings.get("movementStalls"), 0),
            },
            "latestDeathReview": latest_death,
            "latestRoundReview": latest_round_review,
            "latestSeriesReview": latest_series_review,
            "latestSeriesPlan": latest_series_plan,
            "latestBotFeedback": latest_bot_feedback,
            "globalKnowledgeSummary": global_summary,
            "latestMatchReview": latest_series_review,
            "nextMatchPlan": list(series_plan.get("steps", []))[:5],
        }
