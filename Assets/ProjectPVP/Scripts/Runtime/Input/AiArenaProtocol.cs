using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectPVP.Input
{
    [Serializable]
    public sealed class AiArenaSnapshotEnvelope
    {
        public const int CurrentSchemaVersion = 2;

        public int schemaVersion = CurrentSchemaVersion;
        public string transport = "local_json";
        public int frame;
        public int selfSlotId;
        public AiArenaArenaObservation arena = new AiArenaArenaObservation();
        public AiArenaCombatantObservation self = new AiArenaCombatantObservation();
        public List<AiArenaCombatantObservation> opponents = new List<AiArenaCombatantObservation>();
        public List<AiArenaProjectileObservation> projectiles = new List<AiArenaProjectileObservation>();
        public AiArenaSemanticObservation semantics = new AiArenaSemanticObservation();
    }

    [Serializable]
    public sealed class AiArenaArenaObservation
    {
        public bool roundResetPending;
        public int roundsToChampion;
        public int playerOneWins;
        public int playerTwoWins;
        public int currentRespawnSeedIndex;
        public string currentRespawnSeedLabel = string.Empty;
        public int pendingRoundWinnerSlot;
        public int pendingChampionSlot;
        public int championAnnouncementSlot;
        public float wrapXMin;
        public float wrapXMax;
        public float wrapYMin;
        public float wrapYMax;
    }

    [Serializable]
    public sealed class AiArenaCombatantObservation
    {
        public int slotId;
        public string botId = string.Empty;
        public string botDisplayName = string.Empty;
        public string characterId = string.Empty;
        public string displayName = string.Empty;
        public string actionKey = string.Empty;
        public bool isDead;
        public bool isGrounded;
        public bool isTouchingWall;
        public bool isDashing;
        public bool isMeleeActive;
        public bool isShootAnimating;
        public bool isUltimateActive;
        public bool isHitStunned;
        public bool canParryProjectile;
        public bool canBlockProjectiles;
        public int facing = 1;
        public int arrows;
        public float projectileInheritVelocityFactor = 1f;
        public float projectileBaseSpeed = 1600f;
        public float projectileGravity = 1500f;
        public float shootCooldownLeft;
        public float meleeCooldownLeft;
        public float dashCooldownLeft;
        public float ultimateCooldownLeft;
        public float hitStunTimeLeft;
        public Vector2 position = Vector2.zero;
        public Vector2 velocity = Vector2.zero;
        public Vector2 meleeHitboxCenter = Vector2.zero;
        public Vector2 meleeHitboxSize = Vector2.zero;
        public Vector2 ultimateHitboxCenter = Vector2.zero;
        public float ultimateHitboxRadius;
    }

    [Serializable]
    public sealed class AiArenaProjectileObservation
    {
        public int sourceSlotId;
        public bool isStuck;
        public bool isDisarmed;
        public bool isCollectible;
        public Vector2 position = Vector2.zero;
        public Vector2 velocity = Vector2.zero;
        public Vector2 travelDirection = Vector2.right;
    }

    [Serializable]
    public sealed class AiArenaSemanticObservation
    {
        public bool hasTarget;
        public int targetSlotId;
        public float horizontalDistance;
        public float verticalDistance;
        public Vector2 targetDirection = Vector2.right;
        public bool targetAbove;
        public bool targetBelow;
        public bool targetInMeleeRange;
        public bool targetInUltimateRange;
        public bool targetInShootRange;
        public bool selfHasArrows;
        public bool shouldAdvance;
        public bool shouldRetreat;
        public bool shouldPressure;
        public bool shouldZone;
        public bool shouldPunish;
        public bool shouldAntiAir;
        public bool targetVulnerable;
        public bool targetPressuring;
        public bool targetUsingRanged;
        public bool targetUsingMelee;
        public bool targetUsingUltimate;
        public bool selfCornered;
        public bool targetCornered;
        public bool incomingProjectileThreat;
        public bool shouldJumpEvade;
        public bool shouldDashEvade;
        public bool hasCollectibleProjectile;
        public bool shouldCollectProjectile;
        public float collectibleProjectileDistance = -1f;
        public Vector2 collectibleProjectileDirection = Vector2.zero;
        public float incomingProjectileTime = -1f;
        public Vector2 incomingProjectileDirection = Vector2.zero;
        public Vector2 predictedTargetDirection = Vector2.right;
    }

    [Serializable]
    public sealed class AiArenaDecisionEnvelope
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public string status = "ok";
        public string debugSummary = string.Empty;
        public float moveAxis;
        public float aimX = 1f;
        public float aimY;
        public bool jumpPressed;
        public bool jumpHeld;
        public bool shootPressed;
        public bool shootHeld;
        public bool meleePressed;
        public bool ultimatePressed;
        public bool dashPrimaryPressed;
        public bool dashSecondaryPressed;
    }

    internal static class AiArenaBotFeedbackBuilder
    {
        internal static string Build(AiArenaSnapshotEnvelope snapshot, AiArenaDecisionEnvelope decision)
        {
            return Build(snapshot, decision, trustDebugSummaryForAction: true);
        }

        internal static string Build(AiArenaSnapshotEnvelope snapshot, string actionSummary, PlayerInputFrame executedFrame)
        {
            return Build(snapshot, BuildExecutedDecision(actionSummary, executedFrame), trustDebugSummaryForAction: false);
        }

        internal static string Build(AiArenaSnapshotEnvelope snapshot, string actionSummary, CodexReportedInputFrame executedFrame)
        {
            return Build(snapshot, BuildExecutedDecision(actionSummary, executedFrame), trustDebugSummaryForAction: false);
        }

        private static string Build(AiArenaSnapshotEnvelope snapshot, AiArenaDecisionEnvelope decision, bool trustDebugSummaryForAction)
        {
            if (snapshot == null || snapshot.semantics == null)
            {
                return "waiting for arena snapshot; improve: verify bot observation setup.";
            }

            AiArenaSemanticObservation semantics = snapshot.semantics;
            AiArenaCombatantObservation self = snapshot.self ?? new AiArenaCombatantObservation();
            bool isOutOfArrows = snapshot.self != null && self.arrows <= 0;
            string action = ResolveAction(decision);
            bool shotDecision = IsShotDecision(decision);

            if (!semantics.hasTarget)
            {
                return "no target visible; improve: verify spawn, camera, or opponent tracking.";
            }

            if (semantics.incomingProjectileThreat)
            {
                string time = semantics.incomingProjectileTime >= 0f
                    ? semantics.incomingProjectileTime.ToString("0.00") + "s"
                    : "now";
                if (!IsProjectileDefenseDecision(decision, trustDebugSummaryForAction))
                {
                    return "missed projectile defense " + time + "; action " + action + "; improve: dash, jump, parry, or block before attacking.";
                }

                return "projectile threat " + time + "; action " + action + "; improve: defend before attacking.";
            }

            if (semantics.targetUsingUltimate)
            {
                if (!IsUltimateEscapeDecision(semantics, decision, trustDebugSummaryForAction))
                {
                    return "missed ultimate escape; action " + action + "; improve: dash or move away before pickups or trades.";
                }

                return "enemy ultimate active; action " + action + "; improve: clear danger before pickups or trades.";
            }

            if (semantics.targetUsingMelee)
            {
                if (!IsMeleeEscapeDecision(semantics, decision, trustDebugSummaryForAction))
                {
                    return "missed melee escape; action " + action + "; improve: dash or move away before trading into active melee.";
                }

                return "enemy melee active; action " + action + "; improve: reset spacing before punishing.";
            }

            if (semantics.targetUsingRanged)
            {
                if (!IsRangedPressureDecision(semantics, decision, trustDebugSummaryForAction))
                {
                    return "missed ranged response; action " + action + "; improve: dodge, break line, or interrupt before chasing pickups.";
                }

                return "enemy ranged active; action " + action + "; improve: clear the arrow line before committing.";
            }

            if (ShouldPrioritizeCornerEscapeFeedback(semantics, isOutOfArrows))
            {
                if (!IsCornerEscapeDecision(semantics, decision, trustDebugSummaryForAction))
                {
                    string advice = AiArenaHeuristicPolicy.ShouldDeferCollectionForCornerEscape(semantics)
                        ? "move toward center before chasing wall-side arrows."
                        : "move toward center before committing.";
                    return "missed corner escape; action " + action + "; improve: " + advice;
                }

                return "corner pressure detected; action " + action + "; improve: escape corner before committing.";
            }

            if (shotDecision && isOutOfArrows)
            {
                return "shot attempted without arrows; action " + action + "; improve: recover arrow before shooting.";
            }

            if (shotDecision && !semantics.targetInShootRange)
            {
                return "shot attempted out of range at " + semantics.horizontalDistance.ToString("0") + "u; action " + action
                    + "; improve: close distance, aim a valid line, or hold fire.";
            }

            if (semantics.shouldCollectProjectile || isOutOfArrows)
            {
                string distance = semantics.collectibleProjectileDistance >= 0f
                    ? semantics.collectibleProjectileDistance.ToString("0") + "u"
                    : "unknown distance";
                if (semantics.shouldCollectProjectile && IsKnownProjectileRecoveryDirection(semantics) && !IsProjectileRecoveryDecision(semantics, decision))
                {
                    return "missed arrow recovery at " + distance + "; action " + action + "; improve: move toward pickup before forcing trades.";
                }

                return "recover arrow at " + distance + "; action " + action + "; improve: recover ammo before forcing trades.";
            }

            if (semantics.selfCornered)
            {
                if (!IsCornerEscapeDecision(semantics, decision, trustDebugSummaryForAction))
                {
                    return "missed corner escape; action " + action + "; improve: move toward center before committing.";
                }

                return "corner pressure detected; action " + action + "; improve: escape corner before committing.";
            }

            if (semantics.shouldAntiAir)
            {
                if (!IsAntiAirDecision(decision, trustDebugSummaryForAction))
                {
                    return "missed anti-air; action " + action + "; improve: shoot, jump, or aim upward before the target lands.";
                }

                return "anti-air opportunity; action " + action + "; improve: challenge vertical approaches before landing.";
            }

            if (semantics.shouldPunish)
            {
                if (!IsAttackDecision(decision, trustDebugSummaryForAction))
                {
                    return "missed punish window; action " + action + "; improve: fire, melee, or ultimate before target recovers.";
                }

                return "punish window available; action " + action + "; improve: convert vulnerability quickly.";
            }

            if (semantics.targetVulnerable)
            {
                return "vulnerable target out of range at " + semantics.horizontalDistance.ToString("0") + "u; action " + action
                    + "; improve: close distance before spending attacks.";
            }

            return "spacing stable at " + semantics.horizontalDistance.ToString("0") + "u; action " + action + "; improve: keep pressure without wasting arrows.";
        }

        private static bool IsAttackDecision(AiArenaDecisionEnvelope decision, bool trustDebugSummaryForAction)
        {
            if (decision == null)
            {
                return false;
            }

            if (decision.shootPressed || decision.shootHeld || decision.meleePressed || decision.ultimatePressed)
            {
                return true;
            }

            if (!trustDebugSummaryForAction || string.IsNullOrWhiteSpace(decision.debugSummary))
            {
                return false;
            }

            return decision.debugSummary.IndexOf("shoot", StringComparison.OrdinalIgnoreCase) >= 0
                || decision.debugSummary.IndexOf("fire", StringComparison.OrdinalIgnoreCase) >= 0
                || decision.debugSummary.IndexOf("melee", StringComparison.OrdinalIgnoreCase) >= 0
                || decision.debugSummary.IndexOf("ultimate", StringComparison.OrdinalIgnoreCase) >= 0
                || decision.debugSummary.IndexOf("attack", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsShotDecision(AiArenaDecisionEnvelope decision)
        {
            if (decision == null)
            {
                return false;
            }

            return decision.shootPressed || decision.shootHeld;
        }

        private static bool IsProjectileDefenseDecision(AiArenaDecisionEnvelope decision, bool trustDebugSummaryForAction)
        {
            if (decision == null)
            {
                return false;
            }

            if (decision.jumpPressed || decision.jumpHeld || decision.dashPrimaryPressed || decision.dashSecondaryPressed)
            {
                return true;
            }

            if (!trustDebugSummaryForAction)
            {
                return IsExecutedSummaryProjectileDefense(decision);
            }

            if (string.IsNullOrWhiteSpace(decision.debugSummary))
            {
                return false;
            }

            return decision.debugSummary.IndexOf("parry", StringComparison.OrdinalIgnoreCase) >= 0
                || decision.debugSummary.IndexOf("block", StringComparison.OrdinalIgnoreCase) >= 0
                || decision.debugSummary.IndexOf("evade", StringComparison.OrdinalIgnoreCase) >= 0
                || decision.debugSummary.IndexOf("dodge", StringComparison.OrdinalIgnoreCase) >= 0
                || decision.debugSummary.IndexOf("projectile drift", StringComparison.OrdinalIgnoreCase) >= 0
                || decision.debugSummary.IndexOf("dash", StringComparison.OrdinalIgnoreCase) >= 0
                || decision.debugSummary.IndexOf("jump", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsExecutedSummaryProjectileDefense(AiArenaDecisionEnvelope decision)
        {
            if (decision == null || string.IsNullOrWhiteSpace(decision.debugSummary))
            {
                return false;
            }

            string summary = decision.debugSummary;
            bool hasHorizontalMovement = Mathf.Abs(decision.moveAxis) > 0.1f;
            if (summary.IndexOf("projectile drift", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return hasHorizontalMovement;
            }

            if (summary.IndexOf("parry hold", StringComparison.OrdinalIgnoreCase) >= 0
                || summary.IndexOf("projectile block", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            if (hasHorizontalMovement
                && (summary.IndexOf("evade", StringComparison.OrdinalIgnoreCase) >= 0
                    || summary.IndexOf("dodge", StringComparison.OrdinalIgnoreCase) >= 0
                    || summary.IndexOf("escape", StringComparison.OrdinalIgnoreCase) >= 0
                    || summary.IndexOf("retreat", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return true;
            }

            return false;
        }

        private static bool IsAntiAirDecision(AiArenaDecisionEnvelope decision, bool trustDebugSummaryForAction)
        {
            if (decision == null)
            {
                return false;
            }

            if (decision.jumpPressed || decision.jumpHeld || IsAttackDecision(decision, trustDebugSummaryForAction) || decision.aimY > 0.35f)
            {
                return true;
            }

            if (!trustDebugSummaryForAction || string.IsNullOrWhiteSpace(decision.debugSummary))
            {
                return false;
            }

            return decision.debugSummary.IndexOf("anti air", StringComparison.OrdinalIgnoreCase) >= 0
                || decision.debugSummary.IndexOf("anti-air", StringComparison.OrdinalIgnoreCase) >= 0
                || decision.debugSummary.IndexOf("climb", StringComparison.OrdinalIgnoreCase) >= 0
                || decision.debugSummary.IndexOf("above", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsUltimateEscapeDecision(AiArenaSemanticObservation semantics, AiArenaDecisionEnvelope decision, bool trustDebugSummaryForAction)
        {
            return IsNonAttackingEscapeDecision(semantics, decision, trustDebugSummaryForAction);
        }

        private static bool IsMeleeEscapeDecision(AiArenaSemanticObservation semantics, AiArenaDecisionEnvelope decision, bool trustDebugSummaryForAction)
        {
            return IsNonAttackingEscapeDecision(semantics, decision, trustDebugSummaryForAction);
        }

        private static bool IsRangedPressureDecision(AiArenaSemanticObservation semantics, AiArenaDecisionEnvelope decision, bool trustDebugSummaryForAction)
        {
            return IsAttackDecision(decision, trustDebugSummaryForAction)
                || IsNonAttackingEscapeDecision(semantics, decision, trustDebugSummaryForAction);
        }

        private static bool IsCornerEscapeDecision(AiArenaSemanticObservation semantics, AiArenaDecisionEnvelope decision, bool trustDebugSummaryForAction)
        {
            if (semantics == null || decision == null)
            {
                return false;
            }

            if (IsAttackDecision(decision, trustDebugSummaryForAction))
            {
                return false;
            }

            if (trustDebugSummaryForAction
                && !string.IsNullOrWhiteSpace(decision.debugSummary)
                && (decision.debugSummary.IndexOf("corner escape", StringComparison.OrdinalIgnoreCase) >= 0
                    || decision.debugSummary.IndexOf("escape corner", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return true;
            }

            if (Mathf.Abs(semantics.targetDirection.x) > 0.1f && Mathf.Abs(decision.moveAxis) > 0.1f)
            {
                return Mathf.Sign(decision.moveAxis) == Mathf.Sign(semantics.targetDirection.x);
            }

            return false;
        }

        private static bool IsNonAttackingEscapeDecision(AiArenaSemanticObservation semantics, AiArenaDecisionEnvelope decision, bool trustDebugSummaryForAction)
        {
            if (semantics == null || decision == null)
            {
                return false;
            }

            if (IsAttackDecision(decision, trustDebugSummaryForAction))
            {
                return false;
            }

            if (decision.dashPrimaryPressed || decision.dashSecondaryPressed)
            {
                return true;
            }

            if (trustDebugSummaryForAction
                && !string.IsNullOrWhiteSpace(decision.debugSummary)
                && (decision.debugSummary.IndexOf("evade", StringComparison.OrdinalIgnoreCase) >= 0
                    || decision.debugSummary.IndexOf("dodge", StringComparison.OrdinalIgnoreCase) >= 0
                    || decision.debugSummary.IndexOf("escape", StringComparison.OrdinalIgnoreCase) >= 0
                    || decision.debugSummary.IndexOf("retreat", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return true;
            }

            if (Mathf.Abs(semantics.targetDirection.x) > 0.1f && Mathf.Abs(decision.moveAxis) > 0.1f)
            {
                return Mathf.Sign(decision.moveAxis) != Mathf.Sign(semantics.targetDirection.x);
            }

            return false;
        }

        private static AiArenaDecisionEnvelope BuildExecutedDecision(string actionSummary, PlayerInputFrame frame)
        {
            return new AiArenaDecisionEnvelope
            {
                debugSummary = actionSummary,
                moveAxis = frame.axis,
                aimX = frame.aim.x,
                aimY = frame.aim.y,
                jumpPressed = frame.jumpPressed,
                jumpHeld = frame.jumpHeld,
                shootPressed = frame.shootPressed,
                shootHeld = frame.shootHeld,
                meleePressed = frame.meleePressed,
                ultimatePressed = frame.ultimatePressed,
                dashPrimaryPressed = frame.dashPrimaryPressed,
                dashSecondaryPressed = frame.dashSecondaryPressed,
            };
        }

        private static AiArenaDecisionEnvelope BuildExecutedDecision(string actionSummary, CodexReportedInputFrame frame)
        {
            CodexReportedInputFrame resolvedFrame = frame != null ? frame : new CodexReportedInputFrame();
            return new AiArenaDecisionEnvelope
            {
                debugSummary = actionSummary,
                moveAxis = resolvedFrame.axis,
                aimX = resolvedFrame.aim.x,
                aimY = resolvedFrame.aim.y,
                jumpPressed = resolvedFrame.jumpPressed,
                jumpHeld = resolvedFrame.jumpHeld,
                shootPressed = resolvedFrame.shootPressed,
                shootHeld = resolvedFrame.shootHeld,
                meleePressed = resolvedFrame.meleePressed,
                ultimatePressed = resolvedFrame.ultimatePressed,
                dashPrimaryPressed = resolvedFrame.dashPrimaryPressed,
                dashSecondaryPressed = resolvedFrame.dashSecondaryPressed,
            };
        }

        private static bool ShouldPrioritizeCornerEscapeFeedback(AiArenaSemanticObservation semantics, bool isOutOfArrows)
        {
            if (semantics == null || !semantics.selfCornered)
            {
                return false;
            }

            if (AiArenaHeuristicPolicy.ShouldDeferCollectionForCornerEscape(semantics))
            {
                return true;
            }

            return isOutOfArrows && !IsKnownProjectileRecoveryDirection(semantics);
        }

        private static bool IsKnownProjectileRecoveryDirection(AiArenaSemanticObservation semantics)
        {
            return semantics != null && semantics.collectibleProjectileDirection.sqrMagnitude > 0.01f;
        }

        private static bool IsProjectileRecoveryDecision(AiArenaSemanticObservation semantics, AiArenaDecisionEnvelope decision)
        {
            if (semantics == null || decision == null)
            {
                return false;
            }

            Vector2 direction = semantics.collectibleProjectileDirection;
            if (Mathf.Abs(direction.x) > 0.1f)
            {
                return Mathf.Sign(decision.moveAxis) == Mathf.Sign(direction.x)
                    && Mathf.Abs(decision.moveAxis) > 0.1f;
            }

            if (direction.y > 0.1f)
            {
                return decision.jumpPressed || decision.jumpHeld;
            }

            if (direction.y < -0.1f)
            {
                return true;
            }

            return false;
        }

        private static string ResolveAction(AiArenaDecisionEnvelope decision)
        {
            if (decision == null || string.IsNullOrWhiteSpace(decision.debugSummary))
            {
                return "none";
            }

            return decision.debugSummary;
        }
    }
}
