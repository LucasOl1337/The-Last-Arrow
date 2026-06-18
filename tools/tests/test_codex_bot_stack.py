import json
import os
import socket
import subprocess
import sys
import time
import unittest
from pathlib import Path
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen


ROOT = Path(__file__).resolve().parents[2]
PYTHON = sys.executable
CREATE_NO_WINDOW = getattr(subprocess, "CREATE_NO_WINDOW", 0)


def _start_process(command: list[str], env: dict[str, str]) -> subprocess.Popen[str]:
    return subprocess.Popen(
        command,
        cwd=str(ROOT),
        env=env,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
        creationflags=CREATE_NO_WINDOW,
    )


def _find_free_port() -> int:
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as sock:
        sock.bind(("127.0.0.1", 0))
        return int(sock.getsockname()[1])


def _wait_for_health(broker_url: str, timeout_seconds: float = 10.0) -> dict[str, object]:
    deadline = time.time() + timeout_seconds
    last_error = ""
    while time.time() < deadline:
        try:
            with urlopen(f"{broker_url}/health", timeout=1) as response:
                payload = json.loads(response.read().decode("utf-8"))
            if payload.get("ok"):
                return payload
        except (HTTPError, URLError, TimeoutError, OSError, json.JSONDecodeError) as exc:
            last_error = str(exc)
        time.sleep(0.25)

    raise AssertionError(f"broker did not become healthy: {last_error}")


def _post_json(broker_url: str, path: str, payload: dict[str, object]) -> dict[str, object]:
    raw = json.dumps(payload, ensure_ascii=True, separators=(",", ":")).encode("utf-8")
    request = Request(
        f"{broker_url}{path}",
        data=raw,
        headers={"Content-Type": "application/json"},
        method="POST",
    )
    with urlopen(request, timeout=5) as response:
        return json.loads(response.read().decode("utf-8"))


def _get_json(broker_url: str, path: str) -> dict[str, object]:
    with urlopen(f"{broker_url}{path}", timeout=5) as response:
        return json.loads(response.read().decode("utf-8"))


def _get_agent_session(report: dict[str, object], slot_id: int) -> dict[str, object] | None:
    sessions = report.get("agentSessions")
    if not isinstance(sessions, list):
        return None

    for item in sessions:
        if isinstance(item, dict) and int(item.get("slotId", 0) or 0) == slot_id:
            return item
    return None


def _wait_for_agent_action(broker_url: str, slot_id: int, timeout_seconds: float = 10.0) -> dict[str, object]:
    deadline = time.time() + timeout_seconds
    last_report: dict[str, object] | None = None
    while time.time() < deadline:
        time.sleep(0.5)
        last_report = _get_json(broker_url, "/report")["report"]
        session = _get_agent_session(last_report, slot_id)
        if not session:
            continue
        if int(session.get("agentActionCount", 0) or 0) >= 1:
            return session

    raise AssertionError(f"agent slot {slot_id} did not publish an action: {last_report}")


def _build_prompt_state(slot_id: int, bot_id: str, target_slot: int, horizontal_distance: float, *, target_in_melee_range: bool) -> dict[str, object]:
    return {
        "frame": 10 + slot_id,
        "botId": bot_id,
        "botDisplayName": f"Slot {slot_id} Smoke",
        "self": {
            "slotId": slot_id,
            "isGrounded": True,
            "isDead": False,
            "isDashing": False,
            "isMeleeActive": False,
            "isUltimateActive": False,
            "isHitStunned": False,
            "canParryProjectile": True,
            "arrows": 3,
            "shootCooldownLeft": 0.0,
            "meleeCooldownLeft": 0.0,
            "dashCooldownLeft": 0.0,
            "ultimateCooldownLeft": 0.0,
        },
        "target": {
            "slotId": target_slot,
            "isHitStunned": False,
            "isMeleeActive": False,
            "isUltimateActive": False,
            "isGrounded": True,
        },
        "arena": {
            "roundResetPending": False,
            "horizontalDistance": horizontal_distance,
            "verticalDistance": 18.0,
            "targetInMeleeRange": target_in_melee_range,
            "targetInUltimateRange": False,
            "targetInShootRange": True,
            "targetCornered": False,
            "selfCornered": False,
            "targetAbove": False,
        },
        "dangerousProjectiles": [],
        "events": [],
        "memory": [],
    }


class CodexBotStackSmokeTestCase(unittest.TestCase):
    def test_two_heuristic_agents_publish_actions_for_both_slots(self) -> None:
        broker_port = _find_free_port()
        broker_url = f"http://127.0.0.1:{broker_port}"
        env_base = os.environ.copy()
        env_base["CODEX_BROKER_PORT"] = str(broker_port)
        env_base["CODEX_BROKER_BASE"] = broker_url
        env_base["CODEX_MODEL_PROVIDER"] = "heuristic"
        env_base["PYTHONUNBUFFERED"] = "1"

        broker = _start_process([PYTHON, "tools/codex_broker.py"], env_base)
        agent1 = None
        agent2 = None

        try:
            _wait_for_health(broker_url)

            env_slot1 = env_base.copy()
            env_slot1["CODEX_AGENT_SLOT_ID"] = "1"
            env_slot1["CODEX_BOT_ID"] = "slot-1-smoke"

            env_slot2 = env_base.copy()
            env_slot2["CODEX_AGENT_SLOT_ID"] = "2"
            env_slot2["CODEX_BOT_ID"] = "slot-2-smoke"

            agent1 = _start_process([PYTHON, "tools/codex_live_agent.py"], env_slot1)

            try:
                time.sleep(2.0)

                _post_json(
                    broker_url,
                    "/agent/session/start",
                    {
                        "slotId": 1,
                        "promptState": _build_prompt_state(1, "slot-1-smoke", 2, 290.0, target_in_melee_range=False),
                    },
                )
                slot1 = _wait_for_agent_action(broker_url, 1)

                agent2 = _start_process([PYTHON, "tools/codex_live_agent.py"], env_slot2)
                time.sleep(2.0)

                _post_json(
                    broker_url,
                    "/agent/session/start",
                    {
                        "slotId": 2,
                        "promptState": _build_prompt_state(2, "slot-2-smoke", 1, 220.0, target_in_melee_range=True),
                    },
                )
                slot2 = _wait_for_agent_action(broker_url, 2)

                self.assertEqual("LocalHeuristic", slot1["controllerOwner"])
                self.assertEqual("heuristic_fallback", slot1["controllerSource"])
                self.assertFalse(slot1["targetVisible"])
                self.assertEqual("heuristic_waiting_for_target", slot1["intentReason"])
                self.assertEqual("LocalHeuristic", slot2["controllerOwner"])
                self.assertEqual("heuristic_fallback", slot2["controllerSource"])
                self.assertFalse(slot2["targetVisible"])
                self.assertEqual("heuristic_waiting_for_target", slot2["intentReason"])
            finally:
                if agent2 is not None:
                    agent2.terminate()
                    try:
                        agent2.wait(timeout=3)
                    except subprocess.TimeoutExpired:
                        agent2.kill()
                if agent1 is not None:
                    agent1.terminate()
                    try:
                        agent1.wait(timeout=3)
                    except subprocess.TimeoutExpired:
                        agent1.kill()
        finally:
            broker.terminate()
            try:
                broker.wait(timeout=3)
            except subprocess.TimeoutExpired:
                broker.kill()


if __name__ == "__main__":
    unittest.main()
