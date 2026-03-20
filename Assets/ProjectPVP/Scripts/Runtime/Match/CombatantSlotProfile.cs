using ProjectPVP.Input;
using UnityEngine;

namespace ProjectPVP.Match
{
    public enum PreferredGamepadFamily
    {
        Any = 0,
        XboxLike = 1,
        DualSense = 2,
    }

    [CreateAssetMenu(fileName = "CombatantSlotProfile", menuName = "ProjectPVP/Match/Combatant Slot Profile")]
    public sealed class CombatantSlotProfile : ScriptableObject
    {
        private static CombatantSlotProfile s_slotOneFallback;
        private static CombatantSlotProfile s_slotTwoFallback;

        public string displayName = string.Empty;
        public bool useDefaultKeyboardBindings = true;
        public PlayerActionMap keyboardBindings = PlayerActionMap.CreateDefaultPlayerOne();
        public bool enableGamepad;
        public bool useSlotIndexAsPreferredGamepad = true;
        [Min(0)] public int preferredGamepadIndex;
        public PreferredGamepadFamily preferredGamepadFamily = PreferredGamepadFamily.Any;
        public bool useDefaultGamepadBindings = true;
        public GamepadActionMap gamepadBindings = GamepadActionMap.CreateDefault();
        public GamepadControlProfileAsset gamepadProfile;
        public bool useDefaultDebugTint = true;
        public Color debugTint = Color.white;

        public string ResolveDisplayName(CombatantSlotId slotId)
        {
            return !string.IsNullOrWhiteSpace(displayName)
                ? displayName.Trim()
                : slotId.ToDisplayName();
        }

        public PlayerActionMap CreateKeyboardBindings(CombatantSlotId slotId)
        {
            if (useDefaultKeyboardBindings)
            {
                return PlayerActionMap.CreateDefaultForPlayer(slotId.ToInt());
            }

            return keyboardBindings != null
                ? keyboardBindings.Clone()
                : PlayerActionMap.CreateDefaultForPlayer(slotId.ToInt());
        }

        public GamepadActionMap CreateGamepadBindings()
        {
            if (gamepadProfile != null)
            {
                return gamepadProfile.CreateRuntimeMap();
            }

            if (useDefaultGamepadBindings)
            {
                return GamepadActionMap.CreateDefault();
            }

            return gamepadBindings != null
                ? gamepadBindings.Clone()
                : GamepadActionMap.CreateDefault();
        }

        public int ResolvePreferredGamepadIndex(CombatantSlotId slotId)
        {
            return useSlotIndexAsPreferredGamepad
                ? Mathf.Max(0, slotId.ToIndex())
                : Mathf.Max(0, preferredGamepadIndex);
        }

        public PreferredGamepadFamily ResolvePreferredGamepadFamily()
        {
            return preferredGamepadFamily;
        }

        public Color ResolveDebugTint(CombatantSlotId slotId)
        {
            return useDefaultDebugTint
                ? ResolveDefaultTint(slotId)
                : debugTint;
        }

        public static CombatantSlotProfile ResolveRuntimeFallback(CombatantSlotId slotId)
        {
            return slotId switch
            {
                CombatantSlotId.SlotTwo => s_slotTwoFallback ??= CreateRuntimeFallback(slotId),
                _ => s_slotOneFallback ??= CreateRuntimeFallback(slotId),
            };
        }

        public static Color ResolveDefaultTint(CombatantSlotId slotId)
        {
            return slotId == CombatantSlotId.SlotTwo
                ? new Color(1f, 0.62f, 0.36f, 1f)
                : new Color(0.34f, 0.86f, 1f, 1f);
        }

        private static CombatantSlotProfile CreateRuntimeFallback(CombatantSlotId slotId)
        {
            CombatantSlotProfile fallback = CreateInstance<CombatantSlotProfile>();
            fallback.hideFlags = HideFlags.HideAndDontSave;
            fallback.displayName = slotId.ToDisplayName();
            fallback.useDefaultKeyboardBindings = true;
            fallback.enableGamepad = slotId == CombatantSlotId.SlotOne;
            fallback.useSlotIndexAsPreferredGamepad = true;
            fallback.preferredGamepadFamily = PreferredGamepadFamily.Any;
            fallback.useDefaultGamepadBindings = true;
            fallback.useDefaultDebugTint = true;
            return fallback;
        }
    }
}
