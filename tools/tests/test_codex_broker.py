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

    def test_build_tick_prompt_explicitly_uses_bot_feedback(self) -> None:
        prompt = codex_broker.build_tick_prompt(
            {"frame": 12},
            {
                "summary": "AI PARRY HOLD",
                "botFeedback": "projectile threat 0.12s; action AI PARRY HOLD; improve: defend before attacking.",
            },
            force_refresh=True,
        )

        self.assertIn("executorFeedback.botFeedback", prompt)
        self.assertIn("projectile threat 0.12s", prompt)


class AgentDrivenSessionReportTestCase(unittest.TestCase):
    def test_report_payload_defaults_to_broker_default_before_first_agent_action(self) -> None:
        session = codex_broker.AgentDrivenSession(
            2,
            "agent-session",
            {
                "frame": 1,
                "self": {"botId": "bot-slot-2"},
                "target": {"slotId": 1},
                "arena": {"horizontalDistance": 320.0},
            },
        )

        session.update_agent_status(
            {
                "sessionId": "agent-session",
                "model": "local-heuristic",
                "phase": "waiting_for_agent",
                "thinking": False,
            }
        )

        report = session.report_payload()

        self.assertEqual("broker_default", report["controllerSource"])
        self.assertEqual("BrokerDefault", report["controllerOwner"])
        self.assertFalse(report["hasAgentAction"])

    def test_report_payload_infers_local_heuristic_source_when_feedback_is_missing(self) -> None:
        session = codex_broker.AgentDrivenSession(
            2,
            "agent-session",
            {
                "frame": 1,
                "self": {"botId": "bot-slot-2"},
                "target": {"slotId": 1},
                "arena": {"horizontalDistance": 320.0},
            },
        )

        session.update_agent_status(
            {
                "sessionId": "agent-session",
                "model": "local-heuristic",
                "phase": "idle",
                "thinking": False,
            }
        )
        session.publish_action(
            {
                "mode": "pressure",
                "preferredRange": 320,
                "advanceBias": 0.72,
                "shootBias": 0.5,
                "meleeBias": 0.62,
                "dashBias": 0.6,
                "jumpBias": 0.24,
                "antiProjectile": "hold",
                "antiAir": True,
                "punishRecovery": True,
                "cornerEscapeBias": 0.28,
                "focusTargetSlot": 1,
                "expiresInMs": 360,
                "reason": "heuristic_zone_spacing",
            }
        )

        report = session.report_payload()

        self.assertEqual("heuristic_fallback", report["controllerSource"])
        self.assertEqual("LocalHeuristic", report["controllerOwner"])
        self.assertEqual("local-heuristic", report["agentModel"])
        self.assertTrue(report["hasAgentAction"])

    def test_report_payload_includes_bot_feedback_from_executor_feedback(self) -> None:
        session = codex_broker.AgentDrivenSession(
            2,
            "agent-session",
            {
                "frame": 1,
                "self": {"botId": "bot-slot-2"},
                "target": {"slotId": 1},
                "arena": {"horizontalDistance": 320.0},
            },
        )

        session.publish_state(
            {
                "frame": 2,
                "executorFeedback": {
                    "source": "codex",
                    "summary": "AI PARRY HOLD",
                    "botFeedback": "projectile threat 0.12s; action AI PARRY HOLD; improve: defend before attacking.",
                    "targetRangedThreatActive": True,
                    "targetUltimateThreatActive": True,
                    "selfCornered": True,
                },
            }
        )

        report = session.report_payload()

        self.assertEqual(
            "projectile threat 0.12s; action AI PARRY HOLD; improve: defend before attacking.",
            report["botFeedback"],
        )
        self.assertTrue(report["targetRangedThreatActive"])
        self.assertTrue(report["targetUltimateThreatActive"])
        self.assertTrue(report["selfCornered"])


if __name__ == "__main__":
    unittest.main()
