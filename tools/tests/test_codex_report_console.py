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


class _DummyMemory:
    def smart_hints(self) -> list[str]:
        return []


if __name__ == "__main__":
    unittest.main()
