using ProjectPVP.Data;
using UnityEngine;

namespace ProjectPVP.Gameplay
{
    /// <summary>
    /// Resolves character stat values from CharacterDefinition with fallback defaults.
    /// </summary>
    public sealed class PlayerStatResolver
    {
        private const int PriorityNegativeInfinity = -99999;
        private const float DefaultMoveSpeed = 415f;
        private const float DefaultAcceleration = 2200f;
        private const float DefaultFriction = 2400f;
        private const float DefaultGravity = 1500f;
        private const float DefaultMaxFallSpeed = 1500f;
        private const float DefaultJumpVelocity = 660f;
        private const float DefaultShootCooldown = 0.02f;
        private const int DefaultMaxArrows = 3;
        private const float DefaultMeleeCooldown = 0.45f;
        private const float DefaultMeleeDuration = 0.12f;
        private const float DefaultUltimateCooldown = 1.25f;
        private const float DefaultUltimateDuration = 0.28f;
        private const float DefaultUltimateRadius = 180f;
        private const float DefaultUltimateWindupRatio = 0.45f;
        private const float DefaultUltimateDashDuration = 0.1f;
        private const float DefaultUltimateReplayDuration = 0.1f;
        private const float DefaultUltimateProjectileBlockDuration = 0.12f;
        private const float DefaultWallJumpHorizontalForce = 500f;
        private const float DefaultWallJumpVerticalForce = 720f;
        private const float DefaultWallSlideSpeed = 180f;
        private const float DefaultWallGravityScale = 0.2f;
        private const float DefaultDashMultiplier = 1.8f;
        private const float DefaultDashDuration = 0.12f;
        private const float DefaultDashCooldown = 0.45f;
        private const float DefaultDashDistance = 100f;
        private const float DefaultDashUpwardMultiplier = 0.5f;
        private const float DefaultProjectileAssistStrength = 0.2f;
        private const float DefaultProjectileAssistMaxTurnRateDeg = 420f;
        private const float DefaultProjectileAssistAcquireConeDeg = 36f;
        private const float DefaultProjectileAssistMaxRange = 1600f;
        private const float DefaultProjectileAssistMinDistance = 40f;
        private const float DefaultProjectileAssistDropoffStartRatio = 0.6f;
        private const float ArrowEncumbrancePerExtraArrow = 0.15f;
        private const float ArrowEncumbranceMinimumScale = 0.5f;

        private readonly PlayerContext _context;

        public PlayerStatResolver(PlayerContext context)
        {
            _context = context;
        }

        public float ResolveMoveSpeed()
        {
            float baseValue = _context.characterDefinition != null ? _context.characterDefinition.moveSpeed : DefaultMoveSpeed;
            return baseValue * ResolveMoveScale() * ResolveArrowEncumbranceScale();
        }

        public float ResolveMoveScale()
        {
            float scale = _context.characterDefinition != null ? _context.characterDefinition.runtimeMoveScale : 1f;
            return Mathf.Max(0.1f, scale);
        }

        public float ResolveArrowEncumbranceScale()
        {
            int heldArrows = _context != null ? Mathf.Max(0, _context.arrows) : 0;
            int extraArrows = Mathf.Max(0, heldArrows - 1);
            float scale = 1f - (extraArrows * ArrowEncumbrancePerExtraArrow);
            return Mathf.Clamp(scale, ArrowEncumbranceMinimumScale, 1f);
        }

        public float ResolveJumpScale()
        {
            float scale = _context.characterDefinition != null ? _context.characterDefinition.runtimeJumpScale : 1f;
            return Mathf.Max(0.1f, scale);
        }

        public float ResolveGravityScale()
        {
            float scale = _context.characterDefinition != null ? _context.characterDefinition.runtimeGravityScale : 1f;
            return Mathf.Max(0.1f, scale);
        }

        public float ResolveDashScale()
        {
            float scale = _context.characterDefinition != null ? _context.characterDefinition.runtimeDashScale : 1f;
            return Mathf.Max(0.1f, scale);
        }

        public float ResolveAcceleration()
        {
            float baseValue = _context.characterDefinition != null ? _context.characterDefinition.acceleration : DefaultAcceleration;
            return baseValue * ResolveMoveScale();
        }

        public float ResolveFriction()
        {
            float baseValue = _context.characterDefinition != null ? _context.characterDefinition.friction : DefaultFriction;
            return baseValue * ResolveMoveScale();
        }

        public float ResolveGravity()
        {
            float baseValue = _context.characterDefinition != null ? _context.characterDefinition.gravity : DefaultGravity;
            return baseValue * ResolveGravityScale();
        }

        public float ResolveMaxFallSpeed()
        {
            float baseValue = _context.characterDefinition != null ? _context.characterDefinition.maxFallSpeed : DefaultMaxFallSpeed;
            return baseValue * ResolveGravityScale();
        }

