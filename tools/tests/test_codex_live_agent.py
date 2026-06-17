import sys
import unittest
from pathlib import Path


TOOLS_DIR = Path(__file__).resolve().parents[1]
if str(TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(TOOLS_DIR))

import codex_live_agent


def _build_base_state() -> dict[str, object]:
    return {
        "promptState": {
            "self": {
                "slotId": 2,
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
                "slotId": 1,
                "isHitStunned": False,
                "isMeleeActive": False,
                "isUltimateActive": False,
                "isGrounded": True,
            },
            "arena": {
                "roundResetPending": False,
                "horizontalDistance": 320.0,
                "verticalDistance": 20.0,
                "targetInMeleeRange": False,
                "targetInUltimateRange": False,
                "targetInShootRange": True,
                "targetCornered": False,
                "selfCornered": False,
                "targetAbove": False,
            },
            "dangerousProjectiles": [],
            "events": [],
            "memory": [],
        },
        "executorFeedback": {
            "targetVisible": True,
            "projectileThreatActive": False,
            "roundResetPending": False,
        },
    }


class CodexLiveAgentHeuristicTestCase(unittest.TestCase):
    def test_resolve_runtime_provider_uses_heuristic_when_codex_is_missing(self) -> None:
        self.assertEqual("heuristic", codex_live_agent.resolve_runtime_provider("openai_codex", False))
        self.assertEqual("heuristic", codex_live_agent.resolve_runtime_provider("ollama", False))
        self.assertEqual("openrouter", codex_live_agent.resolve_runtime_provider("openrouter", False))
        self.assertEqual("heuristic", codex_live_agent.resolve_runtime_provider("heuristic", True))

    def test_resolve_agent_model_reports_local_heuristic(self) -> None:
        self.assertEqual("local-heuristic", codex_live_agent.resolve_agent_model("heuristic"))
        self.assertEqual(codex_live_agent.DISPLAY_MODEL, codex_live_agent.resolve_agent_model("openai_codex"))

    def test_build_tick_prompt_explicitly_uses_bot_feedback(self) -> None:
        state = _build_base_state()
        state["executorFeedback"]["botFeedback"] = "projectile threat 0.12s; action AI PARRY HOLD; improve: defend before attacking."
        payload = codex_live_agent.format_prompt_payload(state, codex_live_agent.MemoryTracker(slot_id=2))

        prompt = codex_live_agent.build_tick_prompt(payload)

        self.assertIn("executorFeedback.botFeedback", prompt)
        self.assertIn("projectile threat 0.12s", prompt)

    def test_build_heuristic_intent_punishes_vulnerable_target(self) -> None:
        state = _build_base_state()
        state["promptState"]["target"] = {
            "slotId": 1,
            "isHitStunned": True,
            "isMeleeActive": False,
            "isUltimateActive": False,
            "isGrounded": True,
        }
        state["promptState"]["arena"] = {
            "roundResetPending": False,
            "horizontalDistance": 128.0,
            "verticalDistance": 12.0,
            "targetInMeleeRange": True,
            "targetInUltimateRange": True,
            "targetInShootRange": True,
            "targetCornered": True,
            "selfCornered": False,
            "targetAbove": False,
        }
        state["promptState"]["events"] = ["target_became_vulnerable"]

        intent = codex_live_agent.build_heuristic_intent(state)

        self.assertEqual("punish", intent["mode"])
        self.assertEqual(1, intent["focusTargetSlot"])
        self.assertEqual("hold", intent["antiProjectile"])
        self.assertGreaterEqual(intent["meleeBias"], 0.9)
        self.assertGreaterEqual(intent["shootBias"], 0.6)
        self.assertIsNotNone(codex_live_agent.validate_intent(intent))

    def test_build_heuristic_intent_uses_projectile_evade(self) -> None:
        state = _build_base_state()
        state["promptState"]["arena"] = {
            "roundResetPending": False,
            "horizontalDistance": 260.0,
            "verticalDistance": 12.0,
            "targetInMeleeRange": False,
            "targetInUltimateRange": False,
            "targetInShootRange": True,
            "targetCornered": False,
            "selfCornered": False,
            "targetAbove": False,
        }
        state["promptState"]["dangerousProjectiles"] = [{"etaSeconds": 0.18}]
        state["executorFeedback"]["projectileThreatActive"] = True

        intent = codex_live_agent.build_heuristic_intent(state)

        self.assertEqual("retreat", intent["mode"])
        self.assertEqual("dash", intent["antiProjectile"])
        self.assertGreaterEqual(intent["dashBias"], 0.9)
        self.assertIsNotNone(codex_live_agent.validate_intent(intent))

    def test_heuristic_intent_preserves_movement_stall_escape_after_aggression_bias(self) -> None:
        state = _build_base_state()
        state["promptState"]["events"] = ["movement_stalled"]
        state["promptState"]["memory"] = ["movement_stalled"]
        state["executorFeedback"]["botFeedback"] = (
            "movement stalled; action: escape jump/dash; improve: replan path instead of holding one axis."
        )

        intent = codex_live_agent.apply_aggression_bias(
            codex_live_agent.build_heuristic_intent(state),
            state,
        )

        self.assertEqual("retreat", intent["mode"])
        self.assertEqual("heuristic_movement_stall_escape", intent["reason"])
        self.assertLessEqual(intent["advanceBias"], 0.18)
        self.assertGreaterEqual(intent["dashBias"], 0.9)
        self.assertGreaterEqual(intent["jumpBias"], 0.75)
        self.assertGreaterEqual(intent["cornerEscapeBias"], 0.9)
        self.assertIsNotNone(codex_live_agent.validate_intent(intent))

    def test_build_heuristic_intent_pressures_last_arrow_target(self) -> None:
        state = _build_base_state()
        state["promptState"]["self"]["arrows"] = 2
        state["promptState"]["target"]["arrows"] = 0
        state["promptState"]["arena"] = {
            "roundResetPending": False,
            "horizontalDistance": 420.0,
            "verticalDistance": 12.0,
            "targetInMeleeRange": False,
            "targetInUltimateRange": False,
            "targetInShootRange": True,
            "targetCornered": False,
            "selfCornered": False,
            "targetAbove": False,
        }

        intent = codex_live_agent.build_heuristic_intent(state)

        self.assertEqual("pressure", intent["mode"])
        self.assertEqual("heuristic_last_arrow_pressure", intent["reason"])
        self.assertGreaterEqual(intent["advanceBias"], 0.9)
        self.assertIsNotNone(codex_live_agent.validate_intent(intent))

    def test_apply_aggression_bias_promotes_visible_zone_opening_without_feedback(self) -> None:
        state = _build_base_state()
        state["executorFeedback"] = {}

        intent = {
            "mode": "zone",
            "preferredRange": 420,
            "advanceBias": 0.56,
            "shootBias": 0.84,
            "meleeBias": 0.24,
            "dashBias": 0.46,
            "jumpBias": 0.3,
            "antiProjectile": "hold",
            "antiAir": False,
            "punishRecovery": True,
            "cornerEscapeBias": 0.26,
            "focusTargetSlot": 1,
            "expiresInMs": 360,
            "reason": "heuristic_zone_spacing",
        }

        tuned = codex_live_agent.apply_aggression_bias(intent, state)

        self.assertEqual("pressure", tuned["mode"])
        self.assertGreaterEqual(tuned["advanceBias"], 0.84)
        self.assertGreaterEqual(tuned["dashBias"], 0.72)
        self.assertIsNotNone(codex_live_agent.validate_intent(tuned))

    def test_apply_aggression_bias_promotes_last_arrow_pressure(self) -> None:
        state = _build_base_state()
        state["promptState"]["self"]["arrows"] = 2
        state["promptState"]["target"]["arrows"] = 0
        state["promptState"]["arena"] = {
            "roundResetPending": False,
            "horizontalDistance": 420.0,
            "verticalDistance": 12.0,
            "targetInMeleeRange": False,
            "targetInUltimateRange": False,
            "targetInShootRange": True,
            "targetCornered": False,
            "selfCornered": False,
            "targetAbove": False,
        }

        intent = {
            "mode": "zone",
            "preferredRange": 420,
            "advanceBias": 0.56,
            "shootBias": 0.84,
            "meleeBias": 0.24,
            "dashBias": 0.46,
            "jumpBias": 0.3,
            "antiProjectile": "hold",
            "antiAir": False,
            "punishRecovery": True,
            "cornerEscapeBias": 0.26,
            "focusTargetSlot": 1,
            "expiresInMs": 360,
            "reason": "heuristic_zone_spacing",
        }

        tuned = codex_live_agent.apply_aggression_bias(intent, state)

        self.assertEqual("pressure", tuned["mode"])
        self.assertEqual("last_arrow_pressure", tuned["reason"])
        self.assertGreaterEqual(tuned["advanceBias"], 0.9)
        self.assertIsNotNone(codex_live_agent.validate_intent(tuned))


if __name__ == "__main__":
    unittest.main()
