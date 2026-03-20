using ProjectPVP.Data;
using ProjectPVP.Input;
using ProjectPVP.Match;

namespace ProjectPVP.Gameplay
{
    public readonly struct CombatantRuntimeContext
    {
        public CombatantRuntimeContext(
            CombatantSlotId slotId,
            PlayerController controller,
            CharacterDefinition definition,
            CombatantAnchorRig anchorRig,
            ICombatantInputSource inputSource)
        {
            SlotId = slotId;
            Controller = controller;
            Definition = definition;
            AnchorRig = anchorRig;
            InputSource = inputSource;
        }

        public CombatantSlotId SlotId { get; }
        public PlayerController Controller { get; }
        public CharacterDefinition Definition { get; }
        public CombatantAnchorRig AnchorRig { get; }
        public ICombatantInputSource InputSource { get; }
    }
}
