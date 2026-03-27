using ProjectPVP.Input;
using ProjectPVP.Match;
using UnityEngine;

namespace ProjectPVP.AI
{
    public sealed class AiIdleInputSource : MonoBehaviour, ICombatantInputSource
    {
        private PlayerInputFrame _currentFrame;

        public PlayerInputFrame CurrentFrame => _currentFrame;
        public int ActiveGamepadSlot => -1;
        public string FaceButtonDebug => "AI/Idle";

        public void CaptureFrame()
        {
            _currentFrame = default;
        }

        public void ConfigureForSlot(CombatantSlotId slotId)
        {
        }
    }
}
