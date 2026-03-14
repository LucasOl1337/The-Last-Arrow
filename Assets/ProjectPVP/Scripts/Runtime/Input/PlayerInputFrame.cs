using System;
using UnityEngine;

namespace ProjectPVP.Input
{
    [Serializable]
    public struct PlayerInputFrame
    {
        public int frame;
        public float axis;
        public Vector2 aim;
        public bool left;
        public bool right;
        public bool up;
        public bool down;
        public bool jumpPressed;
        public bool jumpHeld;
        public bool shootPressed;
        public bool shootHeld;
        public bool meleePressed;
        public bool ultimatePressed;
        public bool dashPrimaryPressed;
        public bool dashSecondaryPressed;

        public Vector2 Movement
        {
            get
            {
                return new Vector2(axis, (up ? 1f : 0f) - (down ? 1f : 0f));
            }
        }
    }
}
