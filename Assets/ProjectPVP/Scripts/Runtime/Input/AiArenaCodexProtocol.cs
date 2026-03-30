using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectPVP.Input
{
    [Serializable]
    public sealed class CodexBrokerIntentEnvelope
    {
        public string status = "ok";
        public string sessionId = string.Empty;
        public long generatedAtUnixMs;
        public int generatedAtFrame = -1;
        public bool isFresh;
        public bool hasAgentAction;
        public string controllerOwner = string.Empty;
        public CodexStrategyIntent intent = new CodexStrategyIntent();
        public string error = string.Empty;
    }

    [Serializable]
    public sealed class CodexStrategyIntent
    {
        public string mode = "stabilize";
        public int preferredRange = 320;
        public float advanceBias = 0.5f;
        public float shootBias = 0.5f;
        public float meleeBias = 0.5f;
        public float dashBias = 0.5f;
        public float jumpBias = 0.5f;
        public string antiProjectile = "hold";
        public bool antiAir;
        public bool punishRecovery = true;
        public float cornerEscapeBias = 0.5f;
        public int focusTargetSlot = 2;
        public int expiresInMs = 400;
        public string reason = string.Empty;
    }

    [Serializable]
    public sealed class CodexBrokerSessionStartRequest
    {
        public int slotId;
        public CodexPromptState promptState = new CodexPromptState();
    }

    [Serializable]
    public sealed class CodexBrokerStrategyTickRequest
    {
        public string sessionId = string.Empty;
        public int slotId;
        public int frame;
        public bool forceRefresh;
        public CodexPromptState promptState = new CodexPromptState();
        public CodexExecutorFeedback executorFeedback = new CodexExecutorFeedback();
    }

    [Serializable]
    public sealed class CodexBrokerSessionResetRequest
    {
        public string sessionId = string.Empty;
        public int slotId;
        public string reason = "round_reset";
    }

    [Serializable]
    public sealed class CodexBrokerSessionStopRequest
    {
        public string sessionId = string.Empty;
        public int slotId;
    }

    [Serializable]
    public sealed class CodexAgentStateUpdateRequest
    {
        public string sessionId = string.Empty;
        public int slotId;
        public int frame;
        public bool forceRefresh;
        public CodexPromptState promptState = new CodexPromptState();
        public CodexExecutorFeedback executorFeedback = new CodexExecutorFeedback();
    }

    [Serializable]
    public sealed class CodexPromptState
    {
        public int frame;
        public string botId = string.Empty;
        public string botDisplayName = string.Empty;
        public string task = "win the round safely";
        public CodexPromptCombatant self = new CodexPromptCombatant();
        public CodexPromptCombatant target = new CodexPromptCombatant();
        public CodexPromptArena arena = new CodexPromptArena();
        public List<CodexPromptProjectileThreat> dangerousProjectiles = new List<CodexPromptProjectileThreat>();
        public List<string> events = new List<string>();
        public List<string> memory = new List<string>();
    }

    [Serializable]
    public sealed class CodexPromptCombatant
    {
        public int slotId;
        public string botId = string.Empty;
        public string botDisplayName = string.Empty;
        public string displayName = string.Empty;
        public string actionKey = string.Empty;
        public bool isDead;
        public bool isGrounded;
        public bool isDashing;
        public bool isMeleeActive;
        public bool isUltimateActive;
        public bool isHitStunned;
        public bool canParryProjectile;
        public bool canBlockProjectiles;
        public int arrows;
        public int facing = 1;
        public float shootCooldownLeft;
        public float meleeCooldownLeft;
        public float dashCooldownLeft;
        public float ultimateCooldownLeft;
        public float hitStunTimeLeft;
        public Vector2 position = Vector2.zero;
        public Vector2 velocity = Vector2.zero;
    }

    [Serializable]
    public sealed class CodexPromptArena
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
        public bool selfCornered;
        public bool targetCornered;
        public float horizontalDistance;
        public float verticalDistance;
        public bool targetInMeleeRange;
        public bool targetInUltimateRange;
        public bool targetInShootRange;
        public bool targetAbove;
        public bool targetBelow;
    }

    [Serializable]
    public sealed class CodexPromptProjectileThreat
    {
        public int sourceSlotId;
        public float etaSeconds = -1f;
        public Vector2 position = Vector2.zero;
        public Vector2 travelDirection = Vector2.right;
    }

    [Serializable]
    public sealed class CodexExecutorFeedback
    {
        public string source = "heuristic_fallback";
        public string summary = string.Empty;
        public string intentMode = string.Empty;
        public string intentReason = string.Empty;
        public bool projectileThreatActive;
        public bool targetVisible;
        public bool roundResetPending;
        public float intentAgeMs = -1f;
        public CodexReportedInputFrame reportedInput = new CodexReportedInputFrame();
    }

    [Serializable]
    public sealed class CodexReportedInputFrame
    {
        public int frame = -1;
        public float axis;
        public Vector2 aim = Vector2.zero;
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
