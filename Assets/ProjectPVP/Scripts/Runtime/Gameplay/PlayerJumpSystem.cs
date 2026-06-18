using System.Collections.Generic;
using ProjectPVP.Data;
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
        private const float WallJumpDetachIgnoreDuration = 0.14f;
        private const float JumpStartAnimationDuration = 0.12f;

        private readonly PlayerContext _context;
        private readonly PlayerStatResolver _statResolver;
        private readonly PlayerCollisionSystem _collisionSystem;
        private readonly PlayerMovementSystem _movementSystem;
        private readonly List<PlayerController> _playerQueryBuffer = new();

        public PlayerJumpSystem(PlayerContext context, PlayerStatResolver statResolver, PlayerCollisionSystem collisionSystem, PlayerMovementSystem movementSystem)
        {
            _context = context;
            _statResolver = statResolver;
            _collisionSystem = collisionSystem;
            _movementSystem = movementSystem;
        }

        public void HandleJumpAndGravity(PlayerInputFrame frame, float deltaTime, ref Vector2 velocity)
        {
            if (_context.isDead)
            {
                return;
            }

            if (TryConsumeJump(ref velocity))
            {
                return;
            }

            if (_context.isGrounded)
            {
                if (velocity.y < 0f)
                {
                    velocity.y = 0f;
                }

                return;
            }

            bool pushingIntoWall = _context.isTouchingWall && Mathf.Abs(frame.axis) > 0.01f && Mathf.Sign(frame.axis) == -Mathf.Sign(_context.wallNormal.x);
            bool movingIntoWall = _context.isTouchingWall && Mathf.Abs(velocity.x) > 0.01f && Mathf.Sign(velocity.x) == -Mathf.Sign(_context.wallNormal.x);
            bool wallSliding = false;

            if (movingIntoWall)
            {
                velocity.x = 0f;
            }

            bool detachedFromUpwardWallImpact = false;
            if (_context.isTouchingWall && velocity.y > 0f && (pushingIntoWall || movingIntoWall))
            {
                velocity.y = Mathf.Min(velocity.y, WallImpactCancelUpwardSpeed);
                _context.wallDetachIgnoreTimer = WallJumpDetachIgnoreDuration;
                _context.isTouchingWall = false;
                detachedFromUpwardWallImpact = true;
            }

            if (_context.isTouchingWall && !detachedFromUpwardWallImpact && !HasBufferedJump())
            {
                if (velocity.y <= 0f && (pushingIntoWall || movingIntoWall))
                {
                    velocity.x = 0f;
                    velocity.y = Mathf.Max(velocity.y, -_statResolver.ResolveWallSlideSpeed());
                    wallSliding = true;
                }
                else
                {
                    velocity.x = 0f;
                    velocity.y = Mathf.Min(velocity.y, -WallImpactFallSpeed);
                    _context.wallDetachIgnoreTimer = WallJumpDetachIgnoreDuration;
                    _context.isTouchingWall = false;
                }
            }

            float gravityMultiplier = wallSliding
                ? _statResolver.ResolveWallGravityScale()
                : ResolveAirGravityMultiplier(frame, velocity.y);
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
            if (_context.isDead || !HasBufferedJump())
            {
                return false;
            }

            if (!_context.isGrounded && CanWallJump())
            {
                ConsumeWallJump(ref velocity);
                return true;
            }

            if (_context.isGrounded || _context.coyoteTimeLeft > 0f)
            {
                velocity.y = _statResolver.ResolveJumpVelocity();
                ConsumeBufferedJump();
                _context.coyoteTimeLeft = 0f;
                TriggerJumpStartAnimation();
                return true;
            }

            if (CanWallJump())
            {
                ConsumeWallJump(ref velocity);
                return true;
            }

            return false;
        }

        public bool HasBufferedJump()
        {
            return _context.jumpBufferLeft > 0f;
        }

        private bool CanWallJump()
        {
            if (_context.wallNormal.sqrMagnitude <= 0.01f)
            {
                return false;
            }

            return _context.wallJumpGraceTimer > 0f || _context.isTouchingWall;
        }

        private void ConsumeWallJump(ref Vector2 velocity)
        {
            velocity.y = _statResolver.ResolveWallJumpVerticalForce();
            velocity.x = _context.wallNormal.x * _statResolver.ResolveWallJumpHorizontalForce();
            ConsumeBufferedJump();
            _context.coyoteTimeLeft = 0f;
            _context.wallJumpGraceTimer = 0f;
            _context.wallDetachIgnoreTimer = WallJumpDetachIgnoreDuration;
            _context.isTouchingWall = false;
            TriggerJumpStartAnimation();
        }

        public void ConsumeBufferedJump()
        {
            _context.jumpBufferLeft = 0f;
        }

        public void TriggerJumpStartAnimation()
        {
            if (_context.isDead)
            {
                return;
            }

            float duration = _statResolver.ResolveActionDuration("jump_start", JumpStartAnimationDuration);
            _context.jumpStartTimeLeft = duration;
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

            bool appliedStomp = false;
            PlayerController.CopyActivePlayers(_playerQueryBuffer);
            if (_playerQueryBuffer.Count > 0)
            {
                for (int index = 0; index < _playerQueryBuffer.Count; index += 1)
                {
                    if (TryApplyHeadStompReaction(_playerQueryBuffer[index], selfFeetRect))
                    {
                        appliedStomp = true;
                        break;
                    }
                }
            }
            else
            {
                PlayerController[] players = Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
                foreach (PlayerController other in players)
                {
                    if (TryApplyHeadStompReaction(other, selfFeetRect))
                    {
                        appliedStomp = true;
                        break;
                    }
                }
            }

            _playerQueryBuffer.Clear();
            if (appliedStomp)
            {
                _context.body.linearVelocity = new Vector2(_context.body.linearVelocity.x, _statResolver.ResolveJumpVelocity() * 0.8f);
                TriggerJumpStartAnimation();
            }
        }

        private bool TryApplyHeadStompReaction(PlayerController target, Rect selfFeetRect)
        {
            if (target == null || target == _context.Controller || target.IsDead || target.bodyCollider == null || target.IsDodgeInvulnerable)
            {
                return false;
            }

            Bounds targetBounds = target.bodyCollider.bounds;
            float headHeight = Mathf.Max(12f, targetBounds.size.y * 0.25f);
            Rect targetHeadRect = PlayerCollisionSystem.BuildRect(
                new Vector2(targetBounds.min.x, targetBounds.max.y - headHeight),
                new Vector2(targetBounds.size.x, headHeight));

            if (!selfFeetRect.Overlaps(targetHeadRect))
            {
                return false;
            }

            ApplyHeadStompReaction(target);
            return true;
        }

        private void ApplyHeadStompReaction(PlayerController target)
        {
            target.Kill(_context.Controller, "Head Stomp");
        }
    }
}
