using UnityEngine;

namespace ProjectPVP.Input
{
    public struct AiArenaProjectileSnapshot
    {
        public bool isValid;
        public int sourceSlotId;
        public bool isStuck;
        public bool isDisarmed;
        public Vector2 position;
        public Vector2 velocity;
        public Vector2 travelDirection;
    }
}
