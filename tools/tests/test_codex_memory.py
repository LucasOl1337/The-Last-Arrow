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


if __name__ == "__main__":
    unittest.main()
