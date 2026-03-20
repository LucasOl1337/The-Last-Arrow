using ProjectPVP.Data;
using UnityEngine;

namespace ProjectPVP.Gameplay
{
    internal static class ProjectileLauncher
    {
        public static ProjectileController Spawn(
            ProjectileController projectilePrefab,
            CharacterDefinition definition,
            GameObject sourceObject,
            Vector2 origin,
            Vector2 direction,
            Vector2 inheritedVelocity,
            float inheritFactor,
            Sprite overrideSprite,
            float scale)
        {
            if (projectilePrefab == null)
            {
                return null;
            }

            ProjectileController projectile = Object.Instantiate(projectilePrefab, origin, Quaternion.identity);
            if (definition != null)
            {
                projectile.ApplyDefinition(definition);
            }

            projectile.Launch(sourceObject, origin, direction, inheritedVelocity, inheritFactor, overrideSprite);
            projectile.transform.localScale = Vector3.one * Mathf.Max(0.01f, scale);
            return projectile;
        }
    }
}
