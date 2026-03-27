using ProjectPVP.Gameplay;
using ProjectPVP.Input;
using ProjectPVP.Match;
using UnityEngine;

namespace ProjectPVP.AI
{
    public sealed class AiHeuristicInputSource : MonoBehaviour, ICombatantInputSource
    {
        [Min(1)] public int shootHoldFrames = 2;
        [Min(120f)] public float desiredSpacing = 240f;
        public MatchController matchController;

        private PlayerController _player;
        private PlayerInputFrame _currentFrame;
        private int _frameIndex;
        private int _shotHoldFramesLeft;
        private bool _shotHoldActive;
        private string _debugState = "idle";

        public PlayerInputFrame CurrentFrame => _currentFrame;
        public int ActiveGamepadSlot => -1;
        public string FaceButtonDebug => "AI/Heuristic " + _debugState;

        private void Awake()
        {
            CacheReferences();
        }

        private void OnEnable()
        {
            CacheReferences();
        }

        public void CaptureFrame()
        {
            CacheReferences();
            AiCombatSnapshot snapshot = AiCombatSnapshotBuilder.Build(matchController, _player, _frameIndex);
            _currentFrame = Decide(snapshot, _frameIndex);
            _frameIndex += 1;
        }

        public void ConfigureForSlot(CombatantSlotId slotId)
        {
            _frameIndex = 0;
            _shotHoldFramesLeft = 0;
            _shotHoldActive = false;
            _debugState = slotId.ToDisplayName();
        }

        private PlayerInputFrame Decide(AiCombatSnapshot snapshot, int frame)
        {
            if (snapshot == null || snapshot.self == null || snapshot.opponent == null)
            {
                _debugState = "no-opponent";
                return default;
            }

            float horizontalDistance = snapshot.features.horizontalDistance;
            float verticalDistance = snapshot.features.verticalDistance;
            float absHorizontalDistance = Mathf.Abs(horizontalDistance);
            Vector2 aim = new Vector2(horizontalDistance, verticalDistance);
            if (aim.sqrMagnitude > 0.001f)
            {
                aim.Normalize();
            }
            else
            {
                aim = new Vector2(snapshot.self.facing == 0 ? 1f : snapshot.self.facing, 0f);
            }

            AiFrameAction action = new AiFrameAction
            {
                axis = absHorizontalDistance > desiredSpacing ? Mathf.Sign(horizontalDistance) : 0f,
                aimX = aim.x,
                aimY = aim.y,
                up = verticalDistance > 80f,
                down = verticalDistance < -80f,
            };

            if (snapshot.features.hostileProjectileThreat && snapshot.self.dashPrimaryCooldownLeft <= 0.01f)
            {
                action.dashPrimaryPressed = true;
                action.axis = Mathf.Sign(horizontalDistance);
                _shotHoldActive = false;
                _shotHoldFramesLeft = 0;
                _debugState = "defend-dash";
                return AiActionSanitizer.ToPlayerInputFrame(action, frame);
            }

            if (snapshot.features.meleeRangeNow && snapshot.self.meleeCooldownLeft <= 0.01f)
            {
                action.axis = Mathf.Sign(horizontalDistance);
                action.meleePressed = true;
                _shotHoldActive = false;
                _shotHoldFramesLeft = 0;
                _debugState = "melee";
                return AiActionSanitizer.ToPlayerInputFrame(action, frame);
            }

            if (snapshot.self.currentArrows > 0 && snapshot.self.shootCooldownLeft <= 0.01f && snapshot.features.shootLaneOpen)
            {
                if (!_shotHoldActive && absHorizontalDistance >= desiredSpacing * 0.6f)
                {
                    _shotHoldActive = true;
                    _shotHoldFramesLeft = Mathf.Max(1, shootHoldFrames);
                }

                if (_shotHoldActive)
                {
                    if (_shotHoldFramesLeft > 0)
                    {
                        action.shootHeld = true;
                        action.axis = 0f;
                        _shotHoldFramesLeft -= 1;
                        _debugState = "aim-shot";
                    }
                    else
                    {
                        _shotHoldActive = false;
                        _debugState = "release-shot";
                    }

                    return AiActionSanitizer.ToPlayerInputFrame(action, frame);
                }
            }

            if (absHorizontalDistance > desiredSpacing * 1.5f && snapshot.self.dashPrimaryCooldownLeft <= 0.01f)
            {
                action.axis = Mathf.Sign(horizontalDistance);
                action.dashPrimaryPressed = true;
                _debugState = "gap-close";
                return AiActionSanitizer.ToPlayerInputFrame(action, frame);
            }

            if (verticalDistance > 110f && snapshot.self.isGrounded)
            {
                action.jumpPressed = true;
                action.jumpHeld = true;
                _debugState = "jump-chase";
                return AiActionSanitizer.ToPlayerInputFrame(action, frame);
            }

            if (absHorizontalDistance < desiredSpacing * 0.45f)
            {
                action.axis = -Mathf.Sign(horizontalDistance);
                _debugState = "back-step";
            }
            else
            {
                _debugState = "advance";
            }

            return AiActionSanitizer.ToPlayerInputFrame(action, frame);
        }

        private void CacheReferences()
        {
            if (_player == null)
            {
                _player = GetComponent<PlayerController>();
            }

            if (matchController == null)
            {
                matchController = FindFirstObjectByType<MatchController>();
            }
        }
    }
}