        public float ResolveJumpVelocity()
        {
            float baseValue = _context.characterDefinition != null ? _context.characterDefinition.jumpVelocity : DefaultJumpVelocity;
            return baseValue * ResolveJumpScale();
        }

        public float ResolveShootCooldown()
        {
            return _context.characterDefinition != null ? _context.characterDefinition.shootCooldown : DefaultShootCooldown;
        }

        public int ResolveMaxArrows()
        {
            return _context.characterDefinition != null ? _context.characterDefinition.maxArrows : DefaultMaxArrows;
        }

        public float ResolveMeleeCooldown()
        {
            return _context.characterDefinition != null ? _context.characterDefinition.meleeCooldown : DefaultMeleeCooldown;
        }

        public float ResolveMeleeDuration()
        {
            return _context.characterDefinition != null ? _context.characterDefinition.meleeDuration : DefaultMeleeDuration;
        }

        public float ResolveWallJumpHorizontalForce()
        {
            float value = _context.characterDefinition != null ? _context.characterDefinition.wallJumpHorizontalForce : DefaultWallJumpHorizontalForce;
            return value * ResolveJumpScale();
        }

        public float ResolveWallJumpVerticalForce()
        {
            float value = _context.characterDefinition != null ? _context.characterDefinition.wallJumpVerticalForce : DefaultWallJumpVerticalForce;
            return value * ResolveJumpScale();
        }

        public float ResolveWallSlideSpeed()
        {
            float value = _context.characterDefinition != null ? _context.characterDefinition.wallSlideSpeed : DefaultWallSlideSpeed;
            return value * ResolveGravityScale();
        }

        public float ResolveWallGravityScale()
        {
            return _context.characterDefinition != null ? _context.characterDefinition.wallGravityScale : DefaultWallGravityScale;
        }

        public float ResolveDashMultiplier()
        {
            return _context.characterDefinition != null ? _context.characterDefinition.dashMultiplier : DefaultDashMultiplier;
        }

        public float ResolveDashDuration()
        {
            return _context.characterDefinition != null ? _context.characterDefinition.dashDuration : DefaultDashDuration;
        }

        public float ResolveDashCooldown()
        {
            return _context.characterDefinition != null ? _context.characterDefinition.dashCooldown : DefaultDashCooldown;
        }

        public float ResolveDashDistance()
        {
            float value = _context.characterDefinition != null ? _context.characterDefinition.dashDistance : DefaultDashDistance;
            return value * ResolveDashScale();
        }

        public float ResolveDashUpwardMultiplier()
        {
            return _context.characterDefinition != null ? _context.characterDefinition.dashUpwardMultiplier : DefaultDashUpwardMultiplier;
        }

        public Vector2 ResolveColliderSize()
        {
            return _context.characterDefinition != null ? _context.characterDefinition.colliderSize : new Vector2(90f, 210f);
        }

        public Vector2 ResolveColliderOffset()
        {
            return _context.characterDefinition != null ? _context.characterDefinition.colliderOffset : Vector2.zero;
        }

        public float ResolveUltimateCooldown()
        {
            return Mathf.Max(DefaultUltimateCooldown, ResolveActionDuration("ult", DefaultUltimateDuration) * 2.5f);
        }

        public float ResolveUltimateRadius()
        {
            if (_context.ultimateHitboxAnchor != null && _context.ultimateHitboxAnchor.radius > 0.01f)
            {
                return _context.ultimateHitboxAnchor.radius;
            }

            return _context.characterDefinition != null
                ? Mathf.Max(DefaultUltimateRadius * 0.7f, ResolveColliderSize().x * 1.4f)
                : DefaultUltimateRadius;
        }

        public float ResolveUltimateWindupRatio()
        {
            float configured = _context.characterDefinition != null ? _context.characterDefinition.ultimateWindupRatio : 0f;
            return configured > 0f
                ? Mathf.Clamp01(configured)
                : DefaultUltimateWindupRatio;
        }

        public float ResolveUltimateDashDistance()
        {
            float configured = _context.characterDefinition != null ? _context.characterDefinition.ultimateDashDistance : 0f;
            return configured > 0.01f
                ? configured * ResolveDashScale()
                : 0f;
        }

        public float ResolveUltimateDashDuration()
        {
            float configured = _context.characterDefinition != null ? _context.characterDefinition.ultimateDashDuration : 0f;
            return configured > 0.01f
                ? configured
                : DefaultUltimateDashDuration;
        }

        public float ResolveUltimateReplayDuration()
        {
            float configured = _context.characterDefinition != null ? _context.characterDefinition.ultimateReplayDuration : 0f;
            if (configured > 0.01f)
            {
                return configured;
            }

            float fallback = ResolveUltimateDashDuration();
            return fallback > 0.01f ? fallback : DefaultUltimateReplayDuration;
        }

        public bool ResolveUltimateBlocksProjectiles()
        {
            return _context.characterDefinition != null && _context.characterDefinition.ultimateBlocksProjectiles;
        }

