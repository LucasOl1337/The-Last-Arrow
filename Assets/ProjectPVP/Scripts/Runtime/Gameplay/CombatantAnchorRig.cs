using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectPVP.Gameplay
{
    [Serializable]
    public sealed class CombatantAnchorRig
    {
        public PlayerCombatAnchor spawnAnchor;
        public Transform projectileOrigin;
        public PlayerCombatAnchor meleeHitboxAnchor;
        public PlayerCombatAnchor ultimateHitboxAnchor;

        public void SyncFromLegacy(
            PlayerCombatAnchor legacySpawnAnchor,
            Transform legacyProjectileOrigin,
            PlayerCombatAnchor legacyMeleeHitboxAnchor,
            PlayerCombatAnchor legacyUltimateHitboxAnchor)
        {
            spawnAnchor ??= legacySpawnAnchor;
            projectileOrigin ??= legacyProjectileOrigin;
            meleeHitboxAnchor ??= legacyMeleeHitboxAnchor;
            ultimateHitboxAnchor ??= legacyUltimateHitboxAnchor;
        }

        public void SyncLegacy(
            ref PlayerCombatAnchor legacySpawnAnchor,
            ref Transform legacyProjectileOrigin,
            ref PlayerCombatAnchor legacyMeleeHitboxAnchor,
            ref PlayerCombatAnchor legacyUltimateHitboxAnchor)
        {
            legacySpawnAnchor = spawnAnchor != null ? spawnAnchor : legacySpawnAnchor;
            legacyProjectileOrigin = projectileOrigin != null ? projectileOrigin : legacyProjectileOrigin;
            legacyMeleeHitboxAnchor = meleeHitboxAnchor != null ? meleeHitboxAnchor : legacyMeleeHitboxAnchor;
            legacyUltimateHitboxAnchor = ultimateHitboxAnchor != null ? ultimateHitboxAnchor : legacyUltimateHitboxAnchor;
        }

        public IEnumerable<PlayerCombatAnchor> EnumerateAnchors()
        {
            if (spawnAnchor != null)
            {
                yield return spawnAnchor;
            }

            if (meleeHitboxAnchor != null)
            {
                yield return meleeHitboxAnchor;
            }

            if (ultimateHitboxAnchor != null)
            {
                yield return ultimateHitboxAnchor;
            }
        }
    }
}
