import json
import tempfile
import unittest
from pathlib import Path

import tools.bot_manager as bot_manager
from tools.bot_manager import BotManager, GlobalKnowledgeStore
from tools.codex_memory import MemoryTracker


class BotManagerTestCase(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.root = Path(self._tmp.name)
        self._originals = {
            "BOT_MEMORY_DIR": bot_manager.BOT_MEMORY_DIR,
            "GLOBAL_DIR": bot_manager.GLOBAL_DIR,
            "BOTS_DIR": bot_manager.BOTS_DIR,
            "ROSTER_PATH": bot_manager.ROSTER_PATH,
            "RUNTIME_ASSIGNMENTS_PATH": bot_manager.RUNTIME_ASSIGNMENTS_PATH,
            "GENERATIONS_LOG": bot_manager.GENERATIONS_LOG,
            "GLOBAL_KNOWLEDGE_LOG": bot_manager.GLOBAL_KNOWLEDGE_LOG,
            "GLOBAL_KNOWLEDGE_INDEX": bot_manager.GLOBAL_KNOWLEDGE_INDEX,
            "GLOBAL_KNOWLEDGE_SUMMARY": bot_manager.GLOBAL_KNOWLEDGE_SUMMARY,
        }
        bot_manager.BOT_MEMORY_DIR = self.root / "bot_memory"
        bot_manager.GLOBAL_DIR = bot_manager.BOT_MEMORY_DIR / "global"
        bot_manager.BOTS_DIR = bot_manager.BOT_MEMORY_DIR / "bots"
        bot_manager.ROSTER_PATH = bot_manager.BOT_MEMORY_DIR / "roster.json"
        bot_manager.RUNTIME_ASSIGNMENTS_PATH = bot_manager.BOT_MEMORY_DIR / "runtime_slot_assignments.json"
        bot_manager.GENERATIONS_LOG = bot_manager.BOT_MEMORY_DIR / "generations.jsonl"
        bot_manager.GLOBAL_KNOWLEDGE_LOG = bot_manager.GLOBAL_DIR / "knowledge_entries.jsonl"
        bot_manager.GLOBAL_KNOWLEDGE_INDEX = bot_manager.GLOBAL_DIR / "knowledge_index.json"
        bot_manager.GLOBAL_KNOWLEDGE_SUMMARY = bot_manager.GLOBAL_DIR / "latest_summary.md"

    def tearDown(self) -> None:
        for key, value in self._originals.items():
            setattr(bot_manager, key, value)
        self._tmp.cleanup()

    def test_roster_persists_assignments(self) -> None:
        manager = BotManager()
        manager.assign_bot(1, "alpha-bot")

        reloaded = BotManager()
        assignment = reloaded.get_assignment(1)
        profile = reloaded.get_profile("alpha-bot")

        self.assertEqual("alpha-bot", assignment["botId"])
        self.assertEqual("alpha-bot", profile["botId"])
        self.assertTrue((bot_manager.BOTS_DIR / "alpha-bot").exists())
        self.assertTrue(bot_manager.RUNTIME_ASSIGNMENTS_PATH.exists())

    def test_generation_promotion_creates_child_and_reassigns(self) -> None:
        manager = BotManager()
        manager.assign_bot(1, "alpha-bot")
        bot_dir = manager.bot_dir("alpha-bot")
        reviews_path = bot_dir / "series_reviews.jsonl"

        review = {
            "result": "won",
            "sampleCount": 100,
            "fallbackFrames": 5,
            "fatalStalls": 0,
        }
        reviews_path.write_text("\n".join(json.dumps(review) for _ in range(8)) + "\n", encoding="utf-8")

        promoted = manager.evaluate_generation_promotion("alpha-bot", slot_id=1)

        self.assertIsNotNone(promoted)
        self.assertEqual("alpha-bot", promoted["parentBotId"])
        self.assertEqual(2, promoted["generation"])
        self.assertEqual(promoted["botId"], manager.get_assignment(1)["botId"])
        self.assertEqual("archived", manager.get_profile("alpha-bot")["status"])

    def test_memory_is_private_per_bot(self) -> None:
        manager = BotManager()
        first = MemoryTracker(bot_id="alpha-bot", slot_id=1, manager=manager)
        second = MemoryTracker(bot_id="beta-bot", slot_id=2, manager=manager)

        first._append_jsonl(first.round_reviews_log, {"botId": "alpha-bot", "roundNumber": 1})
        second._append_jsonl(second.round_reviews_log, {"botId": "beta-bot", "roundNumber": 2})

        self.assertNotEqual(first.round_reviews_log, second.round_reviews_log)
        self.assertIn("alpha-bot", first.round_reviews_log.read_text(encoding="utf-8"))
        self.assertIn("beta-bot", second.round_reviews_log.read_text(encoding="utf-8"))
        self.assertEqual("alpha-bot", first.prompt_payload()["botProfile"]["botId"])
        self.assertEqual("beta-bot", second.prompt_payload()["botProfile"]["botId"])

    def test_global_knowledge_dedupes_equivalent_entries(self) -> None:
        store = GlobalKnowledgeStore()
        store.ingest("gameplay", "Projectile lane is too dominant.", "series-1", "alpha-bot", severity="high")
        store.ingest("gameplay", "Projectile lane is too dominant.", "series-2", "beta-bot", severity="high")

        entries = store.recent_entries(limit=5)

        self.assertEqual(1, len(entries))
        self.assertEqual(2, entries[0]["count"])

    def test_legacy_numeric_model_is_cleared(self) -> None:
        manager = BotManager()
        manager.update_profile("alpha-bot", {"model": "2", "provider": "openai_codex"})

        profile = manager.get_profile("alpha-bot")

        self.assertEqual("", profile["model"])
        self.assertEqual("unvalidated", profile["modelValidation"]["status"])
        self.assertIn("Legacy invalid model value was cleared", profile["modelValidation"]["message"])


if __name__ == "__main__":
    unittest.main()