        public float ResolveUltimateProjectileBlockDuration()
        {
            if (!ResolveUltimateBlocksProjectiles())
            {
                return 0f;
            }

            float configured = _context.characterDefinition != null && _context.characterDefinition.ultimateProjectileBlockDuration > 0.01f
                    ? _context.characterDefinition.ultimateProjectileBlockDuration
                    : DefaultUltimateProjectileBlockDuration;
            return Mathf.Max(configured, ResolveUltimateDashDuration());
        }

        public float ResolveUltimateReplayDelay()
        {
            float configured = _context.characterDefinition != null ? _context.characterDefinition.ultimateReplayDelay : 0f;
            return configured > 0.01f ? configured : 0f;
        }

        public float ResolveProjectileForward() => _context.characterDefinition != null ? _context.characterDefinition.projectileForward : 80f;
        public float ResolveProjectileForwardFacing() => _context.characterDefinition != null ? _context.characterDefinition.projectileForwardFacing : 0f;
        public float ResolveProjectileVerticalOffset() => _context.characterDefinition != null ? _context.characterDefinition.projectileVerticalOffset : 0f;
        public float ResolveProjectileInheritVelocityFactor() => _context.characterDefinition != null ? _context.characterDefinition.projectileInheritVelocityFactor : 1f;
        public float ResolveProjectileBaseSpeed() => _context.characterDefinition != null ? Mathf.Max(1f, _context.characterDefinition.projectileBaseSpeed) : 1600f;
        public float ResolveProjectileGravity() => _context.characterDefinition != null ? Mathf.Max(0f, _context.characterDefinition.projectileGravity) : 1500f;
        public float ResolveProjectileScale() => _context.characterDefinition != null ? _context.characterDefinition.projectileScale : 1f;
        public Vector2 ResolveProjectileOriginOffset() => _context.characterDefinition != null ? _context.characterDefinition.projectileOriginOffset : Vector2.zero;
        public ProjectileOriginMode ResolveProjectileOriginMode() => _context.characterDefinition != null ? _context.characterDefinition.projectileOriginMode : ProjectileOriginMode.BowNode;
        public Sprite ResolveProjectileSprite() => _context.characterDefinition != null ? _context.characterDefinition.projectileSprite : null;
        public bool ResolveProjectileAssistEnabled() => _context.characterDefinition != null ? _context.characterDefinition.projectileAssistEnabled : false;
        public float ResolveProjectileAssistStrength() => _context.characterDefinition != null ? _context.characterDefinition.projectileAssistStrength : DefaultProjectileAssistStrength;
        public float ResolveProjectileAssistMaxTurnRateDeg() => _context.characterDefinition != null ? _context.characterDefinition.projectileAssistMaxTurnRateDeg : DefaultProjectileAssistMaxTurnRateDeg;
        public float ResolveProjectileAssistAcquireConeDeg() => _context.characterDefinition != null ? _context.characterDefinition.projectileAssistAcquireConeDeg : DefaultProjectileAssistAcquireConeDeg;
        public float ResolveProjectileAssistMaxRange() => _context.characterDefinition != null ? _context.characterDefinition.projectileAssistMaxRange : DefaultProjectileAssistMaxRange;
        public float ResolveProjectileAssistMinDistance() => _context.characterDefinition != null ? _context.characterDefinition.projectileAssistMinDistance : DefaultProjectileAssistMinDistance;
        public float ResolveProjectileAssistDropoffStartRatio() => _context.characterDefinition != null ? _context.characterDefinition.projectileAssistDropoffStartRatio : DefaultProjectileAssistDropoffStartRatio;

        public float ResolveActionDuration(string actionName, float fallback)
        {
            return _context.characterDefinition != null
                ? _context.characterDefinition.ResolveActionDuration(actionName, fallback)
                : fallback;
        }

        public bool ResolveActionCancelable(string actionName, bool fallback)
        {
            return _context.characterDefinition != null
                ? _context.characterDefinition.ResolveActionCancelable(actionName, fallback)
                : fallback;
        }

        public bool HasUltimateConfigured()
        {
            if (_context.characterDefinition == null)
            {
                return false;
            }

            return _context.characterDefinition.HasActionAnimation("ult")
                || ResolveUltimateDashDistance() > 0.01f
                || ResolveUltimateReplayDelay() > 0.01f;
        }

        public static int GetActionPriority(string actionName)
        {
            switch (actionName)
            {
                case "death":
                    return 120;
                case "ult":
                    return 100;
                case "melee":
                    return 90;
                case "dash":
                    return 80;
                case "shoot":
                    return 70;
                case "aim":
                    return 60;
                case "jump_start":
                    return 55;
                case "jump_air":
                    return 50;
                case "running":
                    return 40;
                case "walk":
                    return 30;
                case "crouch":
                    return 25;
                case "idle":
                    return 10;
                default:
                    return 0;
            }
        }
    }
}
