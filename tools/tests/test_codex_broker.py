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
    def test_agent_session_payload_defaults_missing_target_visibility_to_false(self) -> None:
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

        payload = session.state_payload()
        report = session.report_payload()

        self.assertIn("targetVisible", payload["executorFeedback"])
        self.assertFalse(payload["executorFeedback"]["targetVisible"])
        self.assertFalse(report["targetVisible"])
        self.assertEqual(-1, report["horizontalDistance"])
        self.assertEqual(0, report["verticalDistance"])
        self.assertFalse(report["targetInShootRange"])
        self.assertFalse(report["shouldAntiAir"])
        self.assertEqual(-1, report["selfArrows"])
        self.assertEqual(-1, report["targetArrows"])

    def test_agent_session_publish_state_normalizes_partial_executor_feedback(self) -> None:
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
                    "botFeedback": "action AI HOLD; improve: wait for visible target.",
                },
            }
        )

        payload = session.state_payload()

        self.assertEqual("codex", payload["executorFeedback"]["source"])
        self.assertFalse(payload["executorFeedback"]["targetVisible"])

    def test_agent_session_publish_state_ignores_non_object_executor_feedback(self) -> None:
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

        session.publish_state({"frame": 2, "executorFeedback": "not-an-object"})

        payload = session.state_payload()

        self.assertFalse(payload["executorFeedback"]["targetVisible"])
        self.assertFalse(payload["executorFeedback"]["roundResetPending"])

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

    def test_report_payload_derives_bot_feedback_from_agent_action_when_executor_feedback_is_missing(self) -> None:
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
                "note": "Posted stabilize (heuristic_waiting_for_target)",
            }
        )
        session.publish_action(
            {
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
        )

        report = session.report_payload()

        self.assertEqual(
            "agent action stabilize; reason heuristic_waiting_for_target; improve: wait for visible target before committing.",
            report["botFeedback"],
        )

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
                    "horizontalDistance": 248.5,
                    "verticalDistance": 96,
                    "targetAbove": True,
                    "targetInShootRange": True,
                    "targetInMeleeRange": False,
                    "targetInUltimateRange": True,
                    "targetVulnerable": True,
                    "shouldAntiAir": True,
                    "selfArrows": 0,
                    "targetArrows": 3,
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
        self.assertEqual(248.5, report["horizontalDistance"])
        self.assertEqual(96, report["verticalDistance"])
        self.assertTrue(report["targetAbove"])
        self.assertTrue(report["targetInShootRange"])
        self.assertFalse(report["targetInMeleeRange"])
        self.assertTrue(report["targetInUltimateRange"])
        self.assertTrue(report["targetVulnerable"])
        self.assertTrue(report["shouldAntiAir"])
        self.assertEqual(0, report["selfArrows"])
        self.assertEqual(3, report["targetArrows"])

    def test_report_payload_normalizes_summary_for_current_no_target(self) -> None:
        session = codex_broker.AgentDrivenSession(
            2,
            "agent-session",
            {
                "frame": 1,
                "self": {"botId": "bot-slot-2"},
                "arena": {"horizontalDistance": 320.0},
            },
        )

        session.publish_state(
            {
                "frame": 2,
                "executorFeedback": {
                    "source": "codex",
                    "summary": "AI DEFENSIVE RETREAT",
                    "intentMode": "pressure",
                    "intentReason": "heuristic_anti_air",
                    "botFeedback": "no target visible; improve: verify spawn, camera, or opponent tracking.",
                    "targetVisible": False,
                    "roundResetPending": False,
                },
            }
        )

        report = session.report_payload()
        payload = session.state_payload()

        self.assertEqual("AI | Fallback:no_target", report["summary"])
        self.assertFalse(report["targetVisible"])
        self.assertEqual("stabilize", payload["executorFeedback"]["intentMode"])
        self.assertEqual("heuristic_waiting_for_target", payload["executorFeedback"]["intentReason"])
        self.assertEqual("stabilize", report["feedbackIntentMode"])
        self.assertEqual("heuristic_waiting_for_target", report["feedbackIntentReason"])

    def test_report_payload_prioritizes_projectile_threat_when_target_is_missing(self) -> None:
        session = codex_broker.AgentDrivenSession(
            2,
            "agent-session",
            {
                "frame": 1,
                "self": {"botId": "bot-slot-2"},
                "arena": {"horizontalDistance": 320.0},
            },
        )

        session.publish_state(
            {
                "frame": 2,
                "executorFeedback": {
                    "source": "codex",
                    "summary": "AI | Fallback:no_target",
                    "intentMode": "stabilize",
                    "intentReason": "heuristic_waiting_for_target",
                    "botFeedback": "no target visible; improve: verify spawn, camera, or opponent tracking.",
                    "targetVisible": False,
                    "projectileThreatActive": True,
                    "roundResetPending": False,
                    "reportedInput": {
                        "frame": 37,
                        "axis": -1.0,
                        "aim": {"x": -1.0, "y": 0.0},
                        "dashPrimaryPressed": True,
                    },
                },
            }
        )

        report = session.report_payload()
        payload = session.state_payload()

        self.assertEqual("AI PROJECTILE THREAT", report["summary"])
        self.assertFalse(report["targetVisible"])
        self.assertTrue(report["projectileThreatActive"])
        self.assertEqual("retreat", payload["executorFeedback"]["intentMode"])
        self.assertEqual("projectile_threat_feedback", payload["executorFeedback"]["intentReason"])
        self.assertEqual("retreat", report["feedbackIntentMode"])
        self.assertEqual("projectile_threat_feedback", report["feedbackIntentReason"])
        self.assertEqual(
            "projectile threat active now; action AI DASH; improve: defend before attacking.",
            report["botFeedback"],
        )

    def test_report_payload_uses_move_summary_when_executor_summary_is_empty(self) -> None:
        session = codex_broker.AgentDrivenSession(
            1,
            "agent-session",
            {
                "frame": 1,
                "self": {"botId": "bot-slot-1"},
                "target": {"slotId": 2},
                "arena": {"horizontalDistance": 980.0},
            },
        )

        session.publish_state(
            {
                "frame": 2,
                "executorFeedback": {
                    "source": "codex",
                    "summary": "",
                    "botFeedback": "spacing stable at 980u; action none; improve: keep pressure without wasting arrows.",
                    "targetVisible": True,
                    "roundResetPending": False,
                    "projectileThreatActive": False,
                    "targetRangedThreatActive": False,
                    "targetUltimateThreatActive": False,
                    "selfCornered": False,
                },
            }
        )

        report = session.report_payload()

        self.assertEqual("AI MOVE", report["summary"])
        self.assertEqual(
            "spacing stable at 980u; action none; improve: keep pressure without wasting arrows.",
            report["botFeedback"],
        )

    def test_state_payload_marks_long_range_pressure_stalled_when_input_is_weak(self) -> None:
        session = codex_broker.AgentDrivenSession(
            1,
            "agent-session",
            {
                "frame": 1,
                "self": {"botId": "bot-slot-1"},
                "target": {"slotId": 2},
                "arena": {"horizontalDistance": 1551.0},
            },
        )
        session.publish_action(
            {
                "mode": "pressure",
                "reason": "heuristic_close_distance",
                "antiAir": False,
            }
        )
        session.publish_state(
            {
                "frame": 92,
                "executorFeedback": {
                    "source": "codex",
                    "summary": "AI MOVE",
                    "intentMode": "pressure",
                    "intentReason": "heuristic_close_distance",
                    "botFeedback": "spacing stable at 1551u; action AI MOVE; improve: keep pressure without wasting arrows.",
                    "targetVisible": True,
                    "roundResetPending": False,
                    "projectileThreatActive": False,
                    "targetRangedThreatActive": False,
                    "targetMeleeThreatActive": False,
                    "targetUltimateThreatActive": False,
                    "selfCornered": False,
                    "targetAbove": False,
                    "targetInShootRange": False,
                    "targetInMeleeRange": False,
                    "horizontalDistance": 1551.0,
                    "reportedInput": {
                        "axis": -0.11,
                        "aim": {"x": 0.96, "y": 0.27},
                        "jumpPressed": False,
                        "jumpHeld": False,
                        "dashPrimaryPressed": False,
                        "dashSecondaryPressed": False,
                        "shootPressed": False,
                        "shootHeld": False,
                        "meleePressed": False,
                    },
                },
            }
        )

        payload = session.state_payload()
        report = session.report_payload()

        self.assertEqual("AI LONG RANGE STALLED", payload["executorFeedback"]["summary"])
        self.assertIn("far target chase stalled", payload["executorFeedback"]["botFeedback"])
        self.assertEqual("pressure", payload["executorFeedback"]["intentMode"])
        self.assertEqual("heuristic_far_target_chase", payload["executorFeedback"]["intentReason"])
        self.assertEqual("AI LONG RANGE STALLED", report["summary"])
        self.assertEqual("pressure", report["intentMode"])
        self.assertEqual("heuristic_far_target_chase", report["intentReason"])

    def test_state_payload_marks_arrow_recovery_stalled_when_pickup_input_is_idle(self) -> None:
        session = codex_broker.AgentDrivenSession(
            1,
            "agent-session",
            {
                "frame": 1,
                "self": {"botId": "bot-slot-1"},
                "target": {"slotId": 2},
                "arena": {"horizontalDistance": 268.0},
            },
        )
        session.publish_action(
            {
                "mode": "retreat",
                "reason": "missed_arrow_recovery",
                "antiAir": False,
            }
        )
        session.publish_state(
            {
                "frame": 9565,
                "executorFeedback": {
                    "source": "codex_live",
                    "summary": "AI COLLECT ARROW",
                    "intentMode": "retreat",
                    "intentReason": "missed_arrow_recovery",
                    "botFeedback": "missed arrow recovery at 292u; action AI COLLECT ARROW; improve: move toward pickup before forcing trades.",
                    "targetVisible": True,
                    "roundResetPending": False,
                    "projectileThreatActive": False,
                    "targetRangedThreatActive": False,
                    "targetMeleeThreatActive": False,
                    "targetUltimateThreatActive": False,
                    "selfArrows": 0,
                    "targetArrows": 1,
                    "recoverableProjectileAvailable": True,
                    "recoverableProjectileCount": 4,
                    "nearestRecoverableProjectileDistance": 297.6,
                    "reportedInput": {
                        "axis": 0.0,
                        "aim": {"x": 0.99, "y": -0.10},
                        "jumpPressed": False,
                        "jumpHeld": False,
                        "dashPrimaryPressed": False,
                        "dashSecondaryPressed": False,
                    },
                },
            }
        )

        payload = session.state_payload()
        report = session.report_payload()

        self.assertEqual("AI ARROW RECOVERY STALLED", payload["executorFeedback"]["summary"])
        self.assertIn("arrow recovery movement stalled at 298u", payload["executorFeedback"]["botFeedback"])
        self.assertEqual("retreat", payload["executorFeedback"]["intentMode"])
        self.assertEqual("heuristic_movement_stall_escape", payload["executorFeedback"]["intentReason"])
        self.assertEqual("AI ARROW RECOVERY STALLED", report["summary"])
        self.assertEqual("heuristic_movement_stall_escape", report["intentReason"])

    def test_report_payload_normalizes_summary_for_current_corner_threat(self) -> None:
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
                    "summary": "AI DEFENSIVE RETREAT",
                    "botFeedback": "missed corner escape; action AI DEFENSIVE RETREAT; improve: move toward center before committing.",
                    "targetVisible": True,
                    "selfCornered": True,
                    "roundResetPending": False,
                },
            }
        )

        report = session.report_payload()

        self.assertEqual("AI CORNER THREAT", report["summary"])
        self.assertTrue(report["selfCornered"])

    def test_report_payload_clears_resolved_corner_escape(self) -> None:
        session = codex_broker.AgentDrivenSession(
            1,
            "agent-session",
            {
                "frame": 1,
                "self": {"botId": "bot-slot-1"},
                "target": {"slotId": 2},
                "arena": {"horizontalDistance": 940.0},
            },
        )

        session.publish_state(
            {
                "frame": 2,
                "executorFeedback": {
                    "source": "codex",
                    "summary": "AI CORNER ESCAPE",
                    "botFeedback": "corner pressure detected; action AI CORNER ESCAPE; improve: escape corner before committing.",
                    "targetVisible": True,
                    "selfCornered": False,
                    "targetCornered": False,
                    "projectileThreatActive": False,
                    "targetRangedThreatActive": False,
                    "targetMeleeThreatActive": False,
                    "targetUltimateThreatActive": False,
                    "roundResetPending": False,
                    "reportedInput": {
                        "frame": 72,
                        "axis": 0.65,
                        "aim": {"x": 1.0, "y": 0.0},
                    },
                },
            }
        )

        payload = session.state_payload()
        report = session.report_payload()

        self.assertEqual("AI RESOLVED CORNER PRESSURE", report["summary"])
        self.assertEqual(
            "corner pressure resolved; action AI MOVE; improve: retake center control before committing.",
            payload["executorFeedback"]["botFeedback"],
        )
        self.assertEqual(
            "corner pressure resolved; action AI MOVE; improve: retake center control before committing.",
            report["botFeedback"],
        )

    def test_report_payload_normalizes_summary_for_current_ranged_threat(self) -> None:
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
                    "summary": "AI COLLECT ARROW",
                    "botFeedback": "ranged threat active now; action pending; improve: dodge, break line, or interrupt before chasing pickups.",
                    "targetVisible": True,
                    "targetRangedThreatActive": True,
                    "selfArrows": 2,
                    "roundResetPending": False,
                    "reportedInput": {
                        "frame": 91,
                        "axis": -1.0,
                        "aim": {"x": -1.0, "y": 0.0},
                        "jumpPressed": True,
                    },
                },
            }
        )

        report = session.report_payload()
        payload = session.state_payload()

        self.assertEqual("AI RANGED THREAT", report["summary"])
        self.assertTrue(report["targetRangedThreatActive"])
        self.assertEqual(
            "ranged threat active now; action AI JUMP; improve: dodge, break line, or interrupt before chasing pickups.",
            payload["executorFeedback"]["botFeedback"],
        )
        self.assertEqual(
            "ranged threat active now; action AI JUMP; improve: dodge, break line, or interrupt before chasing pickups.",
            report["botFeedback"],
        )

    def test_report_payload_clears_resolved_ranged_threat_retreat(self) -> None:
        session = codex_broker.AgentDrivenSession(
            2,
            "agent-session",
            {
                "frame": 1,
                "self": {"botId": "bot-slot-2"},
                "target": {"slotId": 1},
                "arena": {"horizontalDistance": 340.0},
            },
        )

        session.publish_action(
            {
                "mode": "retreat",
                "reason": "target_ranged_threat",
            }
        )
        session.publish_state(
            {
                "frame": 2,
                "executorFeedback": {
                    "source": "codex",
                    "summary": "AI DEFENSIVE RETREAT",
                    "botFeedback": "enemy ranged active; action AI DEFENSIVE RETREAT; improve: clear the arrow line before committing.",
                    "targetVisible": True,
                    "targetInShootRange": True,
                    "projectileThreatActive": False,
                    "targetRangedThreatActive": False,
                    "targetMeleeThreatActive": False,
                    "targetUltimateThreatActive": False,
                    "selfCornered": False,
                    "selfArrows": 3,
                    "targetArrows": 1,
                    "roundResetPending": False,
                    "reportedInput": {
                        "frame": 41,
                        "axis": -0.79,
                        "aim": {"x": 1.0, "y": 0.0},
                        "shootPressed": True,
                    },
                },
            }
        )

        report = session.report_payload()

        self.assertEqual("AI RESOLVED THREAT PRESSURE", report["summary"])
        self.assertEqual(
            "resolved threat pressure active now; action AI SHOOT; improve: stop retreating and retake the shot window.",
            report["botFeedback"],
        )

    def test_state_payload_normalizes_feedback_for_current_ranged_threat(self) -> None:
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
                    "summary": "AI MOVE",
                    "intentMode": "retreat",
                    "intentReason": "projectile_threat_feedback",
                    "botFeedback": "missed ranged response; action AI MOVE; improve: dodge, break line, or interrupt before chasing pickups.",
                    "targetVisible": True,
                    "targetRangedThreatActive": True,
                    "selfArrows": 2,
                    "roundResetPending": False,
                    "reportedInput": {
                        "frame": 91,
                        "axis": -1.0,
                        "aim": {"x": -1.0, "y": 0.0},
                        "jumpPressed": True,
                    },
                },
            }
        )

        payload = session.state_payload()
        report = session.report_payload()

        self.assertEqual("AI RANGED THREAT", report["summary"])
        self.assertEqual(
            "ranged threat active now; action AI JUMP; improve: dodge, break line, or interrupt before chasing pickups.",
            payload["executorFeedback"]["botFeedback"],
        )
        self.assertEqual(
            "ranged threat active now; action AI JUMP; improve: dodge, break line, or interrupt before chasing pickups.",
            report["botFeedback"],
        )
        self.assertEqual("pressure", payload["executorFeedback"]["intentMode"])
        self.assertEqual("target_ranged_threat", payload["executorFeedback"]["intentReason"])
        self.assertEqual("pressure", report["feedbackIntentMode"])
        self.assertEqual("target_ranged_threat", report["feedbackIntentReason"])

    def test_report_payload_replaces_stale_projectile_feedback_for_current_ranged_threat(self) -> None:
        session = codex_broker.AgentDrivenSession(
            2,
            "agent-session",
            {
                "frame": 1,
                "self": {"botId": "bot-slot-2"},
                "target": {"slotId": 1},
                "arena": {"horizontalDistance": 626.0},
            },
        )

        session.publish_state(
            {
                "frame": 2,
                "executorFeedback": {
                    "source": "codex",
                    "summary": "AI RANGED THREAT",
                    "intentMode": "retreat",
                    "intentReason": "projectile_threat_feedback",
                    "botFeedback": "projectile threat 0.27s; action AI PROJECTILE DRIFT; improve: defend before attacking.",
                    "targetVisible": True,
                    "projectileThreatActive": False,
                    "targetRangedThreatActive": True,
                    "targetUltimateThreatActive": False,
                    "selfArrows": 2,
                    "roundResetPending": False,
                    "reportedInput": {
                        "frame": 144,
                        "axis": 0.0,
                        "aim": {"x": -1.0, "y": 0.0},
                        "dashPrimaryPressed": True,
                    },
                },
            }
        )

        payload = session.state_payload()
        report = session.report_payload()

        self.assertEqual("AI RANGED THREAT", report["summary"])
        self.assertEqual(
            "ranged threat active now; action AI DASH; improve: dodge, break line, or interrupt before chasing pickups.",
            payload["executorFeedback"]["botFeedback"],
        )
        self.assertEqual(
            "ranged threat active now; action AI DASH; improve: dodge, break line, or interrupt before chasing pickups.",
            report["botFeedback"],
        )
        self.assertEqual("pressure", report["feedbackIntentMode"])
        self.assertEqual("target_ranged_threat", report["feedbackIntentReason"])

    def test_report_payload_uses_feedback_intent_as_effective_intent(self) -> None:
        session = codex_broker.AgentDrivenSession(
            1,
            "agent-session",
            {
                "frame": 1,
                "self": {"botId": "bot-slot-1"},
                "target": {"slotId": 2},
                "arena": {"horizontalDistance": 575.0},
            },
        )
        session.publish_action(
            {
                "mode": "pressure",
                "reason": "heuristic_default_pressure",
                "antiProjectile": "hold",
            }
        )
        session.publish_state(
            {
                "frame": 2,
                "executorFeedback": {
                    "source": "codex",
                    "summary": "AI PROJECTILE THREAT",
                    "botFeedback": "projectile threat active now; action pending; improve: defend before attacking.",
                    "intentMode": "retreat",
                    "intentReason": "projectile_threat_feedback",
                    "targetVisible": True,
                    "projectileThreatActive": True,
                    "targetRangedThreatActive": True,
                    "selfArrows": 3,
                    "targetArrows": 2,
                    "roundResetPending": False,
                },
            }
        )

        report = session.report_payload()

        self.assertEqual("AI PROJECTILE THREAT", report["summary"])
        self.assertEqual("retreat", report["intentMode"])
        self.assertEqual("projectile_threat_feedback", report["intentReason"])
        self.assertEqual("pressure", report["cachedIntentMode"])
        self.assertEqual("heuristic_default_pressure", report["cachedIntentReason"])
        self.assertEqual("retreat", report["feedbackIntentMode"])
        self.assertEqual("projectile_threat_feedback", report["feedbackIntentReason"])

    def test_state_payload_replaces_waiting_for_target_when_target_is_visible(self) -> None:
        session = codex_broker.AgentDrivenSession(
            2,
            "agent-session",
            {
                "frame": 1,
                "self": {"botId": "bot-slot-2"},
                "target": {"slotId": 1},
                "arena": {"horizontalDistance": 1333.0},
            },
        )
        session.publish_action(
            {
                "mode": "pressure",
                "reason": "heuristic_anti_air",
                "antiAir": False,
            }
        )
        session.publish_state(
            {
                "frame": 15,
                "executorFeedback": {
                    "source": "codex",
                    "summary": "AI MOVE",
                    "botFeedback": "spacing stable at 1333u; action AI MOVE; improve: keep pressure without wasting arrows.",
                    "intentMode": "stabilize",
                    "intentReason": "heuristic_waiting_for_target",
                    "targetVisible": True,
                    "targetAbove": True,
                    "targetInShootRange": False,
                    "verticalDistance": 100.0,
                    "projectileThreatActive": False,
                    "targetRangedThreatActive": False,
                    "targetUltimateThreatActive": False,
                    "selfArrows": 3,
                    "targetArrows": 2,
                    "roundResetPending": False,
                },
            }
        )

        payload = session.state_payload()
        report = session.report_payload()

        self.assertEqual("pressure", payload["executorFeedback"]["intentMode"])
        self.assertEqual("heuristic_anti_air", payload["executorFeedback"]["intentReason"])
        self.assertEqual("AI ANTI AIR CHASE", report["summary"])
        self.assertEqual("pressure", report["intentMode"])
        self.assertEqual("heuristic_anti_air", report["intentReason"])
        self.assertEqual("pressure", report["feedbackIntentMode"])
        self.assertEqual("heuristic_anti_air", report["feedbackIntentReason"])

    def test_report_payload_normalizes_current_anti_air_chase_from_cached_intent(self) -> None:
        session = codex_broker.AgentDrivenSession(
            2,
            "agent-session",
            {
                "frame": 1,
                "self": {"botId": "bot-slot-2"},
                "target": {"slotId": 1},
                "arena": {"horizontalDistance": 1120.0},
            },
        )

        session.publish_action(
            {
                "mode": "pressure",
                "reason": "heuristic_anti_air",
                "antiAir": False,
            }
        )
        session.publish_state(
            {
                "frame": 2,
                "executorFeedback": {
                    "source": "codex",
                    "summary": "AI MOVE",
                    "botFeedback": "spacing stable at 1120u; action AI MOVE; improve: keep pressure without wasting arrows.",
                    "targetVisible": True,
                    "targetAbove": True,
                    "targetInShootRange": False,
                    "verticalDistance": 340.0,
                    "projectileThreatActive": False,
                    "targetRangedThreatActive": False,
                    "targetUltimateThreatActive": False,
                    "selfArrows": 3,
                    "roundResetPending": False,
                    "reportedInput": {
                        "frame": 42,
                        "axis": -1.0,
                        "aim": {"x": -1.0, "y": 0.0},
                        "jumpHeld": True,
                    },
                },
            }
        )

        report = session.report_payload()

        self.assertEqual("AI ANTI AIR CHASE", report["summary"])
        self.assertEqual(
            "anti-air chase active now; action AI JUMP; improve: climb into range before spending arrows.",
            report["botFeedback"],
        )

    def test_report_payload_preserves_stalled_anti_air_chase_feedback(self) -> None:
        session = codex_broker.AgentDrivenSession(
            2,
            "agent-session",
            {
                "frame": 1,
                "self": {"botId": "bot-slot-2"},
                "target": {"slotId": 1},
                "arena": {"horizontalDistance": 1460.0},
            },
        )

        session.publish_action(
            {
                "mode": "pressure",
                "reason": "heuristic_anti_air",
                "antiAir": False,
            }
        )
        session.publish_state(
            {
                "frame": 2,
                "executorFeedback": {
                    "source": "codex",
                    "summary": "AI ANTI AIR STALLED",
                    "botFeedback": "anti-air chase stalled; action grounded advance; improve: hold jump or aim upward while closing vertical distance.",
                    "targetVisible": True,
                    "targetAbove": True,
                    "targetInShootRange": False,
                    "verticalDistance": 340.0,
                    "projectileThreatActive": False,
                    "targetRangedThreatActive": False,
                    "targetUltimateThreatActive": False,
                    "selfArrows": 3,
                    "roundResetPending": False,
                },
            }
        )

        report = session.report_payload()

        self.assertEqual("AI ANTI AIR STALLED", report["summary"])
        self.assertEqual(
            "anti-air chase stalled; action grounded advance; improve: hold jump or aim upward while closing vertical distance.",
            report["botFeedback"],
        )

    def test_report_payload_marks_stalled_anti_air_chase_from_reported_input(self) -> None:
        session = codex_broker.AgentDrivenSession(
            2,
            "agent-session",
            {
                "frame": 1,
                "self": {"botId": "bot-slot-2"},
                "target": {"slotId": 1},
                "arena": {"horizontalDistance": 1158.0},
            },
        )

        session.publish_action(
            {
                "mode": "pressure",
                "reason": "heuristic_anti_air",
                "antiAir": False,
            }
        )
        session.publish_state(
            {
                "frame": 15,
                "executorFeedback": {
                    "source": "codex",
                    "summary": "AI ANTI AIR CHASE",
                    "botFeedback": "anti-air chase active now; action pending; improve: climb into range before spending arrows.",
                    "targetVisible": True,
                    "targetAbove": True,
                    "targetInShootRange": False,
                    "verticalDistance": 218.0,
                    "projectileThreatActive": False,
                    "targetRangedThreatActive": False,
                    "targetUltimateThreatActive": False,
                    "selfArrows": 3,
                    "roundResetPending": False,
                    "reportedInput": {
                        "axis": -0.27,
                        "aim": {"x": -0.84, "y": 0.53},
                        "jumpPressed": False,
                        "jumpHeld": False,
                        "shootPressed": False,
                        "shootHeld": False,
                    },
                },
            }
        )

        report = session.report_payload()

        self.assertEqual("AI ANTI AIR STALLED", report["summary"])
        self.assertEqual(
            "anti-air chase stalled; action grounded advance; improve: hold jump or aim upward while closing vertical distance.",
            report["botFeedback"],
        )

    def test_report_payload_keeps_move_for_slight_vertical_anti_air_intent(self) -> None:
        session = codex_broker.AgentDrivenSession(
            2,
            "agent-session",
            {
                "frame": 1,
                "self": {"botId": "bot-slot-2"},
                "target": {"slotId": 1},
                "arena": {"horizontalDistance": 1460.0},
            },
        )

        session.publish_action(
            {
                "mode": "pressure",
                "reason": "heuristic_anti_air",
                "antiAir": False,
            }
        )
        session.publish_state(
            {
                "frame": 2,
                "executorFeedback": {
                    "source": "codex",
                    "summary": "AI MOVE",
                    "botFeedback": "spacing stable at 1460u; action AI MOVE; improve: keep pressure without wasting arrows.",
                    "targetVisible": True,
                    "targetAbove": True,
                    "targetInShootRange": False,
                    "shouldAntiAir": False,
                    "verticalDistance": 44.0,
                    "projectileThreatActive": False,
                    "targetRangedThreatActive": False,
                    "targetUltimateThreatActive": False,
                    "selfArrows": 3,
                    "roundResetPending": False,
                },
            }
        )

        report = session.report_payload()

        self.assertEqual("AI MOVE", report["summary"])
        self.assertEqual(
            "spacing stable at 1460u; action AI MOVE; improve: keep pressure without wasting arrows.",
            report["botFeedback"],
        )

    def test_report_payload_normalizes_current_anti_air_shot_from_semantics(self) -> None:
        session = codex_broker.AgentDrivenSession(
            2,
            "agent-session",
            {
                "frame": 1,
                "self": {"botId": "bot-slot-2"},
                "target": {"slotId": 1},
                "arena": {"horizontalDistance": 520.0},
            },
        )

        session.publish_action(
            {
                "mode": "pressure",
                "reason": "target_ranged_threat",
                "antiAir": False,
            }
        )
        session.publish_state(
            {
                "frame": 2,
                "executorFeedback": {
                    "source": "codex",
                    "summary": "AI DEFENSIVE RETREAT",
                    "botFeedback": "enemy ranged active; action AI DEFENSIVE RETREAT; improve: clear the arrow line before committing.",
                    "targetVisible": True,
                    "targetAbove": True,
                    "targetInShootRange": True,
                    "shouldAntiAir": True,
                    "projectileThreatActive": False,
                    "targetRangedThreatActive": False,
                    "targetUltimateThreatActive": False,
                    "selfArrows": 2,
                    "roundResetPending": False,
                    "reportedInput": {
                        "frame": 42,
                        "axis": 0.0,
                        "aim": {"x": 0.0, "y": 1.0},
                        "shootPressed": True,
                    },
                },
            }
        )

        report = session.report_payload()

        self.assertEqual("AI ANTI AIR", report["summary"])
        self.assertEqual(
            "anti-air shot active now; action AI SHOOT; improve: take the vertical shot before repositioning.",
            report["botFeedback"],
        )

    def test_report_payload_normalizes_current_last_arrow_pressure_from_semantics(self) -> None:
        session = codex_broker.AgentDrivenSession(
            2,
            "agent-session",
            {
                "frame": 1,
                "self": {"botId": "bot-slot-2"},
                "target": {"slotId": 1},
                "arena": {"horizontalDistance": 850.0},
            },
        )

        session.publish_action(
            {
                "mode": "retreat",
                "reason": "target_ranged_threat",
            }
        )
        session.publish_state(
            {
                "frame": 2,
                "executorFeedback": {
                    "source": "codex",
                    "summary": "AI DEFENSIVE RETREAT",
                    "botFeedback": "enemy ranged active; action AI DEFENSIVE RETREAT; improve: clear the arrow line before committing.",
                    "targetVisible": True,
                    "targetInShootRange": True,
                    "projectileThreatActive": False,
                    "targetRangedThreatActive": False,
                    "targetMeleeThreatActive": False,
                    "targetUltimateThreatActive": False,
                    "selfCornered": False,
                    "selfArrows": 3,
                    "targetArrows": 0,
                    "roundResetPending": False,
                    "reportedInput": {
                        "frame": 109,
                        "axis": 0.94,
                        "aim": {"x": -1.0, "y": 0.0},
                        "shootPressed": True,
                    },
                },
            }
        )

        report = session.report_payload()

        self.assertEqual("AI LAST ARROW PRESSURE", report["summary"])
        self.assertEqual(
            "last-arrow pressure active now; action AI SHOOT; improve: spend the ammo advantage before the target recovers arrows.",
            report["botFeedback"],
        )

    def test_report_payload_marks_stalled_last_arrow_pressure_when_input_has_no_action(self) -> None:
        session = codex_broker.AgentDrivenSession(
            2,
            "agent-session",
            {
                "frame": 1,
                "self": {"botId": "bot-slot-2"},
                "target": {"slotId": 1},
                "arena": {"horizontalDistance": 564.0},
            },
        )

        session.publish_action(
            {
                "mode": "pressure",
                "reason": "last_arrow_pressure",
            }
        )
        session.publish_state(
            {
                "frame": 2,
                "executorFeedback": {
                    "source": "codex",
                    "summary": "AI LAST ARROW PRESSURE",
                    "botFeedback": "last-arrow pressure active now; action pending; improve: spend the ammo advantage before the target recovers arrows.",
                    "targetVisible": True,
                    "targetInShootRange": True,
                    "projectileThreatActive": False,
                    "targetRangedThreatActive": False,
                    "targetMeleeThreatActive": False,
                    "targetUltimateThreatActive": False,
                    "selfCornered": False,
                    "selfArrows": 1,
                    "targetArrows": 0,
                    "roundResetPending": False,
                    "reportedInput": {
                        "frame": 8834,
                        "axis": 0.0,
                        "aim": {"x": -1.0, "y": 0.0},
                        "jumpPressed": False,
                        "jumpHeld": False,
                        "shootPressed": False,
                        "shootHeld": False,
                        "meleePressed": False,
                        "ultimatePressed": False,
                        "dashPrimaryPressed": False,
                        "dashSecondaryPressed": False,
                    },
                },
            }
        )

        report = session.report_payload()

        self.assertEqual("AI LAST ARROW STALLED", report["summary"])
        self.assertEqual(
            "last-arrow pressure stalled; action none; improve: shoot, dash in, or move into a clean shot before the target recovers arrows.",
            report["botFeedback"],
        )

    def test_report_payload_keeps_projectile_summary_ahead_of_ranged_threat(self) -> None:
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
                    "summary": "AI PROJECTILE DRIFT",
                    "botFeedback": "projectile threat 0.10s; action AI PROJECTILE DRIFT; improve: defend before attacking.",
                    "targetVisible": True,
                    "projectileThreatActive": True,
                    "targetRangedThreatActive": True,
                    "roundResetPending": False,
                },
            }
        )

        report = session.report_payload()

        self.assertEqual("AI PROJECTILE DRIFT", report["summary"])
        self.assertTrue(report["projectileThreatActive"])
        self.assertTrue(report["targetRangedThreatActive"])

    def test_agent_session_start_feedback_is_available_to_agent_payload(self) -> None:
        session = codex_broker.AgentDrivenSession(
            2,
            "agent-session",
            {
                "frame": 1,
                "self": {"botId": "bot-slot-2"},
                "target": {"slotId": 1},
                "arena": {"horizontalDistance": 320.0},
            },
            {
                "targetVisible": False,
                "roundResetPending": True,
                "botFeedback": "waiting for arena snapshot; improve: verify bot observation setup.",
            },
        )

        payload = session.state_payload()
        report = session.report_payload()

        self.assertFalse(payload["executorFeedback"]["targetVisible"])
        self.assertTrue(payload["executorFeedback"]["roundResetPending"])
        self.assertEqual(
            "waiting for arena snapshot; improve: verify bot observation setup.",
            payload["executorFeedback"]["botFeedback"],
        )
        self.assertFalse(report["targetVisible"])
        self.assertTrue(report["roundResetPending"])

    def test_agent_session_reuse_replaces_initial_executor_feedback(self) -> None:
        session = codex_broker.AgentDrivenSession(
            2,
            "agent-session",
            {
                "frame": 1,
                "self": {"botId": "bot-slot-2"},
                "target": {"slotId": 1},
                "arena": {"horizontalDistance": 320.0},
            },
            {
                "targetVisible": True,
                "roundResetPending": False,
            },
        )

        session.refresh_start(
            {
                "frame": 2,
                "self": {"botId": "bot-slot-2"},
                "target": {"slotId": 1},
                "arena": {"horizontalDistance": 320.0},
            },
            {
                "targetVisible": False,
                "roundResetPending": True,
            },
        )

        payload = session.state_payload()

        self.assertFalse(payload["executorFeedback"]["targetVisible"])
        self.assertTrue(payload["executorFeedback"]["roundResetPending"])

    def test_agent_session_reuse_without_feedback_keeps_previous_executor_feedback(self) -> None:
        session = codex_broker.AgentDrivenSession(
            2,
            "agent-session",
            {
                "frame": 1,
                "self": {"botId": "bot-slot-2"},
                "target": {"slotId": 1},
                "arena": {"horizontalDistance": 320.0},
            },
            {
                "targetVisible": False,
                "roundResetPending": True,
            },
        )

        session.refresh_start(
            {
                "frame": 2,
                "self": {"botId": "bot-slot-2"},
                "target": {"slotId": 1},
                "arena": {"horizontalDistance": 320.0},
            }
        )

        payload = session.state_payload()

        self.assertFalse(payload["executorFeedback"]["targetVisible"])
        self.assertTrue(payload["executorFeedback"]["roundResetPending"])


if __name__ == "__main__":
    unittest.main()
