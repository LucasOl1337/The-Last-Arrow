using ProjectPVP.Data;
using UnityEngine;

namespace ProjectPVP.Gameplay
{
    /// <summary>
    /// Manages action locks, overrides, and animation states.
    /// </summary>
    public sealed class PlayerActionLockSystem
    {
        private const int PriorityNegativeInfinity = -99999;
        private const float ShootLockDuration = 0.10f;

        private readonly PlayerContext _context;
        private readonly PlayerStatResolver _statResolver;

        public string CurrentVisualActionKey => ResolveVisualActionKey();

        public PlayerActionLockSystem(PlayerContext context, PlayerStatResolver statResolver)
        {
            _context = context;
            _statResolver = statResolver;
        }

        public void UpdateActionLockTimers(float deltaTime)
        {
            for (int index = _context.actionLockEntries.Count - 1; index >= 0; index -= 1)
            {
                var entry = _context.actionLockEntries[index];
                entry.remaining -= deltaTime;
                if (entry.remaining <= 0f)
                {
                    _context.actionLockEntries.RemoveAt(index);
                    ReleaseActionOverride(entry.action);
                    continue;
                }

                _context.actionLockEntries[index] = entry;
            }
        }

        public void UpdateActionOverrideState()
        {
            if (!string.IsNullOrEmpty(_context.pendingOverrideAction) && CanApplyOverride(_context.pendingOverridePriority))
            {
                ApplyPendingOverride();
                return;
            }

            if (ShouldReleaseCancelableOverride())
            {
                ClearCurrentOverride();
            }

            if (string.IsNullOrEmpty(_context.currentOverrideAction) && !string.IsNullOrEmpty(_context.pendingOverrideAction))
            {
                ApplyPendingOverride();
            }
        }

        public void LockActionForDuration(string actionName, float duration, float lockDuration, bool defaultCancelable)
        {
            if (string.IsNullOrWhiteSpace(actionName) || duration <= 0f)
            {
                return;
            }

            bool cancelable = _statResolver.ResolveActionCancelable(actionName, defaultCancelable);
            if (!cancelable)
            {
                lockDuration = Mathf.Max(lockDuration, duration);
            }

            bool updatedExistingEntry = false;
            for (int index = 0; index < _context.actionLockEntries.Count; index += 1)
            {
                var entry = _context.actionLockEntries[index];
                if (!string.Equals(entry.action, actionName, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                entry.remaining = Mathf.Max(entry.remaining, duration);
                entry.cancelable = cancelable;
                _context.actionLockEntries[index] = entry;
                updatedExistingEntry = true;
                break;
            }

            if (!updatedExistingEntry)
            {
                _context.actionLockEntries.Add(new ActionLockEntry { action = actionName, remaining = duration, cancelable = cancelable });
            }

            RequestActionOverride(actionName, PlayerStatResolver.GetActionPriority(actionName), lockDuration);
        }

        public void RequestActionOverride(string actionName, int priority, float lockDuration)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return;
            }

            if (string.Equals(_context.currentOverrideAction, actionName, System.StringComparison.OrdinalIgnoreCase))
            {
                _context.currentOverridePriority = priority;
                _context.currentOverrideLockLeft = Mathf.Max(_context.currentOverrideLockLeft, lockDuration);
                if (string.Equals(_context.pendingOverrideAction, actionName, System.StringComparison.OrdinalIgnoreCase))
                {
                    _context.pendingOverrideAction = string.Empty;
                    _context.pendingOverridePriority = PriorityNegativeInfinity;
                    _context.pendingOverrideLockLeft = 0f;
                }

                return;
            }

            if (CanApplyOverride(priority))
            {
                ApplyOverrideAction(actionName, priority, lockDuration);
                return;
            }

            if (string.IsNullOrEmpty(_context.pendingOverrideAction) || priority >= _context.pendingOverridePriority)
            {
                _context.pendingOverrideAction = actionName;
                _context.pendingOverridePriority = priority;
                _context.pendingOverrideLockLeft = lockDuration;
            }
        }

        public void ReleaseActionOverride(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return;
            }

            if (string.Equals(actionName, _context.currentOverrideAction, System.StringComparison.OrdinalIgnoreCase))
            {
                ClearCurrentOverride();
                ApplyPendingOverride();
                return;
            }

            if (string.Equals(actionName, _context.pendingOverrideAction, System.StringComparison.OrdinalIgnoreCase))
            {
                _context.pendingOverrideAction = string.Empty;
                _context.pendingOverridePriority = PriorityNegativeInfinity;
                _context.pendingOverrideLockLeft = 0f;
            }
        }

        public bool CanApplyOverride(int priority)
        {
            if (string.IsNullOrEmpty(_context.currentOverrideAction))
            {
                return true;
            }

            if (TryGetActionLockEntry(_context.currentOverrideAction, out var currentEntry)
                && currentEntry.remaining > 0f
                && !currentEntry.cancelable)
            {
                return false;
            }

            if (priority > _context.currentOverridePriority)
            {
                return true;
            }

            return priority == _context.currentOverridePriority && _context.currentOverrideLockLeft <= 0f;
        }

