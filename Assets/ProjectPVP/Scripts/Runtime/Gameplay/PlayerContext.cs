using System;
using System.Collections.Generic;
using ProjectPVP.Audio;
using ProjectPVP.Data;
using ProjectPVP.Input;
using ProjectPVP.Match;
using UnityEngine;

namespace ProjectPVP.Gameplay
{
    /// <summary>
    /// Shared mutable state context for all player systems.
    /// All fields are public to allow direct access by systems.
    /// </summary>
    public class PlayerContext
    {
        // References to Unity components
        public Rigidbody2D body;
        public BoxCollider2D bodyCollider;
        public SpriteRenderer spriteRenderer;
        public CombatantAnchorRig anchorRig;
        public PlayerCombatAnchor spawnAnchor;
        public Transform projectileOrigin;
        public PlayerCombatAnchor meleeHitboxAnchor;
        public PlayerCombatAnchor ultimateHitboxAnchor;

        // Character definition
        public CharacterDefinition characterDefinition;

        // Movement and physics state
        public Vector2 dashVelocity = Vector2.zero;
        public Vector2 lastDashVelocity = Vector2.zero;
        public int facing = 1;

        // Arrows and resources
        public int arrows;

        // State flags
        public bool isDead = false;
        public bool aimHoldActive = false;
        public bool shootHeldLastFrame = false;
        public bool dashJumpUsed = false;
        public bool pendingDashPrimary = false;
        public bool pendingDashSecondary = false;
        public bool isGrounded = false;
        public bool isTouchingWall = false;

        // Timers
        public float dashTimeLeft = 0f;
        public float dashPrimaryCooldownLeft = 0f;
        public float dashSecondaryCooldownLeft = 0f;
        public float dashComboWindowLeft = 0f;
        public float shootCooldownLeft = 0f;
        public float meleeCooldownLeft = 0f;
        public float meleeTimeLeft = 0f;
        public float ultimateCooldownLeft = 0f;
        public float ultimateTimeLeft = 0f;
        public float ultimateTotalDuration = 0f;
        public float meleeAnimationTimeLeft = 0f;
        public float shootAnimationTimeLeft = 0f;
        public float jumpStartTimeLeft = 0f;
        public float dashAnimationHoldTimeLeft = 0f;
        public float ultimateAnimationTimeLeft = 0f;
        public float jumpBufferLeft = 0f;
        public float coyoteTimeLeft = 0f;
        public float dashParryTimer = 0f;
        public float dashPressTimer = 0f;
        public float currentOverrideLockLeft = 0f;
        public float wallJumpGraceTimer = 0f;
        public float wallDetachIgnoreTimer = 0f;

        // Ultimate-specific state
        public bool ultimateImpactApplied = false;
        public Vector2 ultimateDashVelocity = Vector2.zero;
        public Vector2 lastUltimateDashVelocity = Vector2.zero;
        public float ultimateDashTimeLeft = 0f;
        public float ultimateProjectileBlockTimer = 0f;

        // Hitstun & Knockback
        public float hitStunTimeLeft = 0f;
        public Vector2 knockbackVelocity = Vector2.zero;
        public float knockbackTimeLeft = 0f;

        // Action override state
        public string currentOverrideAction = string.Empty;
        public string pendingOverrideAction = string.Empty;
        public string activeColliderAction = string.Empty;
        public int currentOverridePriority = -99999;
        public int pendingOverridePriority = -99999;
        public float pendingOverrideLockLeft = 0f;

        // Aim and directions
        public Vector2 aimHoldDirection = Vector2.right;
        public Vector2 wallNormal = Vector2.zero;

        // Melee tracking
        public HashSet<int> meleeHitIds = new HashSet<int>();

        // Action locks
        public List<ActionLockEntry> actionLockEntries = new List<ActionLockEntry>(6);

        // Projectile tracking
        public ProjectileController lastLaunchedProjectile;

        // Physics arrays
        public RaycastHit2D[] castHits = new RaycastHit2D[8];
        public Collider2D[] overlapHits = new Collider2D[16];

        // Collision settings
        public LayerMask groundMask;
        public float groundCheckDistance = 8f;
        public float wallCheckDistance = 6f;

        // Current input frame
        public PlayerInputFrame currentInputFrame;

        // Transform reference
        public Transform transform;

        // Back-reference to the owning PlayerController
        public PlayerController Controller;

        // Runtime objects populated by PlayerController
        public ProjectileController ProjectilePrefab;
        public ICombatantInputSource RuntimeInputSource;
        public CharacterAudioController AudioController;
        public CharacterMechanicsRuntime CharacterMechanicsRuntime;
        public CharacterMechanicsModule CharacterMechanicsSource;

        // Slot identity
        public int slotId;
        public CombatantSlotProfile slotProfile;
    }

    /// <summary>
    /// Represents a lock on an action for a specific duration.
    /// </summary>
    public struct ActionLockEntry
    {
        public string action;
        public float remaining;
        public bool cancelable;
    }
}
