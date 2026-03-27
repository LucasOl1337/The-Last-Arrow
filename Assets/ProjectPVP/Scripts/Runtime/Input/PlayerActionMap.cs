using System;
using UnityEngine;

namespace ProjectPVP.Input
{
    [Serializable]
    public sealed class PlayerActionMap
    {
        public KeyCode left = KeyCode.A;
        public KeyCode right = KeyCode.D;
        public KeyCode up = KeyCode.W;
        public KeyCode down = KeyCode.S;
        public KeyCode jump = KeyCode.Space;
        public KeyCode shoot = KeyCode.Q;
        public KeyCode melee = KeyCode.E;
        public KeyCode ultimate = KeyCode.R;
        public KeyCode dashPrimary = KeyCode.LeftShift;
        public KeyCode dashSecondary = KeyCode.LeftAlt;

        public PlayerActionMap Clone()
        {
            return new PlayerActionMap
            {
                left = left,
                right = right,
                up = up,
                down = down,
                jump = jump,
                shoot = shoot,
                melee = melee,
                ultimate = ultimate,
                dashPrimary = dashPrimary,
                dashSecondary = dashSecondary,
            };
        }

        public static PlayerActionMap CreateDefaultPlayerOne()
        {
            return new PlayerActionMap();
        }

        public static PlayerActionMap CreateDefaultPlayerTwo()
        {
            return new PlayerActionMap
            {
                // 60% keyboard layout (IJKL + Ctrl/Shift/Alt/Enter cluster).
                left = KeyCode.J,
                right = KeyCode.L,
                up = KeyCode.I,
                down = KeyCode.K,
                jump = KeyCode.Return,
                shoot = KeyCode.RightControl,
                melee = KeyCode.RightAlt,
                ultimate = KeyCode.P,
                dashPrimary = KeyCode.RightShift,
                dashSecondary = KeyCode.M,
            };
        }

        public static PlayerActionMap CreateDefaultForPlayer(int slotId)
        {
            return slotId == 2
                ? CreateDefaultPlayerTwo()
                : CreateDefaultPlayerOne();
        }
    }
}
