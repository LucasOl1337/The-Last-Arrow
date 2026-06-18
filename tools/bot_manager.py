import json
import time
from copy import deepcopy
from pathlib import Path
from typing import Any


TOOLS_DIR = Path(__file__).resolve().parent
BOT_MEMORY_DIR = TOOLS_DIR / "bot_memory"
GLOBAL_DIR = BOT_MEMORY_DIR / "global"
BOTS_DIR = BOT_MEMORY_DIR / "bots"
ROSTER_PATH = BOT_MEMORY_DIR / "roster.json"
RUNTIME_ASSIGNMENTS_PATH = BOT_MEMORY_DIR / "runtime_slot_assignments.json"
GENERATIONS_LOG = BOT_MEMORY_DIR / "generations.jsonl"
GLOBAL_KNOWLEDGE_LOG = GLOBAL_DIR / "knowledge_entries.jsonl"
GLOBAL_KNOWLEDGE_INDEX = GLOBAL_DIR / "knowledge_index.json"
GLOBAL_KNOWLEDGE_SUMMARY = GLOBAL_DIR / "latest_summary.md"
ATOMIC_REPLACE_RETRY_COUNT = 5
ATOMIC_REPLACE_RETRY_DELAY_SECONDS = 0.05


def now_iso() -> str:
    return time.strftime("%Y-%m-%dT%H:%M:%S")


def compact_line(value: str) -> str:
    return " ".join((value or "").strip().split())


def safe_int(value: Any, fallback: int = 0) -> int:
    try:
        return int(value)
    except (TypeError, ValueError):
        return fallback


def safe_float(value: Any, fallback: float = 0.0) -> float:
    try:
        return float(value)
    except (TypeError, ValueError):
        return fallback


