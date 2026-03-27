using System;

namespace ProjectPVP.AI
{
    [Serializable]
    public sealed class AiAgentRequestEnvelope
    {
        public string protocolVersion = "ai-arena-v1";
        public string source = "unity";
        public long sentAtUnixMs;
        public AiCombatSnapshot snapshot;
    }

    [Serializable]
    public sealed class AiAgentResponseEnvelope
    {
        public string protocolVersion = "ai-arena-v1";
        public int targetFrame;
        public string debugText = string.Empty;
        public AiFrameAction action = new AiFrameAction();
    }

    [Serializable]
    public sealed class AiFrameAction
    {
        public float axis;
        public float aimX;
        public float aimY;
        public bool left;
        public bool right;
        public bool up;
        public bool down;
        public bool jumpPressed;
        public bool jumpHeld;
        public bool shootPressed;
        public bool shootHeld;
        public bool meleePressed;
        public bool ultimatePressed;
        public bool dashPrimaryPressed;
        public bool dashSecondaryPressed;
    }

    [Serializable]
    public sealed class AiCombatSnapshot
    {
        public string protocolVersion = "ai-arena-v1";
        public string matchId = string.Empty;
        public int roundId;
        public int simulationFrame;
        public float fixedDeltaTime;
        public bool roundResetPending;
        public int playerOneWins;
        public int playerTwoWins;
        public AiCombatantSnapshot self;
        public AiCombatantSnapshot opponent;
        public AiProjectileSnapshot[] projectiles = Array.Empty<AiProjectileSnapshot>();
        public AiArenaStateSnapshot arena = new AiArenaStateSnapshot();
        public AiCombatFeatureSnapshot features = new AiCombatFeatureSnapshot();
        public string[] recentEvents = Array.Empty<string>();
    }

    [Serializable]
    public sealed class AiCombatantSnapshot
    {
        public int slotIndex;
        public string slotName = string.Empty;
        public string characterName = string.Empty;
        public float positionX;
        public float positionY;
        public float velocityX;
        public float velocityY;
        public int facing;
        public bool isGrounded;
        public bool isTouchingWall;
        public bool isDead;
        public bool isDashing;
        public bool isMeleeActive;
        public bool isUltimateActive;
        public bool isHitStunned;
        public bool isKnockedBack;
        public bool isAimHoldActive;
        public int currentArrows;
        public float aimHoldX;
        public float aimHoldY;
        public float inputAxis;
        public float inputAimX;
        public float inputAimY;
        public float dashParryTimeLeft;
        public float dashPressTimeLeft;
        public float dashPrimaryCooldownLeft;
        public float dashSecondaryCooldownLeft;
        public float shootCooldownLeft;
        public float meleeCooldownLeft;
        public float ultimateCooldownLeft;
        public float meleeTimeLeft;
        public float ultimateTimeLeft;
        public float hitStunTimeLeft;
        public float knockbackTimeLeft;
        public float meleeRangeWidth;
        public float meleeRangeHeight;
        public float ultimateRadius;
        public string actionKey = string.Empty;
    }

    [Serializable]
    public sealed class AiProjectileSnapshot
    {
        public float positionX;
        public float positionY;
        public float velocityX;
        public float velocityY;
        public int ownerSlotIndex;
        public bool isStuck;
        public bool isCollectible;
        public bool isDisarmed;
        public float distanceToSelf;
        public float horizontalDistanceToSelf;
        public float verticalDistanceToSelf;
    }

    [Serializable]
    public sealed class AiArenaStateSnapshot
    {
        public float minX;
        public float minY;
        public float width;
        public float height;
    }

    [Serializable]
    public sealed class AiCombatFeatureSnapshot
    {
        public float horizontalDistance;
        public float verticalDistance;
        public float euclideanDistance;
        public bool opponentAbove;
        public bool meleeRangeNow;
        public bool shootLaneOpen;
        public bool hostileProjectileThreat;
        public int hostileProjectileCount;
        public float nearestHostileProjectileDistance;
    }
}
