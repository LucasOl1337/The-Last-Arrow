using System;

namespace ProjectPVP.Input
{
    [Serializable]
    public sealed class GamepadActionMap
    {
        public string moveHorizontalAxis = "ProjectPVP_GamepadMoveX";
        public string moveVerticalAxis = "ProjectPVP_GamepadMoveY";
        public string dpadHorizontalAxis = "ProjectPVP_GamepadDpadX";
        public string dpadVerticalAxis = "ProjectPVP_GamepadDpadY";
        public string lookHorizontalAxis = "ProjectPVP_GamepadLookX_A";
        public string lookVerticalAxis = "ProjectPVP_GamepadLookY_A";
        public string lookHorizontalAxisAlt = "ProjectPVP_GamepadLookX_B";
        public string lookVerticalAxisAlt = "ProjectPVP_GamepadLookY_B";
        public string dashSecondaryAxis = "ProjectPVP_GamepadTriggerR_A";
        public string dashSecondaryAxisAlt = "ProjectPVP_GamepadTriggerR_B";
        public string dashSecondaryAxisThird = "ProjectPVP_GamepadTriggerR_C";
        public int jumpButton = 0;
        public int jumpAlternateButton = -1;
        public int shootButton = 2;
        public int meleeButton = 1;
        public int ultimateButton = 3;
        public int dashPrimaryButton = 4;
        public int dashPrimaryAlternateButton = 5;
        public int dashPrimaryThirdButton = 6;
        public int dashSecondaryButton = 7;
        public int dpadLeftButton = -1;
        public int dpadRightButton = -1;
        public int dpadUpButton = -1;
        public int dpadDownButton = -1;
        public float deadzone = 0.18f;
        public float aimDeadzone = 0.22f;
        public float triggerPressThreshold = 0.45f;
        public bool useMoveStickAsAimFallback = true;

        public GamepadActionMap Clone()
        {
            return new GamepadActionMap
            {
                moveHorizontalAxis = moveHorizontalAxis,
                moveVerticalAxis = moveVerticalAxis,
                dpadHorizontalAxis = dpadHorizontalAxis,
                dpadVerticalAxis = dpadVerticalAxis,
                lookHorizontalAxis = lookHorizontalAxis,
                lookVerticalAxis = lookVerticalAxis,
                lookHorizontalAxisAlt = lookHorizontalAxisAlt,
                lookVerticalAxisAlt = lookVerticalAxisAlt,
                dashSecondaryAxis = dashSecondaryAxis,
                dashSecondaryAxisAlt = dashSecondaryAxisAlt,
                dashSecondaryAxisThird = dashSecondaryAxisThird,
                jumpButton = jumpButton,
                jumpAlternateButton = jumpAlternateButton,
                shootButton = shootButton,
                meleeButton = meleeButton,
                ultimateButton = ultimateButton,
                dashPrimaryButton = dashPrimaryButton,
                dashPrimaryAlternateButton = dashPrimaryAlternateButton,
                dashPrimaryThirdButton = dashPrimaryThirdButton,
                dashSecondaryButton = dashSecondaryButton,
                dpadLeftButton = dpadLeftButton,
                dpadRightButton = dpadRightButton,
                dpadUpButton = dpadUpButton,
                dpadDownButton = dpadDownButton,
                deadzone = deadzone,
                aimDeadzone = aimDeadzone,
                triggerPressThreshold = triggerPressThreshold,
                useMoveStickAsAimFallback = useMoveStickAsAimFallback,
            };
        }

        public static GamepadActionMap CreateDefault()
        {
            return new GamepadActionMap
            {
                moveHorizontalAxis = "ProjectPVP_GamepadMoveX",
                moveVerticalAxis = "ProjectPVP_GamepadMoveY",
                dpadHorizontalAxis = "ProjectPVP_GamepadDpadX",
                dpadVerticalAxis = "ProjectPVP_GamepadDpadY",
                lookHorizontalAxis = "ProjectPVP_GamepadLookX_A",
                lookVerticalAxis = "ProjectPVP_GamepadLookY_A",
                lookHorizontalAxisAlt = "ProjectPVP_GamepadLookX_B",
                lookVerticalAxisAlt = "ProjectPVP_GamepadLookY_B",
                dashSecondaryAxis = "ProjectPVP_GamepadTriggerR_A",
                dashSecondaryAxisAlt = "ProjectPVP_GamepadTriggerR_B",
                dashSecondaryAxisThird = "ProjectPVP_GamepadTriggerR_C",
                jumpButton = 0,
                jumpAlternateButton = -1,
                shootButton = 2,
                meleeButton = 1,
                ultimateButton = 3,
                dashPrimaryButton = 4,
                dashPrimaryAlternateButton = 5,
                dashPrimaryThirdButton = 6,
                dashSecondaryButton = 7,
                dpadLeftButton = -1,
                dpadRightButton = -1,
                dpadUpButton = -1,
                dpadDownButton = -1,
                deadzone = 0.18f,
                aimDeadzone = 0.22f,
                triggerPressThreshold = 0.45f,
                useMoveStickAsAimFallback = true,
            };
        }
    }
}
