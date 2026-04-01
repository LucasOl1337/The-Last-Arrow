import tempfile
import unittest
from pathlib import Path

import tools.bot_manager as bot_manager
from tools.bot_manager import BotManager
from tools.bot_menu import parse_csv_list, parse_json_object


class BotMenuHelpersTestCase(unittest.TestCase):
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

    def test_parse_helpers(self) -> None:
        self.assertEqual(["a", "b", "c"], parse_csv_list("a, b, c"))
        self.assertEqual({}, parse_json_object(""))
        self.assertEqual({"enabled": True}, parse_json_object('{"enabled": true}'))
        self.assertIsNone(parse_json_object("[]"))

    def test_create_and_toggle_slot(self) -> None:
        manager = BotManager()
        manager.create_bot("arena-alpha", display_name="Arena Alpha")
        manager.assign_bot(2, "arena-alpha", enabled=True)
        manager.set_slot_enabled(2, False)

        assignment = manager.get_assignment(2)
        profile = manager.get_profile("arena-alpha")

        self.assertEqual("arena-alpha", assignment["botId"])
        self.assertFalse(assignment["enabled"])
        self.assertEqual("Arena Alpha", profile["displayName"])
        self.assertEqual("openai_codex", profile["provider"])
        self.assertEqual("unvalidated", profile["modelValidation"]["status"])


if __name__ == "__main__":
    unittest.main()
