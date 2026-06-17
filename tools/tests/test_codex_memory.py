import json
import sys
import tempfile
import unittest
from pathlib import Path


TOOLS_DIR = Path(__file__).resolve().parents[1]
if str(TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(TOOLS_DIR))

import codex_memory


class MemoryTrackerPersistenceTestCase(unittest.TestCase):
    def test_save_private_profile_creates_parent_and_round_trips_payload(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            tracker = object.__new__(codex_memory.MemoryTracker)
            tracker.private_profile_path = Path(temp_dir) / "bots" / "bot-test" / "current_opponent_profile.json"
            payload = {
                "observationCount": 4,
                "opponentPatterns": {"jumpEscapes": 2},
                "selfFindings": {"deathsLogged": 1},
            }

            tracker._save_private_profile(payload)

            saved = json.loads(tracker.private_profile_path.read_text(encoding="utf-8"))
            self.assertEqual(4, saved["observationCount"])
            self.assertEqual(2, saved["opponentPatterns"]["jumpEscapes"])
            self.assertEqual(1, saved["selfFindings"]["deathsLogged"])
            self.assertTrue(saved["updatedAt"])
            self.assertEqual([], list(tracker.private_profile_path.parent.glob("current_opponent_profile.json.*.tmp")))

    def test_append_jsonl_creates_parent_and_appends_payload(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            path = Path(temp_dir) / "bots" / "bot-test" / "events.jsonl"

            codex_memory.MemoryTracker._append_jsonl(path, {"type": "projectile_threat_spiked", "frame": 12})

            self.assertEqual(
                [{"type": "projectile_threat_spiked", "frame": 12}],
                [json.loads(line) for line in path.read_text(encoding="utf-8").splitlines()],
            )

    def test_write_round_markdown_creates_report_and_latest_paths(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            tracker = object.__new__(codex_memory.MemoryTracker)
            tracker.bot_id = "bot-test"
            tracker.reports_dir = Path(temp_dir) / "bots" / "bot-test" / "match_reports"
            tracker.latest_round_report_path = Path(temp_dir) / "bots" / "bot-test" / "latest_round_review.md"
            review = {
                "botDisplayName": "Bot Test",
                "botId": "bot-test",
                "generation": 1,
                "roundNumber": 2,
                "result": "win",
                "botWins": 1,
                "opponentWins": 0,
                "seedLabel": "Seed 1",
                "dominantIntent": "pressure (3)",
                "dominantSource": "codex_strategy",
                "visibleFrames": 20,
                "projectileThreatFrames": 3,
                "fallbackFrames": 0,
                "keyEvents": ["target_became_vulnerable (1)"],
                "deathCategories": {},
                "summary": "Round 2 won at 1-0.",
                "betterNextRound": ["Keep pressure short."],
            }

            report_path = tracker._write_round_markdown(review)

            self.assertTrue(report_path.exists())
            self.assertTrue(tracker.latest_round_report_path.exists())
            self.assertEqual(
                report_path.read_text(encoding="utf-8"),
                tracker.latest_round_report_path.read_text(encoding="utf-8"),
            )
            self.assertEqual([], list(report_path.parent.glob("*.tmp")))

    def test_observe_match_counts_structured_combat_threat_frames(self) -> None:
        tracker = object.__new__(codex_memory.MemoryTracker)
        tracker.current_match = {
            "sampleCount": 0,
            "visibleFrames": 0,
            "projectileThreatFrames": 0,
            "meleeThreatFrames": 0,
            "rangedThreatFrames": 0,
            "ultimateThreatFrames": 0,
            "corneredFrames": 0,
            "roundResetFrames": 0,
            "dangerousProjectileFrames": 0,
            "fallbackFrames": 0,
            "intentCounts": {},
            "sourceCounts": {},
            "eventCounts": {},
            "seedLabels": [],
        }
        tracker.current_round = {
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
        }
        current = {
            "lastIntent": {"mode": "retreat"},
            "executorFeedback": {
                "source": "codex_live",
                "intentMode": "retreat",
                "targetVisible": True,
                "projectileThreatActive": True,
                "targetMeleeThreatActive": True,
                "targetRangedThreatActive": True,
                "targetUltimateThreatActive": True,
                "selfCornered": True,
            },
            "promptState": {
                "arena": {"currentRespawnSeedLabel": "Seed X"},
                "events": [],
                "dangerousProjectiles": [{"etaSeconds": 0.2}],
            },
        }

        tracker._observe_match(current)

        self.assertEqual(1, tracker.current_match["projectileThreatFrames"])
        self.assertEqual(1, tracker.current_match["meleeThreatFrames"])
        self.assertEqual(1, tracker.current_match["rangedThreatFrames"])
        self.assertEqual(1, tracker.current_match["ultimateThreatFrames"])
        self.assertEqual(1, tracker.current_match["corneredFrames"])
        self.assertEqual(1, tracker.current_round["rangedThreatFrames"])

    def test_build_death_review_uses_ranged_threat_feedback(self) -> None:
        tracker = object.__new__(codex_memory.MemoryTracker)
        tracker.bot_id = "bot-test"
        tracker.bot_profile = {"displayName": "Bot Test"}
        tracker.slot_id = 2
        tracker.current_match = {"opponentSlotId": 1}
        tracker.current_round = {"roundNumber": 1}
        current = {
            "sessionId": "session-ranged",
            "frame": 144,
            "slotId": 2,
            "executorFeedback": {
                "intentMode": "pressure",
                "intentReason": "forced trade",
                "summary": "AI COLLECT ARROW",
                "targetVisible": True,
                "targetRangedThreatActive": True,
            },
            "promptState": {
                "arena": {
                    "horizontalDistance": 260.0,
                    "playerOneWins": 0,
                    "playerTwoWins": 0,
                    "currentRespawnSeedLabel": "Seed R",
                },
                "target": {
                    "slotId": 1,
                    "isMeleeActive": False,
                    "isUltimateActive": False,
                },
            },
        }

        review = tracker._build_death_review(current)

        self.assertEqual("projectile", review["category"])
        self.assertEqual("ranged_startup_not_respected", review["likelyCause"])
        self.assertIn("interromper startup ranged", review["betterResponse"])

    def test_observe_records_changed_bot_feedback_once(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            tracker = object.__new__(codex_memory.MemoryTracker)
            tracker.bot_id = "bot-test"
            tracker.profile = codex_memory.default_private_profile()
            tracker.events_log = Path(temp_dir) / "bots" / "bot-test" / "events.jsonl"
            tracker.private_profile_path = Path(temp_dir) / "bots" / "bot-test" / "current_opponent_profile.json"
            current = {
                "frame": 42,
                "sessionId": "session-test",
                "executorFeedback": {
                    "botFeedback": "projectile threat 0.12s; action AI PARRY HOLD; improve: defend before attacking.",
                    "source": "codex_live",
                    "intentMode": "stabilize",
                },
                "promptState": {
                    "arena": {"horizontalDistance": 320.0},
                    "target": {"isGrounded": True, "velocity": {"y": 0.0}},
                    "events": [],
                },
            }
            next_frame = dict(current)
            next_frame["frame"] = 43

            tracker._observe(current, None)
            tracker._observe(next_frame, current)

            events = [json.loads(line) for line in tracker.events_log.read_text(encoding="utf-8").splitlines()]
            self.assertEqual(1, len(events))
            self.assertEqual("bot_feedback", events[0]["type"])
            self.assertEqual("session-test", events[0]["sessionId"])
            self.assertEqual(42, events[0]["frame"])
            self.assertIn("projectile threat 0.12s", events[0]["feedback"])
            self.assertEqual("codex_live", events[0]["source"])
            self.assertEqual("stabilize", events[0]["intentMode"])
            self.assertEqual(events[0], tracker.latest_bot_feedback)

    def test_observe_records_repeated_bot_feedback_when_session_changes(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            tracker = object.__new__(codex_memory.MemoryTracker)
            tracker.bot_id = "bot-test"
            tracker.profile = codex_memory.default_private_profile()
            tracker.events_log = Path(temp_dir) / "bots" / "bot-test" / "events.jsonl"
            tracker.private_profile_path = Path(temp_dir) / "bots" / "bot-test" / "current_opponent_profile.json"
            previous = {
                "frame": 120,
                "sessionId": "old-session",
                "executorFeedback": {
                    "botFeedback": "spacing stable; action AI PRESSURE; improve: vary approach timing.",
                },
                "promptState": {
                    "arena": {"horizontalDistance": 260.0},
                    "target": {"isGrounded": True, "velocity": {"y": 0.0}},
                    "events": [],
                },
            }
            current = dict(previous)
            current["frame"] = 1
            current["sessionId"] = "new-session"

            tracker._observe(current, previous)

            events = [json.loads(line) for line in tracker.events_log.read_text(encoding="utf-8").splitlines()]
            self.assertEqual(1, len(events))
            self.assertEqual("new-session", events[0]["sessionId"])

    def test_observe_records_movement_stall_memory_once(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            tracker = object.__new__(codex_memory.MemoryTracker)
            tracker.bot_id = "bot-test"
            tracker.profile = codex_memory.default_private_profile()
            tracker.events_log = Path(temp_dir) / "bots" / "bot-test" / "events.jsonl"
            tracker.private_profile_path = Path(temp_dir) / "bots" / "bot-test" / "current_opponent_profile.json"
            current = {
                "frame": 90,
                "sessionId": "session-stall",
                "executorFeedback": {
                    "source": "codex_live",
                    "intentMode": "pressure",
                },
                "promptState": {
                    "arena": {"horizontalDistance": 240.0},
                    "target": {"isGrounded": True, "velocity": {"y": 0.0}},
                    "events": [],
                    "memory": ["movement_stalled"],
                },
            }
            next_frame = dict(current)
            next_frame["frame"] = 91

            tracker._observe(current, None)
            tracker._observe(next_frame, current)

            self.assertEqual(1, tracker.profile["selfFindings"]["movementStalls"])
            events = [json.loads(line) for line in tracker.events_log.read_text(encoding="utf-8").splitlines()]
            self.assertEqual(1, len(events))
            self.assertEqual("movement_stalled", events[0]["type"])
            self.assertEqual("session-stall", events[0]["sessionId"])
            self.assertEqual(90, events[0]["frame"])
            self.assertEqual("pressure", events[0]["intentMode"])

    def test_smart_hints_and_prompt_payload_expose_latest_bot_feedback(self) -> None:
        tracker = object.__new__(codex_memory.MemoryTracker)
        tracker.bot_id = "bot-test"
        tracker.bot_profile = {"displayName": "Bot Test"}
        tracker.profile = codex_memory.default_private_profile()
        tracker.latest_bot_feedback = {
            "type": "bot_feedback",
            "feedback": "corner pressure; action AI DASH ESCAPE; improve: leave corner before attacking.",
            "frame": 77,
        }
        tracker.latest_death_review = None
        tracker.latest_round_review = None
        tracker.latest_series_review = None
        tracker.latest_series_plan = None
        tracker.latest_round_report_path = Path("latest_round_review.md")
        tracker.latest_series_report_path = Path("latest_series_review.md")
        tracker.latest_series_plan_path = Path("latest_series_plan.md")
        tracker.global_knowledge = type("FakeKnowledge", (), {"summary_points": lambda self, limit=5: []})()
        tracker._refresh_bot_profile = lambda: tracker.bot_profile

        hints = tracker.smart_hints()
        payload = tracker.prompt_payload()

        self.assertTrue(any("Feedback recente do bot" in item for item in hints))
        self.assertEqual(tracker.latest_bot_feedback, payload["latestBotFeedback"])
        self.assertIn("corner pressure", " ".join(payload["focusPoints"]))

    def test_smart_hints_and_prompt_payload_expose_movement_stalls(self) -> None:
        tracker = object.__new__(codex_memory.MemoryTracker)
        tracker.bot_id = "bot-test"
        tracker.bot_profile = {"displayName": "Bot Test"}
        tracker.profile = codex_memory.default_private_profile()
        tracker.profile["selfFindings"]["movementStalls"] = 2
        tracker.latest_bot_feedback = None
        tracker.latest_death_review = None
        tracker.latest_round_review = None
        tracker.latest_series_review = None
        tracker.latest_series_plan = None
        tracker.latest_round_report_path = Path("latest_round_review.md")
        tracker.latest_series_report_path = Path("latest_series_review.md")
        tracker.latest_series_plan_path = Path("latest_series_plan.md")
        tracker.global_knowledge = type("FakeKnowledge", (), {"summary_points": lambda self, limit=5: []})()
        tracker._refresh_bot_profile = lambda: tracker.bot_profile

        hints = tracker.smart_hints()
        payload = tracker.prompt_payload()

        self.assertTrue(any("stall de movimento" in item for item in hints))
        self.assertEqual(2, payload["selfFindings"]["movementStalls"])
        self.assertIn("movement stalled", " ".join(payload["focusPoints"]).lower())


if __name__ == "__main__":
    unittest.main()
