import sys
import unittest
from pathlib import Path


TOOLS_DIR = Path(__file__).resolve().parents[1]
if str(TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(TOOLS_DIR))

import codex_broker


class CodexBrokerRequestParsingTestCase(unittest.TestCase):
    def test_parse_content_length_rejects_invalid_and_oversized_values(self) -> None:
        original_limit = codex_broker.MAX_REQUEST_BODY_BYTES
        codex_broker.MAX_REQUEST_BODY_BYTES = 8

        try:
            self.assertEqual(0, codex_broker.parse_content_length(None))
            self.assertEqual(2, codex_broker.parse_content_length("2"))

            with self.assertRaisesRegex(ValueError, "invalid_content_length"):
                codex_broker.parse_content_length("-1")

            with self.assertRaisesRegex(ValueError, "invalid_content_length"):
                codex_broker.parse_content_length("not-a-number")

            with self.assertRaisesRegex(ValueError, "request_too_large"):
                codex_broker.parse_content_length("9")
        finally:
            codex_broker.MAX_REQUEST_BODY_BYTES = original_limit

    def test_decode_json_object_requires_valid_json_object(self) -> None:
        self.assertEqual({"ok": True}, codex_broker.decode_json_object(b'{"ok": true}'))

        with self.assertRaisesRegex(ValueError, "invalid_json"):
            codex_broker.decode_json_object(b'{"ok":')

        with self.assertRaisesRegex(ValueError, "invalid_payload"):
            codex_broker.decode_json_object(b"[]")


class BrokerSessionSnapshotTestCase(unittest.TestCase):
    def test_snapshot_marks_direct_codex_intent_as_executable(self) -> None:
        session = codex_broker.BrokerSession(
            2,
            "direct-session",
            {"mode": "pressure", "reason": "direct", "expiresInMs": 400},
        )

        snapshot = session.snapshot()

        self.assertTrue(snapshot["hasAgentAction"])
        self.assertEqual("CodexDirect", snapshot["controllerOwner"])
        self.assertEqual("pressure", snapshot["intent"]["mode"])


if __name__ == "__main__":
    unittest.main()
