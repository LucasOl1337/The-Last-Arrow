using UnityEngine;

namespace ProjectPVP.Input
{
    // Compatibility adapter: keeps the slot-based abstraction alive even when the
    // native Input System package is not compiling in the current Unity environment.
    public sealed class InputSystemCombatantInputSource : KeyboardPlayerInputSource
    {
        public UnityEngine.Object inputActions;

        public static bool IsNativeInputSystemAvailable => false;

        public bool useSlotDefaults
        {
            get => usePlayerDefaults;
            set => usePlayerDefaults = value;
        }
    }
}
