using ProjectPVP.Match;
using UnityEngine;

namespace ProjectPVP.Input
{
    public sealed class IdleCombatantInputSource : MonoBehaviour, ICombatantInputSource
    {
        [Min(1)] public int slotId = 1;

        private int _frameIndex;
        private PlayerInputFrame _currentFrame;

        public PlayerInputFrame CurrentFrame => _currentFrame;
        public int ActiveGamepadSlot => -1;
        public string FaceButtonDebug => "Idle";

        public void CaptureFrame()
        {
            _currentFrame = new PlayerInputFrame
            {
                frame = _frameIndex,
                aim = Vector2.right,
            };

            _frameIndex += 1;
        }

        public void ConfigureForSlot(CombatantSlotId configuredSlotId)
        {
            slotId = Mathf.Max(1, configuredSlotId.ToInt());
        }

        public void ResetInputState()
        {
            _currentFrame = default;
            _frameIndex = 0;
        }
    }
}
