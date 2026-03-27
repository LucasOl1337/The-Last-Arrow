using ProjectPVP.Input;
using UnityEngine;

namespace ProjectPVP.Gameplay
{
    /// <summary>
    /// Handles jumping, wall jumps, gravity, and head stomps.
    /// </summary>
    public sealed class PlayerJumpSystem
    {
        private const float JumpCutGravityMultiplier = 2.1f;
        private const float FallGravityMultiplier = 1.12f;
        private const float ApexGravityMultiplier = 0.82f;
        private const float ApexVerticalSpeedThreshold = 120f;
        private const float WallImpactCancelUpwardSpeed = 0f;
        private const float WallImpactFallSpeed = 180f;
        private const float JumpStartAnimationDuration = 0.12f;
        private const float GroundGraceVerticalVelocityThreshold = 20f;

        private readonly PlayerContext _context;
        private readonly PlayerStatResolver _statResolver;
        private readonly PlayerCollisionSystem _collisionSystem;
        private readonly PlayerMovementSystem _movementSystem;

        public PlayerJumpSystem(PlayerContext context, PlayerStatResolver statResolver, PlayerCollisionSystem collisionSystem, PlayerMovementSystem movementSystem)
        {
            _context = context;
            _statResolver = statResolver;
            _collisionSystem = collisionSystem;
            _movementSystem = movementSystem;
        }

        public void HandleJumpAndGravity(PlayerInputFrame frame, float deltaTime, ref Vector2 velocity)
        {
            if (TryConsumeJump(ref velocity))
            {
                return;
            }

            if (IsEffectivelyGrounded())
            {
                if (velocity.y < 0f)
                {
                    velocity.y = 0f;
                }

                return;
            }

            bool pushingIntoWall = _context.isTouchingWall && Mathf.Abs(frame.axis) > 0.01f && Mathf.Sign(frame.axis) == -Mathf.Sign(_context.wallNormal.x);
            bool movingIntoWall = _context.isTouchingWall && Mathf.Abs(velocity.x) > 0.01f && Mathf.Sign(velocity.x) == -Mathf.Sign(_context.wallNormal.x);

            if (movingIntoWall)
            {
                velocity.x = 0f;
            }

            if (_context.isTouchingWall && velocity.y > 0f && (pushingIntoWall || movingIntoWall))
            {
                velocity.y = Mathf.Min(velocity.y, WallImpactCancelUpwardSpeed);
            }

            if (_context.isTouchingWall && !HasBufferedJump())
            {
                velocity.x = 0f;
                velocity.y = Mathf.Min(velocity.y, -WallImpactFallSpeed);
                _context.wallDetachIgnoreTimer = 0.14f;
                _context.isTouchingWall = false;
                _context.wallNormal = Vector2.zero;
            }

            float gravityMultiplier = ResolveAirGravityMultiplier(frame, velocity.y);
            velocity.y -= _statResolver.ResolveGravity() * gravityMultiplier * deltaTime;
            velocity.y = Mathf.Max(velocity.y, -_statResolver.ResolveMaxFallSpeed());
        }

        public float ResolveAirGravityMultiplier(PlayerInputFrame frame, float verticalVelocity)
        {
            if (verticalVelocity > 0f && !frame.jumpHeld)
            {
                return JumpCutGravityMultiplier;
            }

            if (verticalVelocity < 0f)
            {
                return FallGravityMultiplier;
            }

            if (Mathf.Abs(verticalVelocity) <= ApexVerticalSpeedThreshold)
            {
                return ApexGravityMultiplier;
            }

            return 1f;
        }

        public bool TryConsumeJump(ref Vector2 velocity)
        {
            if (!HasBufferedJump())
            {
                return false;
            }

            if (_context.isGrounded || _context.coyoteTimeLeft > 0f)
            {
                velocity.y = _statResolver.ResolveJumpVelocity();
                ConsumeBufferedJump();
                _context.coyoteTimeLeft = 0f;
                TriggerJumpStartAnimation();
                return true;
            }

            if (_context.wallJumpGraceTimer > 0f)
            {
                velocity.y = _statResolver.ResolveWallJumpVerticalForce();
                velocity.x = _context.wallNormal.x * _statResolver.ResolveWallJumpHorizontalForce();
                ConsumeBufferedJump();
                _context.wallJumpGraceTimer = 0f;
                TriggerJumpStartAnimation();
                return true;
            }

            return false;
        }

        public bool HasBufferedJump()
        {
            return _context.jumpBufferLeft > 0f;
        }

        public void ConsumeBufferedJump()
        {
            _context.jumpBufferLeft = 0f;
        }

        public void TriggerJumpStartAnimation()
        {
            float duration = _statResolver.ResolveActionDuration("jump_start", JumpStartAnimationDuration);
            _context.jumpStartTimeLeft = duration;
        }

        public bool IsEffectivelyGrounded()
        {
            if (_context.isGrounded)
            {
                return true;
            }

            if (_context.coyoteTimeLeft <= 0f)
            {
                return false;
            }

            return _context.body == null || _context.body.linearVelocity.y <= GroundGraceVerticalVelocityThreshold;
        }

        public void TryCheckHeadStomp()
        {
            if (_context.isDead || _context.body == null || _context.bodyCollider == null || _context.body.linearVelocity.y >= 0f)
            {
                return;
            }

            Bounds selfBounds = _context.bodyCollider.bounds;
            Rect selfFeetRect = PlayerCollisionSystem.BuildRect(
                new Vector2(selfBounds.min.x, selfBounds.min.y),
                new Vector2(selfBounds.size.x, Mathf.Max(10f, selfBounds.size.y * 0.2f)));

            PlayerController[] players = Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            foreach (PlayerController other in players)
            {
                if (other == null || other == _context.Controller || other.IsDead || other.bodyCollider == null)
                {
                    continue;
                }

                Bounds otherBounds = other.bodyCollider.bounds;
                float headHeight = Mathf.Max(12f, otherBounds.size.y * 0.25f);
                Rect otherHeadRect = PlayerCollisionSystem.BuildRect(
                    new Vector2(otherBounds.min.x, otherBounds.max.y - headHeight),
                    new Vector2(otherBounds.size.x, headHeight));

                if (!selfFeetRect.Overlaps(otherHeadRect))
                {
                    continue;
                }

                other.Kill();
                _context.body.linearVelocity = new Vector2(_context.body.linearVelocity.x, _statResolver.ResolveJumpVelocity() * 0.8f);
                TriggerJumpStartAnimation();
                break;
            }
        }
    }
}
