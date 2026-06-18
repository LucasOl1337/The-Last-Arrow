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

    def test_build_heuristic_intent_closes_on_vulnerable_out_of_range_feedback(self) -> None:
        state = _build_base_state()
        state["promptState"]["arena"] = {
            "roundResetPending": False,
            "horizontalDistance": 1180.0,
            "verticalDistance": 12.0,
            "targetInMeleeRange": False,
            "targetInUltimateRange": False,
            "targetInShootRange": False,
            "targetCornered": False,
            "selfCornered": False,
            "targetAbove": False,
        }
        state["executorFeedback"]["botFeedback"] = (
            "vulnerable target out of range at 1180u; action AI ADVANCE; "
            "improve: close distance before spending attacks."
        )

        intent = codex_live_agent.build_heuristic_intent(state)

        self.assertEqual("pressure", intent["mode"])
        self.assertEqual("heuristic_close_vulnerable_target", intent["reason"])
        self.assertLessEqual(intent["preferredRange"], 220)
        self.assertGreaterEqual(intent["advanceBias"], 0.9)
        self.assertGreaterEqual(intent["dashBias"], 0.8)
        self.assertLess(intent["shootBias"], 0.5)
        self.assertIsNotNone(codex_live_agent.validate_intent(intent))

    def test_build_heuristic_intent_closes_after_out_of_range_shot_feedback(self) -> None:
        state = _build_base_state()
        state["promptState"]["target"]["arrows"] = 2
        state["promptState"]["arena"] = {
            "roundResetPending": False,
            "horizontalDistance": 1180.0,
            "verticalDistance": 12.0,
            "targetInMeleeRange": False,
            "targetInUltimateRange": False,
            "targetInShootRange": False,
            "targetCornered": False,
            "selfCornered": False,
            "targetAbove": False,
        }
        state["executorFeedback"]["botFeedback"] = (
            "shot attempted out of range at 1180u; action AI ZONE SHOT; "
            "improve: close distance, aim a valid line, or hold fire."
        )

        intent = codex_live_agent.build_heuristic_intent(state)

        self.assertEqual("pressure", intent["mode"])
        self.assertEqual("heuristic_close_shot_range", intent["reason"])
        self.assertLessEqual(intent["preferredRange"], 300)
        self.assertGreaterEqual(intent["advanceBias"], 0.88)
        self.assertGreaterEqual(intent["dashBias"], 0.78)
        self.assertLess(intent["shootBias"], 0.5)
        self.assertIsNotNone(codex_live_agent.validate_intent(intent))

    def test_build_heuristic_intent_recovers_after_empty_shot_feedback(self) -> None:
        state = _build_base_state()
        state["promptState"]["self"]["arrows"] = 0
        state["promptState"]["target"]["arrows"] = 2
        state["promptState"]["recoverableProjectiles"] = [
            {"sourceSlotId": 2, "distanceToSelf": 96.0},
        ]
        state["executorFeedback"]["recoverableProjectileAvailable"] = True
        state["executorFeedback"]["recoverableProjectileCount"] = 1
        state["executorFeedback"]["nearestRecoverableProjectileDistance"] = 96.0
        state["executorFeedback"]["botFeedback"] = (
            "shot attempted without arrows; action AI SHOOT; improve: recover arrow before shooting."
        )

        intent = codex_live_agent.build_heuristic_intent(state)

        self.assertEqual("retreat", intent["mode"])
        self.assertEqual("heuristic_recover_arrow_after_empty_shot", intent["reason"])
        self.assertLessEqual(intent["shootBias"], 0.1)
        self.assertLessEqual(intent["meleeBias"], 0.28)
        self.assertGreaterEqual(intent["dashBias"], 0.78)
        self.assertGreaterEqual(intent["cornerEscapeBias"], 0.82)
        self.assertIsNotNone(codex_live_agent.validate_intent(intent))

    def test_build_heuristic_intent_recovers_after_missed_arrow_recovery_feedback(self) -> None:
        state = _build_base_state()
        state["promptState"]["self"]["arrows"] = 1
        state["promptState"]["target"]["arrows"] = 2
        state["promptState"]["recoverableProjectiles"] = [
            {"sourceSlotId": 2, "distanceToSelf": 96.0},
        ]
        state["executorFeedback"]["recoverableProjectileAvailable"] = True
        state["executorFeedback"]["recoverableProjectileCount"] = 1
        state["executorFeedback"]["nearestRecoverableProjectileDistance"] = 96.0
        state["executorFeedback"]["botFeedback"] = (
            "missed arrow recovery at 96u; action AI PRESSURE; improve: move toward pickup before forcing trades."
        )

        intent = codex_live_agent.build_heuristic_intent(state)

        self.assertEqual("retreat", intent["mode"])
        self.assertEqual("heuristic_recover_missed_arrow", intent["reason"])
        self.assertLessEqual(intent["shootBias"], 0.18)
        self.assertLessEqual(intent["meleeBias"], 0.32)
        self.assertGreaterEqual(intent["dashBias"], 0.78)
        self.assertGreaterEqual(intent["cornerEscapeBias"], 0.82)
        self.assertIsNotNone(codex_live_agent.validate_intent(intent))

    def test_build_heuristic_intent_prioritizes_recover_arrow_feedback(self) -> None:
        state = _build_base_state()
        state["promptState"]["self"]["arrows"] = 0
        state["promptState"]["target"]["arrows"] = 2
        state["promptState"]["recoverableProjectiles"] = [
            {"sourceSlotId": 2, "distanceToSelf": 128.0},
        ]
        state["executorFeedback"]["recoverableProjectileAvailable"] = True
        state["executorFeedback"]["recoverableProjectileCount"] = 1
        state["executorFeedback"]["nearestRecoverableProjectileDistance"] = 128.0
        state["executorFeedback"]["botFeedback"] = (
            "recover arrow at 128u; action AI RETREAT; improve: recover ammo before forcing trades."
        )

        intent = codex_live_agent.build_heuristic_intent(state)

        self.assertEqual("retreat", intent["mode"])
        self.assertEqual("heuristic_recover_arrow_feedback", intent["reason"])
        self.assertLessEqual(intent["shootBias"], 0.16)
        self.assertLessEqual(intent["meleeBias"], 0.3)
        self.assertGreaterEqual(intent["dashBias"], 0.82)
        self.assertGreaterEqual(intent["cornerEscapeBias"], 0.84)
        self.assertIsNotNone(codex_live_agent.validate_intent(intent))

    def test_build_heuristic_intent_rechallenges_after_missed_anti_air_feedback(self) -> None:
        state = _build_base_state()
        state["promptState"]["arena"] = {
            "roundResetPending": False,
            "horizontalDistance": 280.0,
            "verticalDistance": 180.0,
            "targetInMeleeRange": False,
            "targetInUltimateRange": False,
            "targetInShootRange": True,
            "targetCornered": False,
            "selfCornered": False,
            "targetAbove": True,
        }
        state["executorFeedback"]["botFeedback"] = (
            "missed anti-air; action AI DRIFT; improve: shoot, jump, or aim upward before the target lands."
        )

        intent = codex_live_agent.build_heuristic_intent(state)

        self.assertEqual("pressure", intent["mode"])
        self.assertEqual("heuristic_missed_anti_air", intent["reason"])
        self.assertTrue(intent["antiAir"])
        self.assertGreaterEqual(intent["shootBias"], 0.68)
        self.assertGreaterEqual(intent["jumpBias"], 0.7)
        self.assertGreaterEqual(intent["dashBias"], 0.68)
        self.assertIsNotNone(codex_live_agent.validate_intent(intent))

    def test_build_heuristic_intent_challenges_anti_air_opportunity_feedback(self) -> None:
        state = _build_base_state()
        state["promptState"]["arena"] = {
            "roundResetPending": False,
            "horizontalDistance": 280.0,
            "verticalDistance": 180.0,
            "targetInMeleeRange": False,
            "targetInUltimateRange": False,
            "targetInShootRange": True,
            "targetCornered": False,
            "selfCornered": False,
            "targetAbove": True,
        }
        state["executorFeedback"]["botFeedback"] = (
            "anti-air opportunity; action AI SHOOT; improve: challenge vertical approaches before landing."
        )

        intent = codex_live_agent.build_heuristic_intent(state)

        self.assertEqual("pressure", intent["mode"])
        self.assertEqual("heuristic_anti_air_opportunity", intent["reason"])
        self.assertTrue(intent["antiAir"])
        self.assertGreaterEqual(intent["shootBias"], 0.72)
        self.assertGreaterEqual(intent["jumpBias"], 0.5)
        self.assertGreaterEqual(intent["dashBias"], 0.64)
        self.assertIsNotNone(codex_live_agent.validate_intent(intent))

    def test_build_heuristic_intent_converts_missed_punish_window_feedback(self) -> None:
        state = _build_base_state()
        state["promptState"]["target"]["arrows"] = 0
        state["promptState"]["target"]["isHitStunned"] = False
        state["promptState"]["arena"] = {
            "roundResetPending": False,
            "horizontalDistance": 128.0,
            "verticalDistance": 8.0,
            "targetInMeleeRange": True,
            "targetInUltimateRange": True,
            "targetInShootRange": True,
            "targetCornered": False,
            "selfCornered": False,
            "targetAbove": False,
        }
        state["executorFeedback"]["botFeedback"] = (
            "missed punish window; action AI PRESSURE; improve: fire, melee, or ultimate before target recovers."
        )

        intent = codex_live_agent.build_heuristic_intent(state)

        self.assertEqual("punish", intent["mode"])
        self.assertEqual("heuristic_missed_punish_window", intent["reason"])
        self.assertLessEqual(intent["preferredRange"], 160)
        self.assertGreaterEqual(intent["meleeBias"], 0.9)
        self.assertGreaterEqual(intent["shootBias"], 0.66)
        self.assertGreaterEqual(intent["dashBias"], 0.8)
        self.assertIsNotNone(codex_live_agent.validate_intent(intent))

    def test_build_heuristic_intent_converts_punish_window_available_feedback(self) -> None:
        state = _build_base_state()
        state["promptState"]["target"]["isHitStunned"] = False
        state["promptState"]["arena"] = {
            "roundResetPending": False,
            "horizontalDistance": 148.0,
            "verticalDistance": 8.0,
            "targetInMeleeRange": True,
            "targetInUltimateRange": True,
            "targetInShootRange": True,
            "targetCornered": False,
            "selfCornered": False,
            "targetAbove": False,
        }
        state["executorFeedback"]["botFeedback"] = (
            "punish window available; action AI MELEE; improve: convert vulnerability quickly."
        )

        intent = codex_live_agent.build_heuristic_intent(state)

        self.assertEqual("punish", intent["mode"])
        self.assertEqual("heuristic_punish_window_available", intent["reason"])
        self.assertLessEqual(intent["preferredRange"], 160)
        self.assertGreaterEqual(intent["meleeBias"], 0.9)
        self.assertGreaterEqual(intent["shootBias"], 0.66)
        self.assertGreaterEqual(intent["dashBias"], 0.8)
        self.assertIsNotNone(codex_live_agent.validate_intent(intent))

    def test_build_heuristic_intent_escapes_after_missed_corner_feedback(self) -> None:
        state = _build_base_state()
        state["promptState"]["self"]["arrows"] = 2
        state["promptState"]["target"]["arrows"] = 0
        state["promptState"]["arena"] = {
            "roundResetPending": False,
            "horizontalDistance": 340.0,
            "verticalDistance": 12.0,
            "targetInMeleeRange": False,
            "targetInUltimateRange": False,
            "targetInShootRange": True,
            "targetCornered": False,
            "selfCornered": True,
            "targetAbove": False,
        }
        state["executorFeedback"]["botFeedback"] = (
            "missed corner escape; action AI PRESSURE; improve: move toward center before committing."
        )

        intent = codex_live_agent.build_heuristic_intent(state)

        self.assertEqual("retreat", intent["mode"])
        self.assertEqual("heuristic_missed_corner_escape", intent["reason"])
        self.assertGreaterEqual(intent["cornerEscapeBias"], 0.9)
        self.assertGreaterEqual(intent["dashBias"], 0.86)
        self.assertGreaterEqual(intent["jumpBias"], 0.55)
        self.assertLessEqual(intent["advanceBias"], 0.18)
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

    def test_build_heuristic_intent_defends_after_projectile_threat_text_feedback(self) -> None:
        state = _build_base_state()
        state["promptState"]["dangerousProjectiles"] = []
        state["executorFeedback"]["projectileThreatActive"] = False
        state["executorFeedback"]["botFeedback"] = (
            "projectile threat 0.12s; action AI PARRY HOLD; improve: defend before attacking."
        )

        intent = codex_live_agent.build_heuristic_intent(state)

        self.assertEqual("retreat", intent["mode"])
        self.assertEqual("heuristic_projectile_threat_feedback", intent["reason"])
        self.assertEqual("dash", intent["antiProjectile"])
        self.assertLessEqual(intent["advanceBias"], 0.18)
        self.assertLessEqual(intent["meleeBias"], 0.24)
        self.assertGreaterEqual(intent["dashBias"], 0.9)
        self.assertIsNotNone(codex_live_agent.validate_intent(intent))

    def test_build_heuristic_intent_uses_ranged_threat_feedback(self) -> None:
        state = _build_base_state()
        state["promptState"]["self"]["arrows"] = 0
        state["promptState"]["target"]["arrows"] = 2
        state["executorFeedback"]["targetRangedThreatActive"] = True

        intent = codex_live_agent.build_heuristic_intent(state)

        self.assertEqual("retreat", intent["mode"])
        self.assertEqual("heuristic_ranged_dodge", intent["reason"])
        self.assertEqual("dash", intent["antiProjectile"])
        self.assertGreaterEqual(intent["dashBias"], 0.8)
        self.assertIsNotNone(codex_live_agent.validate_intent(intent))

    def test_build_heuristic_intent_escapes_after_missed_ultimate_text_feedback(self) -> None:
        state = _build_base_state()
        state["promptState"]["target"]["isUltimateActive"] = False
        state["executorFeedback"]["targetUltimateThreatActive"] = False
        state["executorFeedback"]["botFeedback"] = (
            "missed ultimate escape; action AI PRESSURE; improve: dash or move away before pickups or trades."
        )

        intent = codex_live_agent.build_heuristic_intent(state)

        self.assertEqual("retreat", intent["mode"])
        self.assertEqual("heuristic_missed_ultimate_escape", intent["reason"])
        self.assertLessEqual(intent["advanceBias"], 0.12)
        self.assertLessEqual(intent["meleeBias"], 0.22)
        self.assertGreaterEqual(intent["dashBias"], 0.9)
        self.assertIsNotNone(codex_live_agent.validate_intent(intent))

    def test_build_heuristic_intent_escapes_after_missed_melee_text_feedback(self) -> None:
        state = _build_base_state()
        state["promptState"]["target"]["isMeleeActive"] = False
        state["executorFeedback"]["targetMeleeThreatActive"] = False
        state["executorFeedback"]["botFeedback"] = (
            "missed melee escape; action AI PRESSURE; improve: dash or move away before trading into active melee."
        )

        intent = codex_live_agent.build_heuristic_intent(state)

        self.assertEqual("retreat", intent["mode"])
        self.assertEqual("heuristic_missed_melee_escape", intent["reason"])
        self.assertLessEqual(intent["advanceBias"], 0.18)
        self.assertLessEqual(intent["meleeBias"], 0.26)
        self.assertGreaterEqual(intent["dashBias"], 0.88)
        self.assertIsNotNone(codex_live_agent.validate_intent(intent))

    def test_build_heuristic_intent_responds_after_missed_ranged_text_feedback(self) -> None:
        state = _build_base_state()
        state["promptState"]["self"]["arrows"] = 0
        state["promptState"]["target"]["arrows"] = 2
        state["executorFeedback"]["targetRangedThreatActive"] = False
        state["executorFeedback"]["botFeedback"] = (
            "missed ranged response; action AI COLLECT; improve: dodge, break line, or interrupt before chasing pickups."
        )

        intent = codex_live_agent.build_heuristic_intent(state)

        self.assertEqual("retreat", intent["mode"])
        self.assertEqual("heuristic_missed_ranged_response", intent["reason"])
        self.assertEqual("dash", intent["antiProjectile"])
        self.assertLessEqual(intent["advanceBias"], 0.18)
        self.assertGreaterEqual(intent["dashBias"], 0.8)
        self.assertIsNotNone(codex_live_agent.validate_intent(intent))

    def test_apply_aggression_bias_respects_ultimate_threat_feedback(self) -> None:
        state = _build_base_state()
        state["executorFeedback"]["targetUltimateThreatActive"] = True
        intent = {
            "mode": "pressure",
            "preferredRange": 220,
            "advanceBias": 0.9,
            "shootBias": 0.7,
            "meleeBias": 0.8,
            "dashBias": 0.3,
            "jumpBias": 0.2,
            "antiProjectile": "hold",
            "antiAir": False,
            "punishRecovery": True,
            "cornerEscapeBias": 0.2,
            "focusTargetSlot": 1,
            "expiresInMs": 360,
            "reason": "overaggressive_pressure",
        }

        tuned = codex_live_agent.apply_aggression_bias(intent, state)

        self.assertEqual("retreat", tuned["mode"])
        self.assertEqual("target_ultimate_threat", tuned["reason"])
        self.assertLessEqual(tuned["advanceBias"], 0.12)
        self.assertGreaterEqual(tuned["dashBias"], 0.9)
        self.assertIsNotNone(codex_live_agent.validate_intent(tuned))

    def test_apply_aggression_bias_escapes_after_missed_ultimate_text_feedback(self) -> None:
        state = _build_base_state()
        state["promptState"]["target"]["isUltimateActive"] = False
        state["executorFeedback"]["targetUltimateThreatActive"] = False
        state["executorFeedback"]["botFeedback"] = (
            "missed ultimate escape; action AI PRESSURE; improve: dash or move away before pickups or trades."
        )
        intent = {
            "mode": "pressure",
            "preferredRange": 180,
            "advanceBias": 0.88,
            "shootBias": 0.72,
            "meleeBias": 0.82,
            "dashBias": 0.2,
            "jumpBias": 0.1,
            "antiProjectile": "hold",
            "antiAir": False,
            "punishRecovery": True,
            "cornerEscapeBias": 0.2,
            "focusTargetSlot": 1,
            "expiresInMs": 360,
            "reason": "overaggressive_pressure",
        }

        tuned = codex_live_agent.apply_aggression_bias(intent, state)

        self.assertEqual("retreat", tuned["mode"])
        self.assertEqual("missed_ultimate_escape", tuned["reason"])
        self.assertGreaterEqual(tuned["preferredRange"], 360)
        self.assertLessEqual(tuned["advanceBias"], 0.12)
        self.assertLessEqual(tuned["meleeBias"], 0.22)
        self.assertGreaterEqual(tuned["dashBias"], 0.9)
        self.assertGreaterEqual(tuned["cornerEscapeBias"], 0.78)
        self.assertIsNotNone(codex_live_agent.validate_intent(tuned))

    def test_apply_aggression_bias_defends_after_missed_projectile_feedback(self) -> None:
        state = _build_base_state()
        state["promptState"]["dangerousProjectiles"] = [{"etaSeconds": 0.16}]
        state["executorFeedback"]["projectileThreatActive"] = True
        state["executorFeedback"]["botFeedback"] = (
            "missed projectile defense 0.16s; action AI PRESSURE; improve: dash, jump, parry, or block before attacking."
        )
        intent = {
            "mode": "pressure",
            "preferredRange": 180,
            "advanceBias": 0.92,
            "shootBias": 0.82,
            "meleeBias": 0.86,
            "dashBias": 0.2,
            "jumpBias": 0.1,
            "antiProjectile": "hold",
            "antiAir": False,
            "punishRecovery": True,
            "cornerEscapeBias": 0.2,
            "focusTargetSlot": 1,
            "expiresInMs": 360,
            "reason": "overaggressive_pressure",
        }

        tuned = codex_live_agent.apply_aggression_bias(intent, state)

        self.assertEqual("retreat", tuned["mode"])
        self.assertEqual("missed_projectile_defense", tuned["reason"])
        self.assertEqual("dash", tuned["antiProjectile"])
        self.assertLessEqual(tuned["advanceBias"], 0.16)
        self.assertLessEqual(tuned["meleeBias"], 0.22)
        self.assertGreaterEqual(tuned["dashBias"], 0.9)
        self.assertIsNotNone(codex_live_agent.validate_intent(tuned))

    def test_apply_aggression_bias_defends_after_projectile_threat_text_feedback(self) -> None:
        state = _build_base_state()
        state["promptState"]["dangerousProjectiles"] = []
        state["executorFeedback"]["projectileThreatActive"] = False
        state["executorFeedback"]["botFeedback"] = (
            "projectile threat 0.12s; action AI PARRY HOLD; improve: defend before attacking."
        )
        intent = {
            "mode": "pressure",
            "preferredRange": 180,
            "advanceBias": 0.9,
            "shootBias": 0.72,
            "meleeBias": 0.82,
            "dashBias": 0.2,
            "jumpBias": 0.1,
            "antiProjectile": "hold",
            "antiAir": False,
            "punishRecovery": True,
            "cornerEscapeBias": 0.2,
            "focusTargetSlot": 1,
            "expiresInMs": 360,
            "reason": "overaggressive_pressure",
        }

        tuned = codex_live_agent.apply_aggression_bias(intent, state)

        self.assertEqual("retreat", tuned["mode"])
        self.assertEqual("projectile_threat_feedback", tuned["reason"])
        self.assertLessEqual(tuned["advanceBias"], 0.18)
        self.assertLessEqual(tuned["meleeBias"], 0.24)
        self.assertGreaterEqual(tuned["dashBias"], 0.9)
        self.assertEqual("dash", tuned["antiProjectile"])
        self.assertIsNotNone(codex_live_agent.validate_intent(tuned))

    def test_apply_aggression_bias_escapes_after_missed_melee_text_feedback(self) -> None:
        state = _build_base_state()
        state["promptState"]["target"]["isMeleeActive"] = False
        state["executorFeedback"]["targetMeleeThreatActive"] = False
        state["executorFeedback"]["botFeedback"] = (
            "missed melee escape; action AI PRESSURE; improve: dash or move away before trading into active melee."
        )
        intent = {
            "mode": "pressure",
            "preferredRange": 140,
            "advanceBias": 0.9,
            "shootBias": 0.6,
            "meleeBias": 0.9,
            "dashBias": 0.2,
            "jumpBias": 0.1,
            "antiProjectile": "hold",
            "antiAir": False,
            "punishRecovery": True,
            "cornerEscapeBias": 0.2,
            "focusTargetSlot": 1,
            "expiresInMs": 360,
            "reason": "overaggressive_melee",
        }

        tuned = codex_live_agent.apply_aggression_bias(intent, state)

        self.assertEqual("retreat", tuned["mode"])
        self.assertEqual("missed_melee_escape", tuned["reason"])
        self.assertGreaterEqual(tuned["preferredRange"], 260)
        self.assertLessEqual(tuned["advanceBias"], 0.18)
        self.assertLessEqual(tuned["meleeBias"], 0.26)
        self.assertGreaterEqual(tuned["dashBias"], 0.86)
        self.assertGreaterEqual(tuned["cornerEscapeBias"], 0.7)
        self.assertIsNotNone(codex_live_agent.validate_intent(tuned))

    def test_apply_aggression_bias_responds_after_missed_ranged_text_feedback(self) -> None:
        state = _build_base_state()
        state["promptState"]["self"]["arrows"] = 0
        state["promptState"]["target"]["arrows"] = 2
        state["executorFeedback"]["targetRangedThreatActive"] = False
        state["executorFeedback"]["botFeedback"] = (
            "missed ranged response; action AI COLLECT; improve: dodge, break line, or interrupt before chasing pickups."
        )
        intent = {
            "mode": "pressure",
            "preferredRange": 180,
            "advanceBias": 0.88,
            "shootBias": 0.6,
            "meleeBias": 0.72,
            "dashBias": 0.2,
            "jumpBias": 0.1,
            "antiProjectile": "hold",
            "antiAir": False,
            "punishRecovery": True,
            "cornerEscapeBias": 0.2,
            "focusTargetSlot": 1,
            "expiresInMs": 360,
            "reason": "pickup_chase",
        }

        tuned = codex_live_agent.apply_aggression_bias(intent, state)

        self.assertEqual("retreat", tuned["mode"])
        self.assertEqual("missed_ranged_response", tuned["reason"])
        self.assertGreaterEqual(tuned["preferredRange"], 300)
        self.assertLessEqual(tuned["advanceBias"], 0.24)
        self.assertLessEqual(tuned["shootBias"], 0.28)
        self.assertGreaterEqual(tuned["dashBias"], 0.82)
        self.assertEqual("dash", tuned["antiProjectile"])
        self.assertIsNotNone(codex_live_agent.validate_intent(tuned))

    def test_apply_aggression_bias_closes_on_vulnerable_out_of_range_feedback(self) -> None:
        state = _build_base_state()
        state["promptState"]["arena"] = {
            "roundResetPending": False,
            "horizontalDistance": 1180.0,
            "verticalDistance": 12.0,
            "targetInMeleeRange": False,
            "targetInUltimateRange": False,
            "targetInShootRange": False,
            "targetCornered": False,
            "selfCornered": False,
            "targetAbove": False,
        }
        state["executorFeedback"]["botFeedback"] = (
            "vulnerable target out of range at 1180u; action AI ADVANCE; "
            "improve: close distance before spending attacks."
        )
        intent = {
            "mode": "zone",
            "preferredRange": 520,
            "advanceBias": 0.3,
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
            "reason": "passive_zone",
        }

        tuned = codex_live_agent.apply_aggression_bias(intent, state)

        self.assertEqual("pressure", tuned["mode"])
        self.assertEqual("vulnerable_out_of_range", tuned["reason"])
        self.assertLessEqual(tuned["preferredRange"], 220)
        self.assertGreaterEqual(tuned["advanceBias"], 0.9)
        self.assertLess(tuned["shootBias"], 0.6)
        self.assertIsNotNone(codex_live_agent.validate_intent(tuned))

    def test_apply_aggression_bias_closes_after_out_of_range_shot_feedback(self) -> None:
        state = _build_base_state()
        state["promptState"]["target"]["arrows"] = 2
        state["promptState"]["arena"] = {
            "roundResetPending": False,
            "horizontalDistance": 1180.0,
            "verticalDistance": 12.0,
            "targetInMeleeRange": False,
            "targetInUltimateRange": False,
            "targetInShootRange": False,
            "targetCornered": False,
            "selfCornered": False,
            "targetAbove": False,
        }
        state["executorFeedback"]["botFeedback"] = (
            "shot attempted out of range at 1180u; action AI ZONE SHOT; "
            "improve: close distance, aim a valid line, or hold fire."
        )
        intent = {
            "mode": "zone",
            "preferredRange": 520,
            "advanceBias": 0.2,
            "shootBias": 0.9,
            "meleeBias": 0.24,
            "dashBias": 0.3,
            "jumpBias": 0.2,
            "antiProjectile": "hold",
            "antiAir": False,
            "punishRecovery": True,
            "cornerEscapeBias": 0.26,
            "focusTargetSlot": 1,
            "expiresInMs": 360,
            "reason": "passive_zone",
        }

        tuned = codex_live_agent.apply_aggression_bias(intent, state)

        self.assertEqual("pressure", tuned["mode"])
        self.assertEqual("shot_out_of_range", tuned["reason"])
        self.assertLessEqual(tuned["preferredRange"], 300)
        self.assertGreaterEqual(tuned["advanceBias"], 0.88)
        self.assertGreaterEqual(tuned["dashBias"], 0.78)
        self.assertLess(tuned["shootBias"], 0.5)
        self.assertIsNotNone(codex_live_agent.validate_intent(tuned))

    def test_apply_aggression_bias_recovers_after_empty_shot_feedback(self) -> None:
        state = _build_base_state()
        state["promptState"]["self"]["arrows"] = 0
        state["promptState"]["target"]["arrows"] = 1
        state["promptState"]["recoverableProjectiles"] = [
            {"sourceSlotId": 2, "distanceToSelf": 96.0},
        ]
        state["executorFeedback"]["recoverableProjectileAvailable"] = True
        state["executorFeedback"]["recoverableProjectileCount"] = 1
        state["executorFeedback"]["nearestRecoverableProjectileDistance"] = 96.0
        state["executorFeedback"]["botFeedback"] = (
            "shot attempted without arrows; action AI SHOOT; improve: recover arrow before shooting."
        )
        intent = {
            "mode": "pressure",
            "preferredRange": 180,
            "advanceBias": 0.84,
            "shootBias": 0.82,
            "meleeBias": 0.74,
            "dashBias": 0.3,
            "jumpBias": 0.2,
            "antiProjectile": "hold",
            "antiAir": False,
            "punishRecovery": True,
            "cornerEscapeBias": 0.2,
            "focusTargetSlot": 1,
            "expiresInMs": 360,
            "reason": "stale_pressure",
        }

        tuned = codex_live_agent.apply_aggression_bias(intent, state)

        self.assertEqual("retreat", tuned["mode"])
        self.assertEqual("empty_shot_recover_arrow", tuned["reason"])
        self.assertLessEqual(tuned["shootBias"], 0.1)
        self.assertLessEqual(tuned["meleeBias"], 0.3)
        self.assertGreaterEqual(tuned["dashBias"], 0.78)
        self.assertGreaterEqual(tuned["cornerEscapeBias"], 0.82)
        self.assertIsNotNone(codex_live_agent.validate_intent(tuned))

    def test_apply_aggression_bias_recovers_after_missed_arrow_recovery_feedback(self) -> None:
        state = _build_base_state()
        state["promptState"]["self"]["arrows"] = 1
        state["promptState"]["target"]["arrows"] = 2
        state["promptState"]["recoverableProjectiles"] = [
            {"sourceSlotId": 2, "distanceToSelf": 96.0},
        ]
        state["executorFeedback"]["recoverableProjectileAvailable"] = True
        state["executorFeedback"]["recoverableProjectileCount"] = 1
        state["executorFeedback"]["nearestRecoverableProjectileDistance"] = 96.0
        state["executorFeedback"]["botFeedback"] = (
            "missed arrow recovery at 96u; action AI PRESSURE; improve: move toward pickup before forcing trades."
        )
        intent = {
            "mode": "pressure",
            "preferredRange": 180,
            "advanceBias": 0.9,
            "shootBias": 0.7,
            "meleeBias": 0.72,
            "dashBias": 0.3,
            "jumpBias": 0.2,
            "antiProjectile": "hold",
            "antiAir": False,
            "punishRecovery": True,
            "cornerEscapeBias": 0.2,
            "focusTargetSlot": 1,
            "expiresInMs": 360,
            "reason": "stale_pressure",
        }

        tuned = codex_live_agent.apply_aggression_bias(intent, state)

        self.assertEqual("retreat", tuned["mode"])
        self.assertEqual("missed_arrow_recovery", tuned["reason"])
        self.assertLessEqual(tuned["shootBias"], 0.18)
        self.assertLessEqual(tuned["meleeBias"], 0.32)
        self.assertGreaterEqual(tuned["dashBias"], 0.78)
        self.assertGreaterEqual(tuned["cornerEscapeBias"], 0.82)
        self.assertIsNotNone(codex_live_agent.validate_intent(tuned))

    def test_apply_aggression_bias_prioritizes_recover_arrow_feedback(self) -> None:
        state = _build_base_state()
        state["promptState"]["self"]["arrows"] = 0
        state["promptState"]["target"]["arrows"] = 2
        state["promptState"]["recoverableProjectiles"] = [
            {"sourceSlotId": 2, "distanceToSelf": 128.0},
        ]
        state["executorFeedback"]["recoverableProjectileAvailable"] = True
        state["executorFeedback"]["recoverableProjectileCount"] = 1
        state["executorFeedback"]["nearestRecoverableProjectileDistance"] = 128.0
        state["executorFeedback"]["botFeedback"] = (
            "recover arrow at 128u; action AI RETREAT; improve: recover ammo before forcing trades."
        )
        intent = {
            "mode": "pressure",
            "preferredRange": 180,
            "advanceBias": 0.9,
            "shootBias": 0.7,
            "meleeBias": 0.72,
            "dashBias": 0.3,
            "jumpBias": 0.2,
            "antiProjectile": "hold",
            "antiAir": False,
            "punishRecovery": True,
            "cornerEscapeBias": 0.2,
            "focusTargetSlot": 1,
            "expiresInMs": 360,
            "reason": "stale_pressure",
        }

        tuned = codex_live_agent.apply_aggression_bias(intent, state)

        self.assertEqual("retreat", tuned["mode"])
        self.assertEqual("recover_arrow_feedback", tuned["reason"])
        self.assertLessEqual(tuned["shootBias"], 0.16)
        self.assertLessEqual(tuned["meleeBias"], 0.3)
        self.assertGreaterEqual(tuned["dashBias"], 0.82)
        self.assertGreaterEqual(tuned["cornerEscapeBias"], 0.84)
        self.assertIsNotNone(codex_live_agent.validate_intent(tuned))

    def test_apply_aggression_bias_rechallenges_after_missed_anti_air_feedback(self) -> None:
        state = _build_base_state()
        state["promptState"]["arena"] = {
            "roundResetPending": False,
            "horizontalDistance": 280.0,
            "verticalDistance": 180.0,
            "targetInMeleeRange": False,
            "targetInUltimateRange": False,
            "targetInShootRange": True,
            "targetCornered": False,
            "selfCornered": False,
            "targetAbove": True,
        }
        state["executorFeedback"]["botFeedback"] = (
            "missed anti-air; action AI DRIFT; improve: shoot, jump, or aim upward before the target lands."
        )
        intent = {
            "mode": "zone",
            "preferredRange": 420,
            "advanceBias": 0.2,
            "shootBias": 0.2,
            "meleeBias": 0.7,
            "dashBias": 0.2,
            "jumpBias": 0.1,
            "antiProjectile": "hold",
            "antiAir": False,
            "punishRecovery": True,
            "cornerEscapeBias": 0.2,
            "focusTargetSlot": 1,
            "expiresInMs": 360,
            "reason": "passive_zone",
        }

        tuned = codex_live_agent.apply_aggression_bias(intent, state)

        self.assertEqual("pressure", tuned["mode"])
        self.assertEqual("missed_anti_air", tuned["reason"])
        self.assertTrue(tuned["antiAir"])
        self.assertGreaterEqual(tuned["shootBias"], 0.68)
        self.assertGreaterEqual(tuned["jumpBias"], 0.7)
        self.assertGreaterEqual(tuned["dashBias"], 0.68)
        self.assertIsNotNone(codex_live_agent.validate_intent(tuned))

    def test_apply_aggression_bias_challenges_anti_air_opportunity_feedback(self) -> None:
        state = _build_base_state()
        state["promptState"]["arena"] = {
            "roundResetPending": False,
            "horizontalDistance": 280.0,
            "verticalDistance": 180.0,
            "targetInMeleeRange": False,
            "targetInUltimateRange": False,
            "targetInShootRange": True,
            "targetCornered": False,
            "selfCornered": False,
            "targetAbove": True,
        }
        state["executorFeedback"]["botFeedback"] = (
            "anti-air opportunity; action AI SHOOT; improve: challenge vertical approaches before landing."
        )
        intent = {
            "mode": "zone",
            "preferredRange": 420,
            "advanceBias": 0.2,
            "shootBias": 0.3,
            "meleeBias": 0.7,
            "dashBias": 0.2,
            "jumpBias": 0.1,
            "antiProjectile": "hold",
            "antiAir": False,
            "punishRecovery": True,
            "cornerEscapeBias": 0.2,
            "focusTargetSlot": 1,
            "expiresInMs": 360,
            "reason": "passive_zone",
        }

        tuned = codex_live_agent.apply_aggression_bias(intent, state)

        self.assertEqual("pressure", tuned["mode"])
        self.assertEqual("anti_air_opportunity", tuned["reason"])
        self.assertTrue(tuned["antiAir"])
        self.assertGreaterEqual(tuned["shootBias"], 0.72)
        self.assertGreaterEqual(tuned["jumpBias"], 0.5)
        self.assertGreaterEqual(tuned["dashBias"], 0.64)
        self.assertIsNotNone(codex_live_agent.validate_intent(tuned))

    def test_apply_aggression_bias_converts_missed_punish_window_feedback(self) -> None:
        state = _build_base_state()
        state["promptState"]["target"]["arrows"] = 0
        state["promptState"]["target"]["isHitStunned"] = False
        state["promptState"]["arena"] = {
            "roundResetPending": False,
            "horizontalDistance": 128.0,
            "verticalDistance": 8.0,
            "targetInMeleeRange": True,
            "targetInUltimateRange": True,
            "targetInShootRange": True,
            "targetCornered": False,
            "selfCornered": False,
            "targetAbove": False,
        }
        state["executorFeedback"]["botFeedback"] = (
            "missed punish window; action AI PRESSURE; improve: fire, melee, or ultimate before target recovers."
        )
        intent = {
            "mode": "zone",
            "preferredRange": 420,
            "advanceBias": 0.2,
            "shootBias": 0.3,
            "meleeBias": 0.24,
            "dashBias": 0.2,
            "jumpBias": 0.1,
            "antiProjectile": "hold",
            "antiAir": False,
            "punishRecovery": True,
            "cornerEscapeBias": 0.2,
            "focusTargetSlot": 1,
            "expiresInMs": 360,
            "reason": "passive_zone",
        }

        tuned = codex_live_agent.apply_aggression_bias(intent, state)

        self.assertEqual("punish", tuned["mode"])
        self.assertEqual("missed_punish_window", tuned["reason"])
        self.assertLessEqual(tuned["preferredRange"], 160)
        self.assertGreaterEqual(tuned["meleeBias"], 0.9)
        self.assertGreaterEqual(tuned["shootBias"], 0.66)
        self.assertGreaterEqual(tuned["dashBias"], 0.8)
        self.assertIsNotNone(codex_live_agent.validate_intent(tuned))

    def test_apply_aggression_bias_converts_punish_window_available_feedback(self) -> None:
        state = _build_base_state()
        state["promptState"]["target"]["isHitStunned"] = False
        state["promptState"]["arena"] = {
            "roundResetPending": False,
            "horizontalDistance": 148.0,
            "verticalDistance": 8.0,
            "targetInMeleeRange": True,
            "targetInUltimateRange": True,
            "targetInShootRange": True,
            "targetCornered": False,
            "selfCornered": False,
            "targetAbove": False,
        }
        state["executorFeedback"]["botFeedback"] = (
            "punish window available; action AI MELEE; improve: convert vulnerability quickly."
        )
        intent = {
            "mode": "zone",
            "preferredRange": 420,
            "advanceBias": 0.2,
            "shootBias": 0.3,
            "meleeBias": 0.24,
            "dashBias": 0.2,
            "jumpBias": 0.1,
            "antiProjectile": "hold",
            "antiAir": False,
            "punishRecovery": True,
            "cornerEscapeBias": 0.2,
            "focusTargetSlot": 1,
            "expiresInMs": 360,
            "reason": "passive_zone",
        }

        tuned = codex_live_agent.apply_aggression_bias(intent, state)

        self.assertEqual("punish", tuned["mode"])
        self.assertEqual("punish_window_available", tuned["reason"])
        self.assertLessEqual(tuned["preferredRange"], 160)
        self.assertGreaterEqual(tuned["meleeBias"], 0.9)
        self.assertGreaterEqual(tuned["shootBias"], 0.66)
        self.assertGreaterEqual(tuned["dashBias"], 0.8)
        self.assertIsNotNone(codex_live_agent.validate_intent(tuned))

    def test_apply_aggression_bias_escapes_after_missed_corner_feedback(self) -> None:
        state = _build_base_state()
        state["promptState"]["self"]["arrows"] = 2
        state["promptState"]["target"]["arrows"] = 0
        state["promptState"]["arena"] = {
            "roundResetPending": False,
            "horizontalDistance": 340.0,
            "verticalDistance": 12.0,
            "targetInMeleeRange": False,
            "targetInUltimateRange": False,
            "targetInShootRange": True,
            "targetCornered": False,
            "selfCornered": True,
            "targetAbove": False,
        }
        state["executorFeedback"]["botFeedback"] = (
            "missed corner escape; action AI PRESSURE; improve: move toward center before committing."
        )
        intent = {
            "mode": "pressure",
            "preferredRange": 180,
            "advanceBias": 0.9,
            "shootBias": 0.72,
            "meleeBias": 0.74,
            "dashBias": 0.2,
            "jumpBias": 0.1,
            "antiProjectile": "hold",
            "antiAir": False,
            "punishRecovery": True,
            "cornerEscapeBias": 0.2,
            "focusTargetSlot": 1,
            "expiresInMs": 360,
            "reason": "last_arrow_pressure",
        }

        tuned = codex_live_agent.apply_aggression_bias(intent, state)

        self.assertEqual("retreat", tuned["mode"])
        self.assertEqual("missed_corner_escape", tuned["reason"])
        self.assertGreaterEqual(tuned["cornerEscapeBias"], 0.9)
        self.assertGreaterEqual(tuned["dashBias"], 0.86)
        self.assertGreaterEqual(tuned["jumpBias"], 0.55)
        self.assertLessEqual(tuned["advanceBias"], 0.18)
        self.assertIsNotNone(codex_live_agent.validate_intent(tuned))

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

    def test_build_heuristic_intent_waits_when_executor_reports_no_visible_target(self) -> None:
        state = _build_base_state()
        state["executorFeedback"]["targetVisible"] = False
        state["promptState"]["self"]["arrows"] = 2
        state["promptState"]["target"]["arrows"] = 0

        intent = codex_live_agent.build_heuristic_intent(state)

        self.assertEqual("stabilize", intent["mode"])
        self.assertEqual("heuristic_waiting_for_target", intent["reason"])
        self.assertLessEqual(intent["advanceBias"], 0.2)
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

    def test_apply_aggression_bias_does_not_promote_last_arrow_without_visible_target_feedback(self) -> None:
        state = _build_base_state()
        state["executorFeedback"]["targetVisible"] = False
        state["promptState"]["self"]["arrows"] = 2
        state["promptState"]["target"]["arrows"] = 0

        intent = {
            "mode": "stabilize",
            "preferredRange": 280,
            "advanceBias": 0.2,
            "shootBias": 0.2,
            "meleeBias": 0.2,
            "dashBias": 0.15,
            "jumpBias": 0.15,
            "antiProjectile": "hold",
            "antiAir": False,
            "punishRecovery": True,
            "cornerEscapeBias": 0.4,
            "focusTargetSlot": 1,
            "expiresInMs": 360,
            "reason": "heuristic_waiting_for_target",
        }

        tuned = codex_live_agent.apply_aggression_bias(intent, state)

        self.assertEqual("stabilize", tuned["mode"])
        self.assertEqual("heuristic_waiting_for_target", tuned["reason"])
        self.assertLessEqual(tuned["advanceBias"], 0.2)
        self.assertIsNotNone(codex_live_agent.validate_intent(tuned))


if __name__ == "__main__":
    unittest.main()
