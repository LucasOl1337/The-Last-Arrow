using NUnit.Framework;
using ProjectPVP.Input;
using UnityEngine;

namespace ProjectPVP.Tests.Editor
{
    public sealed class AiArenaOpponentSnapshotSelectorTests
    {
        [Test]
        public void SelectClosest_ReturnsNearestValidLivingDifferentSlot()
        {
            GameObject farRoot = new GameObject("FarOpponent");
            GameObject nearRoot = new GameObject("NearOpponent");
            GameObject sameSlotRoot = new GameObject("SameSlotSource");
            GameObject deadRoot = new GameObject("DeadOpponent");
            GameObject invalidRoot = new GameObject("InvalidOpponent");
            SnapshotSourceController far = farRoot.AddComponent<SnapshotSourceController>();
            SnapshotSourceController near = nearRoot.AddComponent<SnapshotSourceController>();
            SnapshotSourceController sameSlot = sameSlotRoot.AddComponent<SnapshotSourceController>();
            SnapshotSourceController dead = deadRoot.AddComponent<SnapshotSourceController>();
            SnapshotSourceController invalid = invalidRoot.AddComponent<SnapshotSourceController>();

            try
            {
                far.slotId = 2;
                far.position = new Vector2(12f, 0f);
                far.displayName = "Far";
                near.slotId = 3;
                near.position = new Vector2(3f, 0f);
                near.displayName = "Near";
                sameSlot.slotId = 1;
                sameSlot.position = new Vector2(1f, 0f);
                dead.slotId = 4;
                dead.position = new Vector2(0.5f, 0f);
                dead.isDead = true;
                invalid.slotId = 5;
                invalid.position = new Vector2(0.25f, 0f);
                invalid.isValid = false;

                AiArenaControllerSnapshot self = new AiArenaControllerSnapshot
                {
                    isValid = true,
                    slotId = 1,
                    position = Vector2.zero,
                };

                AiArenaControllerSnapshot selected = AiArenaOpponentSnapshotSelector.SelectClosest(
                    new MonoBehaviour[] { far, null, sameSlot, dead, invalid, near },
                    self);

                Assert.That(selected.isValid, Is.True);
                Assert.That(selected.slotId, Is.EqualTo(3));
                Assert.That(selected.displayName, Is.EqualTo("Near"));
                Assert.That(selected.position, Is.EqualTo(new Vector2(3f, 0f)));
            }
            finally
            {
                Object.DestroyImmediate(farRoot);
                Object.DestroyImmediate(nearRoot);
                Object.DestroyImmediate(sameSlotRoot);
                Object.DestroyImmediate(deadRoot);
                Object.DestroyImmediate(invalidRoot);
            }
        }

        [Test]
        public void SelectClosest_KeepsFirstValidCandidateWhenDistancesTie()
        {
            GameObject firstRoot = new GameObject("FirstOpponent");
            GameObject secondRoot = new GameObject("SecondOpponent");
            SnapshotSourceController first = firstRoot.AddComponent<SnapshotSourceController>();
            SnapshotSourceController second = secondRoot.AddComponent<SnapshotSourceController>();

            try
            {
                first.slotId = 2;
                first.position = new Vector2(-4f, 0f);
                first.displayName = "First";
                second.slotId = 3;
                second.position = new Vector2(4f, 0f);
                second.displayName = "Second";

                AiArenaControllerSnapshot self = new AiArenaControllerSnapshot
                {
                    isValid = true,
                    slotId = 1,
                    position = Vector2.zero,
                };

                AiArenaControllerSnapshot selected = AiArenaOpponentSnapshotSelector.SelectClosest(
                    new MonoBehaviour[] { first, second },
                    self);

                Assert.That(selected.slotId, Is.EqualTo(2));
                Assert.That(selected.displayName, Is.EqualTo("First"));
            }
            finally
            {
                Object.DestroyImmediate(firstRoot);
                Object.DestroyImmediate(secondRoot);
            }
        }

        [Test]
        public void SelectClosest_ReturnsDefaultWhenNoValidOpponentExists()
        {
            GameObject sameSlotRoot = new GameObject("SameSlotSource");
            SnapshotSourceController sameSlot = sameSlotRoot.AddComponent<SnapshotSourceController>();

            try
            {
                sameSlot.slotId = 1;
                sameSlot.position = Vector2.one;

                AiArenaControllerSnapshot self = new AiArenaControllerSnapshot
                {
                    isValid = true,
                    slotId = 1,
                    position = Vector2.zero,
                };

                AiArenaControllerSnapshot selected = AiArenaOpponentSnapshotSelector.SelectClosest(
                    new MonoBehaviour[] { null, sameSlot },
                    self);

                Assert.That(selected.isValid, Is.False);
                Assert.That(selected.slotId, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(sameSlotRoot);
            }
        }

        private sealed class SnapshotSourceController : MonoBehaviour, IAiArenaControllerSnapshotSource
        {
            public bool isValid = true;
            public bool isDead;
            public int slotId;
            public string displayName;
            public Vector2 position;

            public AiArenaControllerSnapshot BuildAiArenaControllerSnapshot(int fallbackSlotId, Vector2 fallbackPosition)
            {
                return new AiArenaControllerSnapshot
                {
                    isValid = isValid,
                    isDead = isDead,
                    slotId = slotId > 0 ? slotId : fallbackSlotId,
                    displayName = displayName,
                    position = position,
                };
            }
        }
    }
}
