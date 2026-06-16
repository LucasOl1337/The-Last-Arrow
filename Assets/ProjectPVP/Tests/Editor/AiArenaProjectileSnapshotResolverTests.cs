using System.Collections.Generic;
using NUnit.Framework;
using ProjectPVP.Input;
using UnityEngine;

namespace ProjectPVP.Tests.Editor
{
    public sealed class AiArenaProjectileSnapshotResolverTests
    {
        [Test]
        public void Resolve_ReturnsOnlyValidProjectilesFromOtherSlots()
        {
            GameObject enemyRoot = new GameObject("EnemyProjectile");
            GameObject ownRoot = new GameObject("OwnProjectile");
            GameObject invalidRoot = new GameObject("InvalidProjectile");
            SnapshotSourceProjectile enemy = enemyRoot.AddComponent<SnapshotSourceProjectile>();
            SnapshotSourceProjectile own = ownRoot.AddComponent<SnapshotSourceProjectile>();
            SnapshotSourceProjectile invalid = invalidRoot.AddComponent<SnapshotSourceProjectile>();

            try
            {
                enemy.sourceSlotId = 2;
                enemy.position = new Vector2(5f, 1f);
                own.sourceSlotId = 1;
                own.position = new Vector2(2f, 1f);
                invalid.sourceSlotId = 3;
                invalid.position = new Vector2(8f, 1f);
                invalid.isValid = false;

                AiArenaControllerSnapshot self = new AiArenaControllerSnapshot
                {
                    isValid = true,
                    slotId = 1,
                };

                List<AiArenaProjectileSnapshot> projectiles = AiArenaProjectileSnapshotResolver.Resolve(
                    new MonoBehaviour[] { own, null, invalid, enemy },
                    self);

                Assert.That(projectiles, Has.Count.EqualTo(1));
                Assert.That(projectiles[0].sourceSlotId, Is.EqualTo(2));
                Assert.That(projectiles[0].position, Is.EqualTo(new Vector2(5f, 1f)));
            }
            finally
            {
                Object.DestroyImmediate(enemyRoot);
                Object.DestroyImmediate(ownRoot);
                Object.DestroyImmediate(invalidRoot);
            }
        }

        [Test]
        public void Resolve_PreservesProjectileOrderAfterFiltering()
        {
            GameObject firstRoot = new GameObject("FirstProjectile");
            GameObject secondRoot = new GameObject("SecondProjectile");
            SnapshotSourceProjectile first = firstRoot.AddComponent<SnapshotSourceProjectile>();
            SnapshotSourceProjectile second = secondRoot.AddComponent<SnapshotSourceProjectile>();

            try
            {
                first.sourceSlotId = 2;
                first.position = new Vector2(1f, 0f);
                second.sourceSlotId = 3;
                second.position = new Vector2(2f, 0f);

                AiArenaControllerSnapshot self = new AiArenaControllerSnapshot
                {
                    isValid = true,
                    slotId = 1,
                };

                List<AiArenaProjectileSnapshot> projectiles = AiArenaProjectileSnapshotResolver.Resolve(
                    new MonoBehaviour[] { first, second },
                    self);

                Assert.That(projectiles, Has.Count.EqualTo(2));
                Assert.That(projectiles[0].sourceSlotId, Is.EqualTo(2));
                Assert.That(projectiles[1].sourceSlotId, Is.EqualTo(3));
            }
            finally
            {
                Object.DestroyImmediate(firstRoot);
                Object.DestroyImmediate(secondRoot);
            }
        }

        [Test]
        public void Resolve_ReturnsEmptyListForMissingSources()
        {
            AiArenaControllerSnapshot self = new AiArenaControllerSnapshot
            {
                isValid = true,
                slotId = 1,
            };

            List<AiArenaProjectileSnapshot> projectiles = AiArenaProjectileSnapshotResolver.Resolve(null, self);

            Assert.That(projectiles, Is.Empty);
        }

        private sealed class SnapshotSourceProjectile : MonoBehaviour, IAiArenaProjectileSnapshotSource
        {
            public bool isValid = true;
            public int sourceSlotId;
            public Vector2 position;

            public AiArenaProjectileSnapshot BuildAiArenaProjectileSnapshot()
            {
                return new AiArenaProjectileSnapshot
                {
                    isValid = isValid,
                    sourceSlotId = sourceSlotId,
                    position = position,
                };
            }
        }
    }
}
