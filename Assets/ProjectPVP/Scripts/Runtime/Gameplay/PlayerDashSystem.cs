using ProjectPVP.Input;
using UnityEngine;

namespace ProjectPVP.Gameplay
{
    /// <summary>
    /// Handles dash attacks and velocity.
    /// </summary>
    public sealed class PlayerDashSystem
    {
        private const float DashParryWindow = 0.2f;
        private const float DashPressParryWindow = 0.2f;
        private const float DashComboWindow = 0.2f;

        private readonly PlayerContext _context;
        private readonly PlayerStatResolver _statResolver;

        public PlayerDashSystem(PlayerContext context, PlayerStatResolver statResolver)
        {
            _context = context;
            _statResolver = statResolver;
        }

        public void TryStartDash(PlayerInputFrame frame)
        {
            if (_context.isDead)
            {
                return;
            }

            bool primaryPressed = frame.dashPrimaryPressed;
            bool secondaryPressed = frame.dashSecondaryPressed;
            if (primaryPressed || secondaryPressed)
            {
                if (CanPressedDashStart(primaryPressed, secondaryPressed))
                {
                    _context.dashPressTimer = DashPressParryWindow;
                }

                _context.pendingDashPrimary |= primaryPressed;
                _context.pendingDashSecondary |= secondaryPressed;
                _context.dashComboWindowLeft = Mathf.Max(_context.dashComboWindowLeft, DashComboWindow);
            }

            if (!_context.pendingDashPrimary && !_context.pendingDashSecondary)
            {
                return;
            }

            bool isDashing = _context.dashTimeLeft > 0f;
            if (isDashing)
            {
                return;
            }

            if (_context.dashComboWindowLeft <= 0f && !primaryPressed && !secondaryPressed)
            {
                _context.pendingDashPrimary = false;
                _context.pendingDashSecondary = false;
                return;
            }

            int usedCount = 0;
            bool usePrimary = _context.pendingDashPrimary && _context.dashPrimaryCooldownLeft <= 0f;
            bool useSecondary = _context.pendingDashSecondary && _context.dashSecondaryCooldownLeft <= 0f;

            if (usePrimary)
            {
                _context.dashPrimaryCooldownLeft = _statResolver.ResolveDashCooldown();
                _context.pendingDashPrimary = false;
                usedCount += 1;
            }

            if (useSecondary)
            {
                _context.dashSecondaryCooldownLeft = _statResolver.ResolveDashCooldown();
                _context.pendingDashSecondary = false;
                usedCount += 1;
            }

            if (usedCount <= 0)
            {
                if (_context.dashComboWindowLeft <= 0f)
                {
                    _context.pendingDashPrimary = false;
                    _context.pendingDashSecondary = false;
                }

                return;
            }

            _context.dashComboWindowLeft = 0f;

            Vector2 direction = ResolveDashDirection(frame);
            float dashSpeed = _statResolver.ResolveDashDistance() > 0f && _statResolver.ResolveDashDuration() > 0f
                ? (_statResolver.ResolveDashDistance() * usedCount) / _statResolver.ResolveDashDuration()
                : _statResolver.ResolveMoveSpeed() * _statResolver.ResolveDashMultiplier() * usedCount;

            _context.dashVelocity = direction * dashSpeed;
            _context.dashTimeLeft = _statResolver.ResolveDashDuration();
            _context.dashParryTimer = DashParryWindow;
            _context.dashJumpUsed = false;
            TriggerDashAnimation(_statResolver.ResolveActionDuration("dash", 0.3f));
        }

        private bool CanPressedDashStart(bool primaryPressed, bool secondaryPressed)
        {
            if (_context.isDead || _context.dashTimeLeft > 0f)
            {
                return false;
            }

            return (primaryPressed && _context.dashPrimaryCooldownLeft <= 0f)
                || (secondaryPressed && _context.dashSecondaryCooldownLeft <= 0f);
        }

        public Vector2 UpdateDashVelocity(float deltaTime, ref Vector2 velocity)
        {
            if (_context.isDead || _context.dashTimeLeft <= 0f)
            {
                return Vector2.zero;
            }

            if (HasBufferedJump() && !_context.dashJumpUsed)
            {
                velocity.y = Mathf.Max(velocity.y, _statResolver.ResolveJumpVelocity());
                _context.dashJumpUsed = true;
                ConsumeBufferedJump();
                TriggerJumpStartAnimation();
            }

            float safeDeltaTime = Mathf.Max(0f, deltaTime);
            float appliedDashTime = safeDeltaTime > 0f
                ? Mathf.Min(_context.dashTimeLeft, safeDeltaTime)
                : 0f;
            Vector2 dashVelocity = safeDeltaTime > 0f
                ? _context.dashVelocity * (appliedDashTime / safeDeltaTime)
                : Vector2.zero;
            _context.dashTimeLeft = Mathf.Max(0f, _context.dashTimeLeft - safeDeltaTime);

            if (_context.dashTimeLeft <= 0f)
            {
                _context.dashVelocity = Vector2.zero;
            }

            return dashVelocity;
        }

        public void TriggerDashAnimation(float duration)
        {
            if (_context.isDead)
            {
                return;
            }

            _context.dashAnimationHoldTimeLeft = Mathf.Max(duration, 0f);
        }

        public Vector2 ResolveDashDirection()
        {
            return ResolveDashDirection(default);
        }

        public Vector2 ResolveDashDirection(PlayerInputFrame frame)
        {
            int facingDirection = _context.facing == 0 ? 1 : (_context.facing > 0 ? 1 : -1);
            Vector2 movementDirection = frame.Movement;
            Vector2 rawDirection = movementDirection.sqrMagnitude > 0.01f
                ? movementDirection
                : frame.aim;
            Vector2 snappedDirection = PlayerMovementSystem.Snap8Dir(rawDirection);
            if (snappedDirection.sqrMagnitude <= 0.01f)
            {
                snappedDirection = new Vector2(facingDirection, 0f);
            }

            return ApplyUpwardDashMultiplier(snappedDirection);
        }

        private Vector2 ApplyUpwardDashMultiplier(Vector2 direction)
        {
            if (direction.y <= 0f)
            {
                return direction;
            }

            float originalMagnitude = direction.magnitude;
            direction.y *= Mathf.Max(0f, _statResolver.ResolveDashUpwardMultiplier());

            if (Mathf.Abs(direction.x) > 0.01f && direction.sqrMagnitude > 0.01f)
            {
                direction = direction.normalized * originalMagnitude;
            }

            return direction;
        }

        public void ApplyTransientVelocity(ref Vector2 velocity, Vector2 previousVelocity, Vector2 currentVelocity, ref Vector2 lastVelocity)
        {
            if (previousVelocity != Vector2.zero && currentVelocity == Vector2.zero)
            {
                velocity += previousVelocity;
                lastVelocity = Vector2.zero;
                return;
            }

            velocity += currentVelocity;
            lastVelocity = currentVelocity;
        }

        private bool HasBufferedJump()
        {
            return _context.jumpBufferLeft > 0f;
        }

        private void ConsumeBufferedJump()
        {
            _context.jumpBufferLeft = 0f;
        }

        private void TriggerJumpStartAnimation()
        {
            float duration = _statResolver.ResolveActionDuration("jump_start", 0.12f);
            _context.jumpStartTimeLeft = duration;
        }
    }
}
