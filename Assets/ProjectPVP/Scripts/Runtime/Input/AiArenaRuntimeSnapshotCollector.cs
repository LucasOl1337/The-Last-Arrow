using System.Collections.Generic;
using UnityEngine;

namespace ProjectPVP.Input
{
    internal sealed class AiArenaRuntimeSnapshotCollector
    {
        private readonly List<MonoBehaviour> _controllers = new List<MonoBehaviour>(4);
        private float _controllerRefreshLeft;

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            _controllerRefreshLeft = Mathf.Max(0f, _controllerRefreshLeft - deltaTime);
        }

        public void ForceRefresh()
        {
            _controllerRefreshLeft = 0f;
            _controllers.Clear();
        }

        public void RefreshControllersIfNeeded()
        {
            if (_controllerRefreshLeft > 0f && _controllers.Count > 0)
            {
                return;
            }

            _controllers.Clear();
            MonoBehaviour[] behaviours = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            for (int index = 0; index < behaviours.Length; index += 1)
            {
                MonoBehaviour behaviour = behaviours[index];
                if (behaviour == null || behaviour.GetType().Name != "PlayerController")
                {
                    continue;
                }

                _controllers.Add(behaviour);
            }

            _controllerRefreshLeft = 0.5f;
        }

        public AiArenaControllerSnapshot ResolveSelfSnapshot(GameObject owner, int fallbackSlotId)
        {
            MonoBehaviour selfController = null;
            for (int index = 0; index < _controllers.Count; index += 1)
            {
                MonoBehaviour controller = _controllers[index];
                if (controller == null || controller.gameObject != owner)
                {
                    continue;
                }

                selfController = controller;
                break;
            }

            return BuildSnapshot(selfController, fallbackSlotId, owner != null ? (Vector2)owner.transform.position : Vector2.zero);
        }

