using NUnit.Framework;
using ProjectPVP.Input;
using UnityEngine;

namespace ProjectPVP.Tests.Editor
{
    public sealed class AiArenaSelfSnapshotResolverTests
    {
        [Test]
        public void Resolve_BuildsSnapshotFromSourceOwnedByOwner()
        {
            GameObject ownerRoot = new GameObject("SelfOwner");
            GameObject otherRoot = new GameObject("OtherOwner");
            SnapshotSourceController self = ownerRoot.AddComponent<SnapshotSourceController>();
            SnapshotSourceController other = otherRoot.AddComponent<SnapshotSourceController>();

            try
            {
                ownerRoot.transform.position = new Vector3(6f, 7f, 0f);
                self.displayName = "Self";
                other.displayName = "Other";

                AiArenaControllerSnapshot snapshot = AiArenaSelfSnapshotResolver.Resolve(
                    new MonoBehaviour[] { other, null, self },
                    ownerRoot,
                    2);

                Assert.That(snapshot.isValid, Is.True);
                Assert.That(snapshot.slotId, Is.EqualTo(2));
                Assert.That(snapshot.displayName, Is.EqualTo("Self"));
                Assert.That(snapshot.position, Is.EqualTo(new Vector2(6f, 7f)));
            }
            finally
            {
                Object.DestroyImmediate(ownerRoot);
                Object.DestroyImmediate(otherRoot);
            }
        }

        [Test]
        public void Resolve_ReturnsDefaultWhenOwnerHasNoControllerSource()
        {
            GameObject ownerRoot = new GameObject("MissingSelfOwner");
            GameObject otherRoot = new GameObject("OtherOwner");
            SnapshotSourceController other = otherRoot.AddComponent<SnapshotSourceController>();

            try
            {
                AiArenaControllerSnapshot snapshot = AiArenaSelfSnapshotResolver.Resolve(
                    new MonoBehaviour[] { other },
                    ownerRoot,
                    1);

                Assert.That(snapshot.isValid, Is.False);
                Assert.That(snapshot.slotId, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(ownerRoot);
                Object.DestroyImmediate(otherRoot);
            }
        }

        [Test]
        public void Resolve_ReturnsDefaultWhenSourcesOrOwnerAreMissing()
        {
            GameObject ownerRoot = new GameObject("Owner");

            try
            {
                AiArenaControllerSnapshot missingSources = AiArenaSelfSnapshotResolver.Resolve(null, ownerRoot, 1);
                AiArenaControllerSnapshot missingOwner = AiArenaSelfSnapshotResolver.Resolve(new MonoBehaviour[0], null, 1);

                Assert.That(missingSources.isValid, Is.False);
                Assert.That(missingOwner.isValid, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(ownerRoot);
            }
        }

        private sealed class SnapshotSourceController : MonoBehaviour, IAiArenaControllerSnapshotSource
        {
            public string displayName;

            public AiArenaControllerSnapshot BuildAiArenaControllerSnapshot(int fallbackSlotId, Vector2 fallbackPosition)
            {
                return new AiArenaControllerSnapshot
                {
                    isValid = true,
                    slotId = fallbackSlotId,
                    displayName = displayName,
                    position = fallbackPosition,
                };
            }
        }
    }
}