        public bool ShouldReleaseCancelableOverride()
        {
            if (string.IsNullOrEmpty(_context.currentOverrideAction) || _context.currentOverrideLockLeft > 0f)
            {
                return false;
            }

            if (!TryGetActionLockEntry(_context.currentOverrideAction, out var currentEntry) || !currentEntry.cancelable)
            {
                return false;
            }

            string nextAction = ResolveBaseVisualActionKey();
            return !string.IsNullOrWhiteSpace(nextAction)
                && !string.Equals(nextAction, _context.currentOverrideAction, System.StringComparison.OrdinalIgnoreCase);
        }

        public bool TryGetActionLockEntry(string actionName, out ActionLockEntry resolvedEntry)
        {
            for (int index = 0; index < _context.actionLockEntries.Count; index += 1)
            {
                var entry = _context.actionLockEntries[index];
                if (!string.Equals(entry.action, actionName, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                resolvedEntry = entry;
                return true;
            }

            resolvedEntry = default;
            return false;
        }

        public void ApplyOverrideAction(string actionName, int priority, float lockDuration)
        {
            _context.currentOverrideAction = actionName;
            _context.currentOverridePriority = priority;
            _context.currentOverrideLockLeft = Mathf.Max(lockDuration, 0f);

            if (string.Equals(actionName, _context.pendingOverrideAction, System.StringComparison.OrdinalIgnoreCase))
            {
                _context.pendingOverrideAction = string.Empty;
                _context.pendingOverridePriority = PriorityNegativeInfinity;
                _context.pendingOverrideLockLeft = 0f;
            }
        }

        public void ApplyPendingOverride()
        {
            if (string.IsNullOrEmpty(_context.pendingOverrideAction))
            {
                return;
            }

            ApplyOverrideAction(_context.pendingOverrideAction, _context.pendingOverridePriority, _context.pendingOverrideLockLeft);
        }

        public void ClearCurrentOverride()
        {
            _context.currentOverrideAction = string.Empty;
            _context.currentOverridePriority = PriorityNegativeInfinity;
            _context.currentOverrideLockLeft = 0f;
        }

        public string ResolveVisualActionKey()
        {
            if (!string.IsNullOrEmpty(_context.currentOverrideAction))
            {
                return _context.currentOverrideAction;
            }

            return ResolveBaseVisualActionKey();
        }

        public string ResolveBaseVisualActionKey()
        {
            bool effectivelyGrounded = IsEffectivelyGrounded();

            if (_context.isDead)
            {
                return "death";
            }

            bool isDashAnimationActive = _context.dashTimeLeft > 0f || _context.dashAnimationHoldTimeLeft > 0f;
            if (isDashAnimationActive)
            {
                return "dash";
            }

            if (_context.aimHoldActive && _context.shootAnimationTimeLeft <= 0f)
            {
                return "aim";
            }

            if (_context.shootAnimationTimeLeft > 0f)
            {
                return "shoot";
            }

            if (_context.ultimateAnimationTimeLeft > 0f)
            {
                return "ult";
            }

            if (_context.meleeAnimationTimeLeft > 0f)
            {
                return "melee";
            }

            if (_context.jumpStartTimeLeft > 0f)
            {
                return "jump_start";
            }

            if (!effectivelyGrounded)
            {
                return "jump_air";
            }

            float horizontalVelocity = _context.body != null ? _context.body.linearVelocity.x : 0f;
            if (Mathf.Abs(horizontalVelocity) > 10f || Mathf.Abs(_context.currentInputFrame.axis) > 0.1f)
            {
                if (_context.characterDefinition != null && _context.characterDefinition.HasActionAnimation("running"))
                {
                    return "running";
                }

                return "walk";
            }

            return "idle";
        }

        public void ApplyRuntimeColliderOverride(string actionName)
        {
            if (_context.bodyCollider == null)
            {
                return;
            }

            var overrideData = FindActionColliderOverride(actionName);
            Vector2 targetSize = overrideData != null ? overrideData.size : _statResolver.ResolveColliderSize();
            Vector2 targetOffset = overrideData != null ? overrideData.offset : _statResolver.ResolveColliderOffset();
            string resolvedAction = overrideData != null ? actionName : string.Empty;

            if (_context.activeColliderAction == resolvedAction
                && _context.bodyCollider.size == targetSize
                && _context.bodyCollider.offset == targetOffset)
            {
                return;
            }

            _context.bodyCollider.size = targetSize;
            _context.bodyCollider.offset = targetOffset;
            _context.activeColliderAction = resolvedAction;
        }

        public ActionColliderOverride FindActionColliderOverride(string actionName)
        {
            return _context.characterDefinition != null ? _context.characterDefinition.FindActionColliderOverride(actionName) : null;
        }

        private bool IsEffectivelyGrounded()
        {
            if (_context.isGrounded)
            {
                return true;
            }

            if (_context.coyoteTimeLeft <= 0f)
            {
                return false;
            }

            return _context.body == null || _context.body.linearVelocity.y <= 20f;
        }
    }
}
