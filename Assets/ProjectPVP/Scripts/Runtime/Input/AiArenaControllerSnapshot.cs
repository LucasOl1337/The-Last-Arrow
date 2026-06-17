using UnityEngine;

namespace ProjectPVP.Input
{
    public struct AiArenaControllerSnapshot
    {
        public bool isValid;
        public int slotId;
        public string botId;
        public string botDisplayName;
        public string characterId;
        public string displayName;
        public string actionKey;
        public bool isDead;
        public bool isGrounded;
        public bool isTouchingWall;
        public bool isDashing;
        public bool isMeleeActive;
        public bool isShootAnimating;
        public bool isUltimateActive;
        public bool isHitStunned;
        public bool canParryProjectile;
        public bool canBlockProjectiles;
        public int arrows;
        public int facing;
        public float projectileInheritVelocityFactor;
        public float projectileBaseSpeed;
        public float projectileGravity;
        public float shootCooldownLeft;
        public float meleeCooldownLeft;
        public float dashCooldownLeft;
        public float ultimateCooldownLeft;
        public float hitStunTimeLeft;
        public Vector2 position;
        public Vector2 velocity;
        public Vector2 meleeHitboxCenter;
        public Vector2 meleeHitboxSize;
        public Vector2 ultimateHitboxCenter;
        public float ultimateHitboxRadius;
    }
}