def _append_jsonl(path: Path, payload: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("a", encoding="utf-8") as handle:
        handle.write(json.dumps(payload, ensure_ascii=True) + "\n")


def _write_text_atomic(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary_path = path.with_name(f"{path.name}.{time.time_ns()}.tmp")
    try:
        temporary_path.write_text(text, encoding="utf-8")
        for attempt in range(ATOMIC_REPLACE_RETRY_COUNT):
            try:
                temporary_path.replace(path)
                break
            except PermissionError:
                if attempt >= ATOMIC_REPLACE_RETRY_COUNT - 1:
                    raise
                time.sleep(ATOMIC_REPLACE_RETRY_DELAY_SECONDS * (attempt + 1))
            except FileNotFoundError:
                if attempt >= ATOMIC_REPLACE_RETRY_COUNT - 1:
                    raise
                temporary_path.write_text(text, encoding="utf-8")
                time.sleep(ATOMIC_REPLACE_RETRY_DELAY_SECONDS * (attempt + 1))
    finally:
        if temporary_path.exists():
            temporary_path.unlink()


def _write_json_atomic(path: Path, payload: Any) -> None:
    _write_text_atomic(path, json.dumps(payload, indent=2, ensure_ascii=True) + "\n")


def _load_json(path: Path, fallback: Any) -> Any:
    if not path.exists():
        return deepcopy(fallback)
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return deepcopy(fallback)


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


def _load_recent_jsonl(path: Path, limit: int) -> list[dict[str, Any]]:
    if not path.exists():
        return []
    try:
        lines = [line for line in path.read_text(encoding="utf-8", errors="replace").splitlines() if line.strip()]
    except OSError:
        return []
    payloads: list[dict[str, Any]] = []
    for line in lines[-limit:]:
        try:
            payload = json.loads(line)
        except json.JSONDecodeError:
            continue
        if isinstance(payload, dict):
            payloads.append(payload)
    return payloads


def default_bot_profile(bot_id: str, display_name: str | None = None, *, generation: int = 1, parent_bot_id: str = "") -> dict[str, Any]:
    label = compact_line(display_name or bot_id.replace("-", " ").title()) or bot_id
    return {
        "botId": bot_id,
        "displayName": label,
        "provider": "openai_codex",
        "model": "",
        "reasoningEffort": "",
        "codexHome": "",
        "ollamaHost": "http://127.0.0.1:11434",
        "openRouterApiKeyEnvVar": "OPENROUTER_API_KEY",
        "openRouterBaseUrl": "https://openrouter.ai/api/v1",
        "openRouterSiteUrl": "",
        "openRouterAppName": "The Last Arrow Bot Arena",
        "modelValidation": {
            "status": "unvalidated",
            "message": "Model not validated yet.",
            "verifiedAt": "",
            "provider": "openai_codex",
            "requestedModel": "",
            "selfReportedModel": "",
        },
        "personaSummary": f"{label} is a persistent combat bot for The Last Arrow.",
        "playStyle": "adaptive pressure with respect for projectile threat",
        "combatPriorities": [
            "stay in threatening range",
            "punish visible recovery",
            "respect projectile threat",
        ],
        "skills": [
            "spacing",
            "anti-air",
            "corner pressure",
            "projectile awareness",
        ],
        "notes": [
            "Keep plans short and revise quickly when pressure is not converting.",
        ],
        "characterPreferences": {},
        "behaviorFlags": {
            "preferShortPlans": True,
            "reuseRoundCoaching": True,
            "reuseSeriesPlanning": True,
            "shareGameplayConcernsGlobally": True,
        },
        "generation": max(1, generation),
        "parentBotId": parent_bot_id,
        "status": "active",
        "createdAt": now_iso(),
        "updatedAt": now_iso(),
    }


def normalize_bot_profile(profile: dict[str, Any]) -> tuple[dict[str, Any], bool]:
    payload = deepcopy(profile or {})
    changed = False

    provider = compact_line(str(payload.get("provider", "") or "openai_codex")).lower() or "openai_codex"
    if payload.get("provider") != provider:
        payload["provider"] = provider
        changed = True

    model = compact_line(str(payload.get("model", "") or ""))
    if provider == "openai_codex" and model.isdigit():
        payload["model"] = ""
        validation = payload.get("modelValidation", {}) if isinstance(payload.get("modelValidation"), dict) else {}
        validation.update({
            "status": "unvalidated",
            "message": "Legacy invalid model value was cleared. Select a model again.",
            "provider": provider,
            "requestedModel": "",
            "selfReportedModel": "",
            "verifiedAt": "",
        })
        payload["modelValidation"] = validation
        changed = True

    if provider == "ollama":
        ollama_host = str(payload.get("ollamaHost", "") or "").strip() or "http://127.0.0.1:11434"
        if payload.get("ollamaHost") != ollama_host:
            payload["ollamaHost"] = ollama_host
            changed = True
    if provider == "openrouter":
        defaults = {
            "openRouterApiKeyEnvVar": "OPENROUTER_API_KEY",
            "openRouterBaseUrl": "https://openrouter.ai/api/v1",
            "openRouterSiteUrl": "",
            "openRouterAppName": "The Last Arrow Bot Arena",
        }
        for key, fallback in defaults.items():
            value = str(payload.get(key, "") or fallback).strip()
            if payload.get(key) != value:
                payload[key] = value
                changed = True

    return payload, changed


def default_roster() -> dict[str, Any]:
    slot_one = default_bot_profile("bot-slot-1", "Bot Slot 1")
    slot_two = default_bot_profile("bot-slot-2", "Bot Slot 2")
    return {
        "version": 1,
        "updatedAt": now_iso(),
        "bots": {
            slot_one["botId"]: slot_one,
            slot_two["botId"]: slot_two,
        },
        "assignments": [
            {"slotId": 1, "botId": slot_one["botId"], "enabled": True},
            {"slotId": 2, "botId": slot_two["botId"], "enabled": True},
        ],
    }


class GlobalKnowledgeStore:
    def __init__(self) -> None:
        BOT_MEMORY_DIR.mkdir(parents=True, exist_ok=True)
        GLOBAL_DIR.mkdir(parents=True, exist_ok=True)
        self.index = _load_json(GLOBAL_KNOWLEDGE_INDEX, {"entries": {}})
        if not isinstance(self.index, dict):
            self.index = {"entries": {}}
        self.index.setdefault("entries", {})
        self._save_index()

    def _save_index(self) -> None:
        self.index["updatedAt"] = now_iso()
        _write_json_atomic(GLOBAL_KNOWLEDGE_INDEX, self.index)

    @staticmethod
    def _entry_key(category: str, summary: str) -> str:
        return f"{compact_line(category).lower()}::{compact_line(summary).lower()}"

    def ingest(self, category: str, summary: str, evidence: str, source_bot_id: str, *, severity: str = "medium") -> dict[str, Any] | None:
        normalized_summary = compact_line(summary)
        normalized_category = compact_line(category) or "general"
        if not normalized_summary:
            return None

        entry_key = self._entry_key(normalized_category, normalized_summary)
        entries = self.index.setdefault("entries", {})
        existing = deepcopy(entries.get(entry_key) or {})
        payload = {
            "entryId": str(existing.get("entryId") or f"knowledge-{int(time.time() * 1000)}"),
            "category": normalized_category,
            "summary": normalized_summary,
            "evidence": compact_line(evidence),
            "sourceBotId": compact_line(source_bot_id),
            "createdAt": str(existing.get("createdAt") or now_iso()),
            "updatedAt": now_iso(),
            "severity": compact_line(severity) or "medium",
            "count": safe_int(existing.get("count"), 0) + 1,
        }
        entries[entry_key] = payload
        self._save_index()
        _append_jsonl(GLOBAL_KNOWLEDGE_LOG, payload)
        self._write_summary_markdown()
        return payload

    def recent_entries(self, limit: int = 8) -> list[dict[str, Any]]:
        entries = list((self.index.get("entries") or {}).values())
        entries.sort(key=lambda item: (str(item.get("updatedAt", "")), str(item.get("createdAt", ""))), reverse=True)
        return [deepcopy(entry) for entry in entries[:limit]]

    def summary_points(self, limit: int = 5) -> list[str]:
        points: list[str] = []
        for entry in self.recent_entries(limit=limit * 2):
            severity = compact_line(str(entry.get("severity", "") or "medium"))
            category = compact_line(str(entry.get("category", "") or "general"))
            summary = compact_line(str(entry.get("summary", "") or ""))
            if not summary:
                continue
            points.append(f"[{severity}] {category}: {summary}")
            if len(points) >= limit:
                break
        return points

    def _write_summary_markdown(self) -> None:
        lines = [
            "# Bot Global Knowledge",
            "",
            "## Shared Gameplay / System Learnings",
        ]
        entries = self.recent_entries(limit=12)
        if not entries:
            lines.append("- Nenhum conhecimento global registrado ainda.")
        else:
            for entry in entries:
                lines.append(
                    f"- [{entry.get('severity', 'medium')}] {entry.get('category', 'general')}: "
                    f"{entry.get('summary', '-')}"
                )
                evidence = compact_line(str(entry.get("evidence", "") or ""))
                if evidence:
                    lines.append(f"  Evidence: {evidence}")
        _write_text_atomic(GLOBAL_KNOWLEDGE_SUMMARY, "\n".join(lines) + "\n")


class BotManager:
    def __init__(self) -> None:
        BOT_MEMORY_DIR.mkdir(parents=True, exist_ok=True)
        BOTS_DIR.mkdir(parents=True, exist_ok=True)
        GLOBAL_DIR.mkdir(parents=True, exist_ok=True)
        self.roster = self._load_roster()

    def _load_roster(self) -> dict[str, Any]:
        roster = _load_json(ROSTER_PATH, default_roster())
        if not isinstance(roster, dict):
            roster = default_roster()
        roster.setdefault("version", 1)
        roster.setdefault("bots", {})
        roster.setdefault("assignments", [])
        if not roster["bots"]:
            roster = default_roster()
        self._ensure_assignments(roster)
        self._save_roster(roster)
        return roster

    @staticmethod
    def _ensure_assignments(roster: dict[str, Any]) -> None:
        assignments = roster.setdefault("assignments", [])
        normalized_assignments: list[dict[str, Any]] = []
        by_slot: dict[int, dict[str, Any]] = {}
        for assignment in assignments:
            if not isinstance(assignment, dict):
                continue
            slot_id = safe_int(assignment.get("slotId"), 0)
            if slot_id <= 0:
                continue
            normalized_assignment = deepcopy(assignment)
            normalized_assignment["slotId"] = slot_id
            normalized_assignment["enabled"] = bool(normalized_assignment.get("enabled", True))
            normalized_assignment["botId"] = compact_line(str(normalized_assignment.get("botId", "") or ""))
            by_slot[slot_id] = normalized_assignment

        defaults = default_roster()["assignments"]
        for assignment in defaults:
            slot_id = int(assignment["slotId"])
            if slot_id not in by_slot:
                by_slot[slot_id] = deepcopy(assignment)

        for slot_id in sorted(by_slot):
            normalized_assignments.append(by_slot[slot_id])

        roster["assignments"] = normalized_assignments

    def _save_roster(self, roster: dict[str, Any] | None = None) -> None:
        payload = deepcopy(roster or self.roster)
        payload["updatedAt"] = now_iso()
        _write_json_atomic(ROSTER_PATH, payload)
        self._save_runtime_assignments(payload)

    def _save_runtime_assignments(self, roster: dict[str, Any]) -> None:
        bots = roster.get("bots") if isinstance(roster, dict) else {}
        assignments = roster.get("assignments") if isinstance(roster, dict) else []
        slots: list[dict[str, Any]] = []
        if not isinstance(assignments, list):
            assignments = []
        for assignment in assignments:
            if not isinstance(assignment, dict):
                continue
            slot_id = safe_int(assignment.get("slotId"), 0)
            bot_id = compact_line(str(assignment.get("botId", "") or ""))
            if slot_id <= 0:
                continue
            bot = bots.get(bot_id, {}) if isinstance(bots, dict) else {}
            if not isinstance(bot, dict):
                bot = {}
            defaults = default_bot_profile(bot_id or f"bot-slot-{slot_id}", f"Bot Slot {slot_id}")
            effective_model = compact_line(str(bot.get("model", "") or defaults.get("model", "") or ""))
            slots.append({
                "slotId": slot_id,
                "enabled": bool(assignment.get("enabled", True)),
                "botId": bot_id,
                "displayName": compact_line(str(bot.get("displayName", "") or defaults.get("displayName", ""))),
                "provider": compact_line(str(bot.get("provider", "") or defaults.get("provider", "openai_codex"))),
                "model": effective_model,
            })
        payload = {
            "updatedAt": now_iso(),
            "slots": slots,
        }
        _write_json_atomic(RUNTIME_ASSIGNMENTS_PATH, payload)

    def reload(self) -> None:
        self.roster = self._load_roster()

    def bot_dir(self, bot_id: str) -> Path:
        normalized = compact_line(bot_id) or "unassigned-bot"
        path = BOTS_DIR / normalized
        path.mkdir(parents=True, exist_ok=True)
        return path

    def ensure_bot(self, bot_id: str, *, display_name: str | None = None) -> dict[str, Any]:
        normalized = compact_line(bot_id) or "bot-auto"
        bot = deepcopy((self.roster.get("bots") or {}).get(normalized) or {})
        if not bot:
            bot = default_bot_profile(normalized, display_name)
            self.roster.setdefault("bots", {})[normalized] = bot
            self._save_roster()
        else:
            changed = False
            for key, value in default_bot_profile(normalized, display_name).items():
                if key not in bot:
                    bot[key] = value
                    changed = True
            bot, normalized_changed = normalize_bot_profile(bot)
            changed = changed or normalized_changed
            bot["updatedAt"] = now_iso()
            if changed:
                self.roster["bots"][normalized] = bot
                self._save_roster()
        self.bot_dir(normalized)
        return deepcopy(bot)

    def get_profile(self, bot_id: str) -> dict[str, Any]:
        return self.ensure_bot(bot_id)

    def update_profile(self, bot_id: str, patch: dict[str, Any]) -> dict[str, Any]:
        profile = self.ensure_bot(bot_id)
        profile.update(deepcopy(patch))
        profile["botId"] = bot_id
        profile, _ = normalize_bot_profile(profile)
        profile["updatedAt"] = now_iso()
        self.roster.setdefault("bots", {})[bot_id] = profile
        self._save_roster()
        return deepcopy(profile)

    def list_profiles(self) -> list[dict[str, Any]]:
        bots = list((self.roster.get("bots") or {}).values())
        bots.sort(key=lambda item: (safe_int(item.get("generation"), 1), str(item.get("displayName", ""))))
        return [deepcopy(bot) for bot in bots]

    def create_bot(self, bot_id: str, *, display_name: str = "") -> dict[str, Any]:
        return self.ensure_bot(bot_id, display_name=display_name or None)

    def get_assignment(self, slot_id: int) -> dict[str, Any]:
        normalized_slot = safe_int(slot_id, 0)
        for assignment in reversed(self.roster.get("assignments", [])):
            if safe_int(assignment.get("slotId"), 0) == normalized_slot:
                return deepcopy(assignment)
        fallback_bot_id = f"bot-slot-{normalized_slot}" if normalized_slot > 0 else "bot-auto"
        assignment = {
            "slotId": normalized_slot,
            "botId": fallback_bot_id,
            "enabled": normalized_slot > 0,
        }
        if normalized_slot > 0:
            self.roster.setdefault("assignments", []).append(deepcopy(assignment))
            self.ensure_bot(fallback_bot_id, display_name=f"Bot Slot {normalized_slot}")
            self._save_roster()
        return assignment

    def list_active_assignments(self) -> list[dict[str, Any]]:
        assignments: list[dict[str, Any]] = []
        for assignment in self.roster.get("assignments", []):
            if not bool(assignment.get("enabled", True)):
                continue
            slot_id = safe_int(assignment.get("slotId"), 0)
            bot_id = compact_line(str(assignment.get("botId", "") or ""))
            if slot_id <= 0 or not bot_id:
                continue
            profile = self.ensure_bot(bot_id)
            assignments.append({
                "slotId": slot_id,
                "botId": bot_id,
                "enabled": True,
                "displayName": str(profile.get("displayName", bot_id)),
            })
        assignments.sort(key=lambda item: item["slotId"])
        return assignments

    def assign_bot(self, slot_id: int, bot_id: str, *, enabled: bool = True) -> None:
        normalized_slot = safe_int(slot_id, 0)
        normalized_bot = compact_line(bot_id)
        if normalized_slot <= 0 or not normalized_bot:
            return
        self.ensure_bot(normalized_bot, display_name=f"Bot Slot {normalized_slot}")
        assignments = self.roster.setdefault("assignments", [])
        for assignment in assignments:
            if safe_int(assignment.get("slotId"), 0) == normalized_slot:
                assignment["botId"] = normalized_bot
                assignment["enabled"] = bool(enabled)
                self._save_roster()
                return
        assignments.append({"slotId": normalized_slot, "botId": normalized_bot, "enabled": bool(enabled)})
        self._save_roster()

    def set_slot_enabled(self, slot_id: int, enabled: bool) -> None:
        normalized_slot = safe_int(slot_id, 0)
        if normalized_slot <= 0:
            return
        assignments = self.roster.setdefault("assignments", [])
        for assignment in assignments:
            if safe_int(assignment.get("slotId"), 0) == normalized_slot:
                assignment["enabled"] = bool(enabled)
                self._save_roster()
                return
        fallback_bot_id = f"bot-slot-{normalized_slot}"
        assignments.append({"slotId": normalized_slot, "botId": fallback_bot_id, "enabled": bool(enabled)})
        self.ensure_bot(fallback_bot_id, display_name=f"Bot Slot {normalized_slot}")
        self._save_roster()

    def resolve_slot_bot(self, slot_id: int) -> dict[str, Any]:
        assignment = self.get_assignment(slot_id)
        bot_id = compact_line(str(assignment.get("botId", "") or ""))
        if not bot_id:
            bot_id = f"bot-slot-{safe_int(slot_id, 0)}"
            self.assign_bot(slot_id, bot_id, enabled=True)
        return self.get_profile(bot_id)

    def recent_series_reviews(self, bot_id: str, limit: int = 12) -> list[dict[str, Any]]:
        path = self.bot_dir(bot_id) / "series_reviews.jsonl"
        return _load_recent_jsonl(path, limit)

    @staticmethod
    def _promotion_gate(series_reviews: list[dict[str, Any]]) -> tuple[bool, dict[str, Any]]:
        if len(series_reviews) < 8:
            return False, {"reason": "not_enough_series", "seriesCount": len(series_reviews)}

        normalized = [review for review in series_reviews if isinstance(review, dict)]
        total = len(normalized)
        wins = sum(1 for review in normalized if str(review.get("result", "")).lower() in {"won", "win"})
        total_win_rate = wins / max(1, total)

        last_five = normalized[-5:]
        last_five_wins = sum(1 for review in last_five if str(review.get("result", "")).lower() in {"won", "win"})
        last_five_win_rate = last_five_wins / max(1, len(last_five))

        fallback_ratios = []
        fatal_stalls = 0
        for review in last_five:
            sample_count = max(1, safe_int(review.get("sampleCount"), 0))
            fallback_frames = safe_int(review.get("fallbackFrames"), 0)
            fallback_ratios.append(fallback_frames / sample_count)
            fatal_stalls += safe_int(review.get("fatalStalls"), 0)

        fallback_ratio = sum(fallback_ratios) / max(1, len(fallback_ratios))
        older = normalized[-10:-5]
        previous_win_rate = (
            sum(1 for review in older if str(review.get("result", "")).lower() in {"won", "win"}) / max(1, len(older))
            if older
            else last_five_win_rate
        )
        previous_fallback_ratio = (
            sum(
                safe_int(review.get("fallbackFrames"), 0) / max(1, safe_int(review.get("sampleCount"), 0))
                for review in older
            ) / max(1, len(older))
            if older
            else fallback_ratio
        )
        regressed = last_five_win_rate + 0.0001 < previous_win_rate or fallback_ratio - 0.0001 > previous_fallback_ratio

        metrics = {
            "seriesCount": total,
            "winRateTotal": round(total_win_rate, 4),
            "winRateLastFive": round(last_five_win_rate, 4),
            "fallbackFrameRatioLastFive": round(fallback_ratio, 4),
            "fatalStallsLastFive": fatal_stalls,
            "previousWindowWinRate": round(previous_win_rate, 4),
            "previousWindowFallbackRatio": round(previous_fallback_ratio, 4),
            "regressedVsPreviousWindow": regressed,
        }
        passes = (
            total_win_rate >= 0.55
            and last_five_win_rate >= 0.60
            and fallback_ratio <= 0.10
            and fatal_stalls == 0
            and not regressed
        )
        if passes:
            metrics["reason"] = "promotion_threshold_met"
        else:
            metrics["reason"] = "promotion_threshold_failed"
        return passes, metrics

    def evaluate_generation_promotion(self, bot_id: str, *, slot_id: int | None = None) -> dict[str, Any] | None:
        profile = self.get_profile(bot_id)
        if str(profile.get("status", "active")) != "active":
            return None

        reviews = self.recent_series_reviews(bot_id, limit=12)
        should_promote, metrics = self._promotion_gate(reviews)
        if not should_promote:
            return None

        child_generation = safe_int(profile.get("generation"), 1) + 1
        child_bot_id = f"{bot_id}-g{child_generation}"
        if child_bot_id in (self.roster.get("bots") or {}):
            return deepcopy((self.roster.get("bots") or {}).get(child_bot_id))

        child_profile = deepcopy(profile)
        child_profile["botId"] = child_bot_id
        child_profile["displayName"] = f"{profile.get('displayName', bot_id)} G{child_generation}"
        child_profile["generation"] = child_generation
        child_profile["parentBotId"] = bot_id
        child_profile["status"] = "active"
        child_profile["updatedAt"] = now_iso()
        child_profile["createdAt"] = now_iso()
        child_notes = list(child_profile.get("notes", []))
        child_notes.append(
            f"Promoted from {bot_id} after {metrics['seriesCount']} series with total win rate "
            f"{metrics['winRateTotal']:.2f} and last-five win rate {metrics['winRateLastFive']:.2f}."
        )
        child_profile["notes"] = child_notes[-12:]

        profile["status"] = "archived"
        profile["updatedAt"] = now_iso()
        self.roster["bots"][bot_id] = profile
        self.roster["bots"][child_bot_id] = child_profile
        self.bot_dir(child_bot_id)

        if slot_id is not None:
            self.assign_bot(slot_id, child_bot_id, enabled=True)
        else:
            for assignment in self.roster.get("assignments", []):
                if compact_line(str(assignment.get("botId", "") or "")) == bot_id:
                    assignment["botId"] = child_bot_id

        generation_record = {
            "botId": child_bot_id,
            "parentBotId": bot_id,
            "promotionReason": "promotion_threshold_met",
            "metricsSnapshot": metrics,
            "createdAt": now_iso(),
        }
        _append_jsonl(GENERATIONS_LOG, generation_record)
        self._save_roster()
        return deepcopy(child_profile)
