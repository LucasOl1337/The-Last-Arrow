using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ProjectPVP.Characters;
using ProjectPVP.Data;
using ProjectPVP.Match;
using UnityEngine;

namespace ProjectPVP.Tests.Editor
{
    public sealed class ProjectPvpPlayableValidatorTests
    {
        private static readonly MethodInfo ValidateRosterMethod =
            typeof(ProjectPVP.Editor.ProjectPvpPlayableValidator).GetMethod("ValidateRoster", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo MatchRosterField =
            typeof(MatchController).GetField("roster", BindingFlags.Instance | BindingFlags.NonPublic);

        [Test]
        public void ValidateRoster_AcceptsSelectedCharacterOnlySlots_WithoutCatalogOrExplicitPlayerControllers()
        {
            Assert.That(ValidateRosterMethod, Is.Not.Null);
            Assert.That(MatchRosterField, Is.Not.Null);

            GameObject matchRoot = new GameObject("PlayableValidatorTests");
            CharacterDefinition slotOneDefinition = ScriptableObject.CreateInstance<CharacterDefinition>();
            CharacterDefinition slotTwoDefinition = ScriptableObject.CreateInstance<CharacterDefinition>();

            try
            {
                MatchController matchController = matchRoot.AddComponent<MatchController>();
                MatchRoster roster = new MatchRoster();
                roster.EnsureDefaults();

                slotOneDefinition.id = "mizu";
                slotOneDefinition.displayName = "Mizu";
                slotTwoDefinition.id = "storm_dragon";
                slotTwoDefinition.displayName = "Storm Dragon";

                CombatantSlotConfig slotOne = roster.GetSlot(CombatantSlotId.SlotOne);
                CombatantSlotConfig slotTwo = roster.GetSlot(CombatantSlotId.SlotTwo);
                slotOne.playerProfile = null;
                slotOne.characterProfile = null;
                slotOne.selectedCharacter = slotOneDefinition;
                slotTwo.playerProfile = null;
                slotTwo.characterProfile = null;
                slotTwo.selectedCharacter = slotTwoDefinition;

                matchController.characterCatalog = null;
                MatchRosterField.SetValue(matchController, roster);

                var issues = new List<string>();
                ValidateRosterMethod.Invoke(null, new object[] { matchController, issues });

                Assert.That(issues, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(matchRoot);
                Object.DestroyImmediate(slotOneDefinition);
                Object.DestroyImmediate(slotTwoDefinition);
            }
        }
    }
}
