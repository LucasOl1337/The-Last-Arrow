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
            if (snapshot == null || snapshot.semantics == null)
            {
                return "waiting for arena snapshot; improve: verify bot observation setup.";
            }

            AiArenaSemanticObservation semantics = snapshot.semantics;
            AiArenaCombatantObservation self = snapshot.self ?? new AiArenaCombatantObservation();
            string action = ResolveAction(decision);

            if (!semantics.hasTarget)
            {
                return "no target visible; improve: verify spawn, camera, or opponent tracking.";
            }

            if (semantics.incomingProjectileThreat)
            {
                string time = semantics.incomingProjectileTime >= 0f
                    ? semantics.incomingProjectileTime.ToString("0.00") + "s"
                    : "now";
                if (!IsProjectileDefenseDecision(decision))
                {
                    return "missed projectile defense " + time + "; action " + action + "; improve: dash, jump, parry, or block before attacking.";
                }

                return "projectile threat " + time + "; action " + action + "; improve: defend before attacking.";
            }

            if (semantics.targetUsingUltimate)
            {
                return "enemy ultimate active; action " + action + "; improve: clear danger before pickups or trades.";
            }

            if (semantics.shouldCollectProjectile || self.arrows <= 0)
            {
                string distance = semantics.collectibleProjectileDistance >= 0f
                    ? semantics.collectibleProjectileDistance.ToString("0") + "u"
                    : "unknown distance";
                return "recover arrow at " + distance + "; action " + action + "; improve: recover ammo before forcing trades.";
            }

            if (semantics.selfCornered)
            {
                return "corner pressure detected; action " + action + "; improve: escape corner before committing.";
            }

            if (semantics.targetVulnerable || semantics.shouldPunish)
            {
                if (!IsAttackDecision(decision))
                {
                    return "missed punish window; action " + action + "; improve: fire, melee, or ultimate before target recovers.";
                }

                return "punish window available; action " + action + "; improve: convert vulnerability quickly.";
            }

            return "spacing stable at " + semantics.horizontalDistance.ToString("0") + "u; action " + action + "; improve: keep pressure without wasting arrows.";
        }

        private static bool IsAttackDecision(AiArenaDecisionEnvelope decision)
        {
            if (decision == null)
            {
                return false;
            }

            if (decision.shootPressed || decision.shootHeld || decision.meleePressed || decision.ultimatePressed)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(decision.debugSummary))
            {
                return false;
            }

            return decision.debugSummary.IndexOf("shoot", StringComparison.OrdinalIgnoreCase) >= 0
                || decision.debugSummary.IndexOf("fire", StringComparison.OrdinalIgnoreCase) >= 0
                || decision.debugSummary.IndexOf("melee", StringComparison.OrdinalIgnoreCase) >= 0
                || decision.debugSummary.IndexOf("ultimate", StringComparison.OrdinalIgnoreCase) >= 0
                || decision.debugSummary.IndexOf("attack", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsProjectileDefenseDecision(AiArenaDecisionEnvelope decision)
        {
            if (decision == null)
            {
                return false;
            }

            if (decision.jumpPressed || decision.jumpHeld || decision.dashPrimaryPressed || decision.dashSecondaryPressed)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(decision.debugSummary))
            {
                return false;
            }

            return decision.debugSummary.IndexOf("parry", StringComparison.OrdinalIgnoreCase) >= 0
                || decision.debugSummary.IndexOf("block", StringComparison.OrdinalIgnoreCase) >= 0
                || decision.debugSummary.IndexOf("evade", StringComparison.OrdinalIgnoreCase) >= 0
                || decision.debugSummary.IndexOf("dodge", StringComparison.OrdinalIgnoreCase) >= 0
                || decision.debugSummary.IndexOf("dash", StringComparison.OrdinalIgnoreCase) >= 0
                || decision.debugSummary.IndexOf("jump", StringComparison.OrdinalIgnoreCase) >= 0;
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