        public AiArenaControllerSnapshot ResolveClosestOpponentSnapshot(AiArenaControllerSnapshot self)
        {
            AiArenaControllerSnapshot resolved = default;
            float bestDistance = float.MaxValue;

            for (int index = 0; index < _controllers.Count; index += 1)
            {
                AiArenaControllerSnapshot candidate = BuildSnapshot(_controllers[index], self.slotId, self.position);
                if (!candidate.isValid || candidate.slotId == self.slotId || candidate.isDead)
                {
                    continue;
                }

                float distance = Vector2.SqrMagnitude(candidate.position - self.position);
                if (distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                resolved = candidate;
            }

            return resolved;
        }

        public List<AiArenaProjectileSnapshot> ResolveProjectileSnapshots(AiArenaControllerSnapshot self)
        {
            var projectiles = new List<AiArenaProjectileSnapshot>(8);
            MonoBehaviour[] behaviours = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            for (int index = 0; index < behaviours.Length; index += 1)
            {
                MonoBehaviour behaviour = behaviours[index];
                if (behaviour == null || behaviour.GetType().Name != "ProjectileController")
                {
                    continue;
                }

                AiArenaProjectileSnapshot projectile = BuildProjectileSnapshot(behaviour);
                if (!projectile.isValid || projectile.sourceSlotId == self.slotId)
                {
                    continue;
                }

                projectiles.Add(projectile);
            }

            return projectiles;
        }

        public AiArenaArenaSnapshot ResolveArenaSnapshot()
        {
            MonoBehaviour[] behaviours = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            for (int index = 0; index < behaviours.Length; index += 1)
            {
                MonoBehaviour behaviour = behaviours[index];
                if (behaviour == null || behaviour.GetType().Name != "MatchController")
                {
                    continue;
                }

                return new AiArenaArenaSnapshot
                {
                    wrapBounds = ReadRectProperty(behaviour, "ActiveWrapBounds", new Rect(-1280f, -720f, 2560f, 1440f)),
                    roundResetPending = ReadBoolProperty(behaviour, "IsRoundResetPending", false),
                };
            }

            return new AiArenaArenaSnapshot
            {
                wrapBounds = new Rect(-1280f, -720f, 2560f, 1440f),
                roundResetPending = false,
            };
        }

        private static AiArenaControllerSnapshot BuildSnapshot(MonoBehaviour controller, int fallbackSlotId, Vector2 fallbackPosition)
        {
            if (controller == null)
            {
                return default;
            }

            Vector2 position = controller.transform.position;
            int resolvedSlotId = ReadIntField(controller, "slotId", fallbackSlotId);
            float horizontalVelocity = ReadFloatProperty(controller, "HorizontalVelocity", 0f);
            float verticalVelocity = ReadFloatProperty(controller, "VerticalVelocity", 0f);

            return new AiArenaControllerSnapshot
            {
                isValid = true,
                slotId = resolvedSlotId,
                characterId = ReadStringField(controller, "characterDefinition", string.Empty),
                displayName = controller.name,
                actionKey = ReadStringProperty(controller, "CurrentVisualActionKey", string.Empty),
                isDead = ReadBoolProperty(controller, "IsDead", false),
                isGrounded = ReadBoolProperty(controller, "IsGrounded", true),
                isTouchingWall = ReadBoolProperty(controller, "IsTouchingWall", false),
                isDashing = ReadBoolProperty(controller, "IsDashing", false),
                isMeleeActive = ReadBoolProperty(controller, "IsMeleeActive", false),
                isShootAnimating = ReadBoolProperty(controller, "IsShootAnimating", false),
                isUltimateActive = ReadBoolProperty(controller, "IsUltimateActive", false),
                isHitStunned = ReadBoolProperty(controller, "IsHitStunned", false),
                canParryProjectile = ReadBoolProperty(controller, "CanParryProjectile", false),
                canBlockProjectiles = ReadBoolProperty(controller, "CanBlockProjectileWithUltimate", false),
                arrows = ReadIntProperty(controller, "CurrentArrows", 0),
                facing = ReadIntProperty(controller, "Facing", position.x >= fallbackPosition.x ? 1 : -1),
                shootCooldownLeft = ReadFloatProperty(controller, "ShootCooldownLeft", 0f),
                meleeCooldownLeft = ReadFloatProperty(controller, "MeleeCooldownLeft", 0f),
                dashCooldownLeft = ReadFloatProperty(controller, "DashCooldownLeft", 0f),
                ultimateCooldownLeft = ReadFloatProperty(controller, "UltimateCooldownLeft", 0f),
                hitStunTimeLeft = ReadFloatProperty(controller, "HitStunTimeLeft", 0f),
                position = ReadVector2Property(controller, "RootPosition", position),
                velocity = new Vector2(horizontalVelocity, verticalVelocity),
                meleeHitboxCenter = ReadVector2Property(controller, "MeleeHitboxCenter", position),
                meleeHitboxSize = ReadVector2Property(controller, "MeleeHitboxSize", Vector2.zero),
                ultimateHitboxCenter = ReadVector2Property(controller, "UltimateHitboxCenter", position),
                ultimateHitboxRadius = ReadFloatProperty(controller, "UltimateHitboxRadius", 0f),
            };
        }

        private static AiArenaProjectileSnapshot BuildProjectileSnapshot(MonoBehaviour projectile)
        {
            if (projectile == null)
            {
                return default;
            }

            GameObject sourceObject = ReadGameObjectProperty(projectile, "SourceObject", null);
            int sourceSlotId = 0;
            if (sourceObject != null)
            {
                MonoBehaviour[] sourceBehaviours = sourceObject.GetComponents<MonoBehaviour>();
                for (int index = 0; index < sourceBehaviours.Length; index += 1)
                {
                    MonoBehaviour behaviour = sourceBehaviours[index];
                    if (behaviour == null || behaviour.GetType().Name != "PlayerController")
                    {
                        continue;
                    }

                    sourceSlotId = ReadIntField(behaviour, "slotId", 0);
                    break;
                }
            }

            Vector2 position = projectile.transform.position;
            Vector2 velocity = ReadVector2Property(projectile, "CurrentVelocity", Vector2.zero);
            return new AiArenaProjectileSnapshot
            {
                isValid = true,
                sourceSlotId = sourceSlotId,
                isStuck = ReadBoolProperty(projectile, "IsStuck", false),
                isDisarmed = ReadBoolProperty(projectile, "IsDisarmed", false),
                position = position,
                velocity = velocity,
                travelDirection = ReadVector2Property(projectile, "TravelDirection", velocity.sqrMagnitude > 0.001f ? velocity.normalized : Vector2.right),
            };
        }

        private static bool ReadBoolProperty(MonoBehaviour behaviour, string propertyName, bool fallback)
        {
            try
            {
                var property = behaviour.GetType().GetProperty(propertyName);
                return property != null && property.PropertyType == typeof(bool)
                    ? (bool)property.GetValue(behaviour)
                    : fallback;
            }
            catch
            {
                return fallback;
            }
        }

        private static int ReadIntProperty(MonoBehaviour behaviour, string propertyName, int fallback)
        {
            try
            {
                var property = behaviour.GetType().GetProperty(propertyName);
                return property != null && property.PropertyType == typeof(int)
                    ? (int)property.GetValue(behaviour)
                    : fallback;
            }
            catch
            {
                return fallback;
            }
        }

        private static int ReadIntField(MonoBehaviour behaviour, string fieldName, int fallback)
        {
            try
            {
                var field = behaviour.GetType().GetField(fieldName);
                return field != null && field.FieldType == typeof(int)
                    ? (int)field.GetValue(behaviour)
                    : fallback;
            }
            catch
            {
                return fallback;
            }
        }

        private static string ReadStringField(MonoBehaviour behaviour, string fieldName, string fallback)
        {
            try
            {
                var field = behaviour.GetType().GetField(fieldName);
                if (field == null)
                {
                    return fallback;
                }

                object value = field.GetValue(behaviour);
                if (value == null)
                {
                    return fallback;
                }

                var idField = value.GetType().GetField("id");
                if (idField != null && idField.FieldType == typeof(string))
                {
                    return (string)idField.GetValue(value) ?? fallback;
                }

                return value.ToString();
            }
            catch
            {
                return fallback;
            }
        }

        private static float ReadFloatProperty(MonoBehaviour behaviour, string propertyName, float fallback)
        {
            try
            {
                var property = behaviour.GetType().GetProperty(propertyName);
                return property != null && property.PropertyType == typeof(float)
                    ? (float)property.GetValue(behaviour)
                    : fallback;
            }
            catch
            {
                return fallback;
            }
        }

        private static string ReadStringProperty(MonoBehaviour behaviour, string propertyName, string fallback)
        {
            try
            {
                var property = behaviour.GetType().GetProperty(propertyName);
                return property != null && property.PropertyType == typeof(string)
                    ? (string)property.GetValue(behaviour)
                    : fallback;
            }
            catch
            {
                return fallback;
            }
        }

        private static Rect ReadRectProperty(MonoBehaviour behaviour, string propertyName, Rect fallback)
        {
            try
            {
                var property = behaviour.GetType().GetProperty(propertyName);
                return property != null && property.PropertyType == typeof(Rect)
                    ? (Rect)property.GetValue(behaviour)
                    : fallback;
            }
            catch
            {
                return fallback;
            }
        }

        private static Vector2 ReadVector2Property(MonoBehaviour behaviour, string propertyName, Vector2 fallback)
        {
            try
            {
                var property = behaviour.GetType().GetProperty(propertyName);
                return property != null && property.PropertyType == typeof(Vector2)
                    ? (Vector2)property.GetValue(behaviour)
                    : fallback;
            }
            catch
            {
                return fallback;
            }
        }

        private static GameObject ReadGameObjectProperty(MonoBehaviour behaviour, string propertyName, GameObject fallback)
        {
            try
            {
                var property = behaviour.GetType().GetProperty(propertyName);
                return property != null && property.PropertyType == typeof(GameObject)
                    ? (GameObject)property.GetValue(behaviour)
                    : fallback;
            }
            catch
            {
                return fallback;
            }
        }
    }

    public struct AiArenaControllerSnapshot
    {
        public bool isValid;
        public int slotId;
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

    public struct AiArenaArenaSnapshot
    {
        public Rect wrapBounds;
        public bool roundResetPending;
    }
}
