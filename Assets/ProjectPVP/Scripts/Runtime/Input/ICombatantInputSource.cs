using ProjectPVP.Match;

namespace ProjectPVP.Input
{
    public interface ICombatantInputSource
    {
        PlayerInputFrame CurrentFrame { get; }
        int ActiveGamepadSlot { get; }
        string FaceButtonDebug { get; }

        void CaptureFrame();
        void ConfigureForSlot(CombatantSlotId slotId);
        void ResetInputState();
    }

    public interface IBotFeedbackInputSource
    {
        string BotFeedback { get; }
    }

    public interface IBotCoachStatusInputSource
    {
        string BotControllerStatus { get; }
    }
}
