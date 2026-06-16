import json
import sys
import tempfile
import unittest
from pathlib import Path


TOOLS_DIR = Path(__file__).resolve().parents[1]
if str(TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(TOOLS_DIR))

import bot_manager


class BotManagerAtomicWriteTestCase(unittest.TestCase):
    def test_write_json_atomic_creates_parent_and_round_trips_payload(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            path = Path(temp_dir) / "nested" / "roster.json"

            bot_manager._write_json_atomic(path, {"ok": True, "count": 2})

            self.assertTrue(path.exists())
            self.assertEqual({"ok": True, "count": 2}, json.loads(path.read_text(encoding="utf-8")))

    def test_write_text_atomic_replaces_existing_file_without_tmp_leftover(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            path = Path(temp_dir) / "latest_summary.md"
            path.write_text("old", encoding="utf-8")

            bot_manager._write_text_atomic(path, "new")

            self.assertEqual("new", path.read_text(encoding="utf-8"))
            self.assertEqual([], list(path.parent.glob("latest_summary.md.*.tmp")))


if __name__ == "__main__":
    unittest.main()
