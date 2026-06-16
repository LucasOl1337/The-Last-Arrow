using NUnit.Framework;
using ProjectPVP.Input;
using UnityEngine;

namespace ProjectPVP.Tests.Editor
{
    public sealed class AiArenaControllerSnapshotBuilderTests
    {
        [Test]
        public void Build_UsesTypedSnapshotSourceBeforeReflectionFallback()
        {
            GameObject root = new GameObject("TypedControllerSnapshotSource");
            TypedControllerSource source = root.AddComponent<TypedControllerSource>();

            try
            {
                AiArenaControllerSnapshot snapshot = AiArenaControllerSnapshotBuilder.Build(
                    source,
                    2,
                    new Vector2(10f, 4f));

                Assert.That(snapshot.isValid, Is.True);
                Assert.That(snapshot.slotId, Is.EqualTo(12));
                Assert.That(snapshot.position, Is.EqualTo(new Vector2(10f, 4f)));
                Assert.That(snapshot.displayName, Is.EqualTo("Typed"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Build_FallsBackToLegacyControllerReflection()
        {
            GameObject root = new GameObject("LegacyControllerSnapshotSource");
            LegacyControllerSource source = root.AddComponent<LegacyControllerSource>();

            try
            {
                root.transform.position = new Vector3(3f, 4f, 0f);
                source.slotId = 7;
                source.currentArrows = 3;

                AiArenaControllerSnapshot snapshot = AiArenaControllerSnapshotBuilder.Build(
                    source,
                    1,
                    Vector2.zero);

                Assert.That(snapshot.isValid, Is.True);
                Assert.That(snapshot.slotId, Is.EqualTo(7));
                Assert.That(snapshot.arrows, Is.EqualTo(3));
                Assert.That(snapshot.position, Is.EqualTo(new Vector2(3f, 4f)));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Build_ReturnsDefaultForMissingController()
        {
            AiArenaControllerSnapshot snapshot = AiArenaControllerSnapshotBuilder.Build(
                null,
                1,
                Vector2.one);

            Assert.That(snapshot.isValid, Is.False);
            Assert.That(snapshot.slotId, Is.Zero);
            Assert.That(snapshot.position, Is.EqualTo(Vector2.zero));
        }

        private sealed class TypedControllerSource : MonoBehaviour, IAiArenaControllerSnapshotSource
        {
            public AiArenaControllerSnapshot BuildAiArenaControllerSnapshot(int fallbackSlotId, Vector2 fallbackPosition)
            {
                return new AiArenaControllerSnapshot
                {
                    isValid = true,
                    slotId = fallbackSlotId + 10,
                    displayName = "Typed",
                    position = fallbackPosition,
                };
            }
        }

        private sealed class LegacyControllerSource : MonoBehaviour
        {
            public int slotId;
            public int currentArrows;

            public int CurrentArrows => currentArrows;
            public Vector2 RootPosition => transform.position;
        }
    }
}
