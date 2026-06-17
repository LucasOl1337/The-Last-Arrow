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
}
