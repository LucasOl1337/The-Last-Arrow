import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]


class CodexStackScriptTestCase(unittest.TestCase):
    def test_start_codex_stack_launches_broker_and_two_slot_agents(self) -> None:
        script = (ROOT / "tools" / "start_codex_stack.ps1").read_text(encoding="utf-8")

        self.assertIn("Start-Process -FilePath $python -ArgumentList 'tools/codex_broker.py'", script)
        self.assertIn("$env:CODEX_AGENT_SLOT_ID = '1'", script)
        self.assertIn("$env:CODEX_BOT_ID = 'slot-1-smoke'", script)
        self.assertIn("$env:CODEX_AGENT_SLOT_ID = '2'", script)
        self.assertIn("$env:CODEX_BOT_ID = 'slot-2-smoke'", script)
        self.assertIn("codex_live_agent_slot1.out.log", script)
        self.assertIn("codex_live_agent_slot2.out.log", script)
        self.assertIn("Where-Object { $_ -notlike '*hermes*' }", script)


if __name__ == "__main__":
    unittest.main()
