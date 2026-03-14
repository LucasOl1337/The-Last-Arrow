using UnityEngine;

namespace ProjectPVP.Input
{
    [CreateAssetMenu(fileName = "GamepadControlProfile", menuName = "ProjectPVP/Input/Gamepad Control Profile")]
    public sealed class GamepadControlProfileAsset : ScriptableObject
    {
        public GamepadActionMap actionMap = GamepadActionMap.CreateDefault();

        public GamepadActionMap CreateRuntimeMap()
        {
            return actionMap != null
                ? actionMap.Clone()
                : GamepadActionMap.CreateDefault();
        }
    }
}
