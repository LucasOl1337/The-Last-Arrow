import sys
import unittest
from pathlib import Path


TOOLS_DIR = Path(__file__).resolve().parents[1]
if str(TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(TOOLS_DIR))

import codex_report_console


class CodexReportConsoleStatusChipTestCase(unittest.TestCase):
    def test_status_chip_shows_waiting_for_broker_default_sessions(self) -> None:
        chip = codex_report_console.status_chip(
            {
                "controllerOwner": "BrokerDefault",
                "agentPhase": "waiting_for_agent",
            }
        )

        self.assertIn("WAITING", chip)

    def test_status_chip_keeps_heuristic_sessions_marked_as_fallback(self) -> None:
        chip = codex_report_console.status_chip(
            {
                "controllerOwner": "LocalHeuristic",
                "agentPhase": "idle",
            }
        )

        self.assertIn("FALLBACK", chip)

    def test_status_chip_shows_thinking_for_broker_default_sessions_in_thinking_phase(self) -> None:
        chip = codex_report_console.status_chip(
            {
                "controllerOwner": "BrokerDefault",
                "agentPhase": "thinking",
            }
        )

        self.assertIn("THINKING", chip)


class CodexReportConsoleDiagnoseTestCase(unittest.TestCase):
    def test_diagnose_treats_broker_default_thinking_as_normal_startup(self) -> None:
        hints = codex_report_console.diagnose(
            {
                "controllerOwner": "BrokerDefault",
                "controllerSource": "broker_default",
                "agentPhase": "thinking",
                "agentHeartbeatAgeMs": 0,
                "agentActionCount": 0,
                "hasAgentAction": False,
                "targetVisible": False,
                "projectileThreatActive": False,
                "summary": "",
                "agentNote": "",
                "lastInputSummary": "",
            },
            memory=_DummyMemory(),
        )

        self.assertTrue(any("estado base do broker" in hint for hint in hints))
        self.assertFalse(any("caiu em fallback" in hint for hint in hints))


class CodexReportConsoleViewTestCase(unittest.TestCase):
    def test_build_session_view_shows_bot_feedback(self) -> None:
        view = codex_report_console.build_session_view(
            {
                "slotId": 2,
                "botDisplayName": "Codex Two",
                "botId": "codex-two",
                "agentModel": "local-heuristic",
                "agentPhase": "idle",
                "intentMode": "stabilize",
                "agentActionCount": 3,
                "agentHeartbeatAgeMs": 20,
                "intentReason": "hold parry",
                "agentNote": "",
                "summary": "AI PARRY HOLD",
                "botFeedback": "projectile threat 0.12s; action AI PARRY HOLD; improve: defend before attacking.",
                "controllerOwner": "Codex",
                "controllerSource": "codex_agent",
                "targetVisible": True,
                "projectileThreatActive": True,
                "targetRangedThreatActive": True,
                "targetUltimateThreatActive": True,
                "selfCornered": True,
                "lastInputSummary": "axis=+0.00",
                "feedbackIntentReason": "hold parry",
                "agentThinking": False,
                "intentAgeMs": 40,
                "agentLastError": "",
            },
            memory=_DummyMemory(),
            width=100,
        )

        self.assertIn("Bot feedback", view)
        self.assertIn("projectile threat 0.12s", view)
        self.assertIn("Threats", view)
        self.assertIn("projectile, ranged, ultimate, cornered", view)


class _DummyMemory:
    bot_id = "codex-two"
    slot_id = 2

    def smart_hints(self) -> list[str]:
        return []

    def profile_rows(self) -> list[tuple[str, str]]:
        return []

    def latest_death_rows(self) -> list[tuple[str, str]]:
        return []

    def latest_round_rows(self) -> list[tuple[str, str]]:
        return []

    def latest_match_rows(self) -> list[tuple[str, str]]:
        return []

    def latest_plan_rows(self) -> list[tuple[str, str]]:
        return []


if __name__ == "__main__":
    unittest.main()
