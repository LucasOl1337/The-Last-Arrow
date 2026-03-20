using UnityEngine;

namespace ProjectPVP.Gameplay
{
    public enum CombatShapeKind
    {
        Circle = 0,
        Box = 1,
        Capsule = 2,
    }

    public struct CombatShapeSnapshot
    {
        public CombatShapeKind shapeKind;
        public Vector2 center;
        public Vector2 size;
        public float radius;
        public float angle;
        public CapsuleDirection2D capsuleDirection;

        public CombatShapeSnapshot Translate(Vector2 offset)
        {
            center += offset;
            return this;
        }
    }
}
