using NUnit.Framework;
using ProjectPVP.Input;
using UnityEngine;

namespace ProjectPVP.Tests.Editor
{
    public sealed class AiArenaProjectileSnapshotBuilderTests
    {
        [Test]
        public void Build_UsesTypedSnapshotSourceBeforeReflectionFallback()
        {
            GameObject root = new GameObject("TypedProjectileSnapshotSource");
            TypedProjectileSource source = root.AddComponent<TypedProjectileSource>();

            try
            {
                AiArenaProjectileSnapshot snapshot = AiArenaProjectileSnapshotBuilder.Build(source);

                Assert.That(snapshot.isValid, Is.True);
                Assert.That(snapshot.sourceSlotId, Is.EqualTo(9));
                Assert.That(snapshot.position, Is.EqualTo(new Vector2(5f, 6f)));
                Assert.That(snapshot.isCollectible, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Build_FallsBackToLegacyProjectileReflection()
        {
            GameObject root = new GameObject("LegacyProjectileSnapshotSource");
            LegacyProjectileSource source = root.AddComponent<LegacyProjectileSource>();

            try
            {
                root.transform.position = new Vector3(2f, 3f, 0f);
                source.currentVelocity = new Vector2(4f, 0f);

                AiArenaProjectileSnapshot snapshot = AiArenaProjectileSnapshotBuilder.Build(source);

                Assert.That(snapshot.isValid, Is.True);
                Assert.That(snapshot.position, Is.EqualTo(new Vector2(2f, 3f)));
                Assert.That(snapshot.velocity, Is.EqualTo(new Vector2(4f, 0f)));
                Assert.That(snapshot.travelDirection, Is.EqualTo(Vector2.right));
                Assert.That(snapshot.isCollectible, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Build_ReturnsDefaultForMissingProjectile()
        {
            AiArenaProjectileSnapshot snapshot = AiArenaProjectileSnapshotBuilder.Build(null);

            Assert.That(snapshot.isValid, Is.False);
            Assert.That(snapshot.sourceSlotId, Is.Zero);
            Assert.That(snapshot.position, Is.EqualTo(Vector2.zero));
        }

        private sealed class TypedProjectileSource : MonoBehaviour, IAiArenaProjectileSnapshotSource
        {
            public AiArenaProjectileSnapshot BuildAiArenaProjectileSnapshot()
            {
                return new AiArenaProjectileSnapshot
                {
                    isValid = true,
                    sourceSlotId = 9,
                    position = new Vector2(5f, 6f),
                    isCollectible = true,
                };
            }
        }

        private sealed class LegacyProjectileSource : MonoBehaviour
        {
            public Vector2 currentVelocity;

            public Vector2 CurrentVelocity => currentVelocity;
        }
    }
}
