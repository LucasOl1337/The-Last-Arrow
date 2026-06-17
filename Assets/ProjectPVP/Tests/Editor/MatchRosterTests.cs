using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ProjectPVP.Gameplay;
using ProjectPVP.Match;
using UnityEngine;

namespace ProjectPVP.Tests.Editor
{
    public sealed class MatchRosterTests
    {
        private static readonly FieldInfo SlotsField =
            typeof(MatchRoster).GetField("slots", BindingFlags.Instance | BindingFlags.NonPublic);

        [Test]
        public void EnsureDefaults_DeduplicatesSerializedSlotsAndPreservesFirstUsefulData()
        {
            Assert.That(SlotsField, Is.Not.Null);

            GameObject slotOneRoot = new GameObject("MatchRosterSlotOneController");
            GameObject slotTwoRoot = new GameObject("MatchRosterSlotTwoController");

            try
            {
                PlayerController slotOneController = CreatePlayer(slotOneRoot);
                PlayerController slotTwoController = CreatePlayer(slotTwoRoot);

                MatchRoster roster = new MatchRoster();
                var serializedSlots = new List<CombatantSlotConfig>
                {
                    new CombatantSlotConfig
                    {
                        slotId = CombatantSlotId.SlotOne,
                        displayName = "First Slot One",
                    },
                    new CombatantSlotConfig
                    {
                        slotId = CombatantSlotId.SlotTwo,
                        controller = slotTwoController,
                        displayName = "Slot Two",
                    },
                    new CombatantSlotConfig
                    {
                        slotId = CombatantSlotId.SlotOne,
                        controller = slotOneController,
                        displayName = "Duplicate Slot One",
                    },
                    new CombatantSlotConfig
                    {
                        slotId = CombatantSlotId.SlotTwo,
                        displayName = "Duplicate Slot Two",
                    },
                };

                SlotsField.SetValue(roster, serializedSlots);
                roster.EnsureDefaults();

                IReadOnlyList<CombatantSlotConfig> slots = roster.Slots;

                Assert.That(slots, Has.Count.EqualTo(2));
                Assert.That(slots[0].slotId, Is.EqualTo(CombatantSlotId.SlotOne));
                Assert.That(slots[0].controller, Is.SameAs(slotOneController));
                Assert.That(slots[0].displayName, Is.EqualTo("First Slot One"));
                Assert.That(slots[1].slotId, Is.EqualTo(CombatantSlotId.SlotTwo));
                Assert.That(slots[1].controller, Is.SameAs(slotTwoController));
                Assert.That(slots[1].displayName, Is.EqualTo("Slot Two"));
            }
            finally
            {
                Object.DestroyImmediate(slotOneRoot);
                Object.DestroyImmediate(slotTwoRoot);
            }
        }

        private static PlayerController CreatePlayer(GameObject root)
        {
            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
            PlayerController controller = root.AddComponent<PlayerController>();
            controller.body = body;
            controller.bodyCollider = collider;
            return controller;
        }
    }
}
