using ProjectPVP.Input;
using UnityEngine;

namespace ProjectPVP.AI
{
    public static class AiActionSanitizer
    {
        public static PlayerInputFrame ToPlayerInputFrame(AiFrameAction action, int frame)
        {
            if (action == null)
            {
                action = new AiFrameAction();
            }

            float axis = Mathf.Clamp(action.axis, -1f, 1f);
            if (Mathf.Abs(axis) < 0.01f)
            {
                if (action.left && !action.right)
                {
                    axis = -1f;
                }
                else if (action.right && !action.left)
                {
                    axis = 1f;
                }
            }

            bool left = action.left || axis < -0.1f;
            bool right = action.right || axis > 0.1f;
            if (left && right)
            {
                if (axis < -0.1f)
                {
                    right = false;
                }
                else if (axis > 0.1f)
                {
                    left = false;
                }
                else
                {
                    left = false;
                    right = false;
                }
            }

            bool up = action.up;
            bool down = action.down;
            if (up && down)
            {
                up = false;
                down = false;
            }

            Vector2 aim = new Vector2(action.aimX, action.aimY);
            if (aim.sqrMagnitude > 1f)
            {
                aim.Normalize();
            }

            return new PlayerInputFrame
            {
                frame = frame,
                axis = axis,
                aim = aim,
                left = left,
                right = right,
                up = up,
                down = down,
                jumpPressed = action.jumpPressed,
                jumpHeld = action.jumpHeld,
                shootPressed = action.shootPressed,
                shootHeld = action.shootHeld,
                meleePressed = action.meleePressed,
                ultimatePressed = action.ultimatePressed,
                dashPrimaryPressed = action.dashPrimaryPressed,
                dashSecondaryPressed = action.dashSecondaryPressed,
            };
        }

        public static PlayerInputFrame ToContinuousFallback(PlayerInputFrame previousFrame, int frame)
        {
            previousFrame.frame = frame;
            previousFrame.jumpPressed = false;
            previousFrame.shootPressed = false;
            previousFrame.meleePressed = false;
            previousFrame.ultimatePressed = false;
            previousFrame.dashPrimaryPressed = false;
            previousFrame.dashSecondaryPressed = false;
            return previousFrame;
        }
    }
}
