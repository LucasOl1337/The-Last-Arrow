import sys
import tempfile
import unittest
from pathlib import Path


TOOLS_DIR = Path(__file__).resolve().parents[1]
if str(TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(TOOLS_DIR))

import codex_trace_store


class CodexTraceStoreTestCase(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.root = Path(self._tmp.name)
        self._originals = {
            "TRACE_DIR": codex_trace_store.TRACE_DIR,
            "TRACE_FILE": codex_trace_store.TRACE_FILE,
            "MAX_TRACE_FILE_BYTES": codex_trace_store.MAX_TRACE_FILE_BYTES,
            "MAX_TRACE_STRING_CHARS": codex_trace_store.MAX_TRACE_STRING_CHARS,
            "MAX_TRACE_LIST_ITEMS": codex_trace_store.MAX_TRACE_LIST_ITEMS,
        }
        codex_trace_store.TRACE_DIR = self.root / "traces"
        codex_trace_store.TRACE_FILE = codex_trace_store.TRACE_DIR / "trace_events.jsonl"

    def tearDown(self) -> None:
        for key, value in self._originals.items():
            setattr(codex_trace_store, key, value)
        self._tmp.cleanup()

    def test_append_and_read_trace_events(self) -> None:
        codex_trace_store.append_trace_event("state", {"slotId": 1, "frame": 10})
        codex_trace_store.append_trace_event("state", {"slotId": 2, "frame": 20})

        slot_one_events = codex_trace_store.read_trace_events(limit=10, slot_id=1)

        self.assertEqual(1, len(slot_one_events))
        self.assertEqual(10, slot_one_events[0]["payload"]["frame"])

    def test_append_rotates_trace_file_when_size_limit_is_reached(self) -> None:
        codex_trace_store.TRACE_DIR.mkdir(parents=True, exist_ok=True)
        codex_trace_store.TRACE_FILE.write_text("x" * 32, encoding="utf-8")
        codex_trace_store.MAX_TRACE_FILE_BYTES = 16

        codex_trace_store.append_trace_event("state", {"slotId": 1})

        archive_path = codex_trace_store.TRACE_FILE.with_suffix(".1.jsonl")
        self.assertTrue(archive_path.exists())
        self.assertEqual("x" * 32, archive_path.read_text(encoding="utf-8"))
        self.assertEqual(1, len(codex_trace_store.read_trace_events(limit=10)))

    def test_append_redacts_sensitive_values_and_truncates_large_strings(self) -> None:
        codex_trace_store.MAX_TRACE_STRING_CHARS = 24
        codex_trace_store.MAX_TRACE_LIST_ITEMS = 2

        codex_trace_store.append_trace_event(
            "codex_request",
            {
                "slotId": 1,
                "authorization": "Bearer secret-token-value",
                "prompt": "x" * 40,
                "nested": {
                    "apiKey": "sk-secret",
                    "stdout": "Bearer nested-secret",
                    "events": [1, 2, 3, 4],
                },
            },
        )

        event = codex_trace_store.read_trace_events(limit=10)[0]
        payload = event["payload"]

        self.assertEqual(1, payload["slotId"])
        self.assertEqual("[REDACTED]", payload["authorization"])
        self.assertEqual("[REDACTED]", payload["nested"]["apiKey"])
        self.assertNotIn("secret-token-value", codex_trace_store.TRACE_FILE.read_text(encoding="utf-8"))
        self.assertIn("[truncated", payload["prompt"])
        self.assertIn("Bearer [REDACTED]", payload["nested"]["stdout"])
        self.assertEqual([1, 2, "[truncated 2 items]"], payload["nested"]["events"])


if __name__ == "__main__":
    unittest.main()
