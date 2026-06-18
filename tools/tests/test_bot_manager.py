import json
import sys
import tempfile
import unittest
from unittest import mock
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

    def test_write_text_atomic_retries_transient_replace_permission_error(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            path = Path(temp_dir) / "roster.json"
            path.write_text("old", encoding="utf-8")
            original_replace = Path.replace
            attempts = {"count": 0}

            def flaky_replace(source: Path, target: Path) -> Path:
                attempts["count"] += 1
                if attempts["count"] < 3:
                    raise PermissionError("transient lock")
                return original_replace(source, target)

            with mock.patch.object(bot_manager.time, "sleep"), mock.patch.object(Path, "replace", flaky_replace):
                bot_manager._write_text_atomic(path, "new")

            self.assertEqual("new", path.read_text(encoding="utf-8"))
            self.assertEqual(3, attempts["count"])
            self.assertEqual([], list(path.parent.glob("roster.json.*.tmp")))

    def test_write_text_atomic_recreates_temp_after_transient_replace_file_not_found(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            path = Path(temp_dir) / "roster.json"
            path.write_text("old", encoding="utf-8")
            original_replace = Path.replace
            attempts = {"count": 0}

            def flaky_replace(source: Path, target: Path) -> Path:
                attempts["count"] += 1
                if attempts["count"] == 1:
                    source.unlink()
                    raise FileNotFoundError("transient missing temp")
                return original_replace(source, target)

            with mock.patch.object(bot_manager.time, "sleep"), mock.patch.object(Path, "replace", flaky_replace):
                bot_manager._write_text_atomic(path, "new")

            self.assertEqual("new", path.read_text(encoding="utf-8"))
            self.assertEqual(2, attempts["count"])
            self.assertEqual([], list(path.parent.glob("roster.json.*.tmp")))


if __name__ == "__main__":
    unittest.main()
