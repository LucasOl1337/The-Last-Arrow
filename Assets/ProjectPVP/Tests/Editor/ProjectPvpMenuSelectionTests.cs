using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ProjectPVP.Characters;
using ProjectPVP.Data;
using ProjectPVP.Gameplay;
using ProjectPVP.Input;
using ProjectPVP.Match;
using UnityEngine;

namespace ProjectPVP.Tests.Editor
{
    public sealed class ProjectPvpMenuSelectionTests
    {
        private static readonly FieldInfo MatchRosterField = typeof(MatchController).GetField("roster", BindingFlags.Instance | BindingFlags.NonPublic);

        [Test]
        public void BuildDefault_UsesModeDefaultsAndCurrentCharacterSelections()
        {
            CharacterBootstrapProfile mizu = CreateProfile("mizu", "Mizu");
            CharacterBootstrapProfile storm = CreateProfile("storm_dragon", "Storm Dragon");
            var slots = new List<CombatantSlotConfig>
            {
                new CombatantSlotConfig
                {
                    slotId = CombatantSlotId.SlotOne,
                    characterProfile = storm,
                },
                new CombatantSlotConfig
                {
                    slotId = CombatantSlotId.SlotTwo,
                    selectedCharacter = mizu.ResolveCharacterDefinition(),
                },
            };

            try
            {
                ProjectPvpMenuSelection selection = ProjectPvpMenuSelectionService.BuildDefault(
                    slots,
                    new[] { mizu, storm },
                    ProjectPvpMenuGameMode.HumanVsAi);

                Assert.That(selection.GameMode, Is.EqualTo(ProjectPvpMenuGameMode.HumanVsAi));
                Assert.That(selection.GetSlot(CombatantSlotId.SlotOne).AiEnabled, Is.False);
                Assert.That(selection.GetSlot(CombatantSlotId.SlotTwo).AiEnabled, Is.True);
                Assert.That(selection.GetSlot(CombatantSlotId.SlotOne).CharacterProfile, Is.SameAs(storm));
                Assert.That(selection.GetSlot(CombatantSlotId.SlotOne).CharacterIndex, Is.EqualTo(1));
                Assert.That(selection.GetSlot(CombatantSlotId.SlotTwo).CharacterProfile, Is.SameAs(mizu));
                Assert.That(selection.GetSlot(CombatantSlotId.SlotTwo).CharacterIndex, Is.Zero);
            }
            finally
            {
                DestroyProfile(mizu);
                DestroyProfile(storm);
            }
        }

        [Test]
        public void ApplyGameMode_RewritesAiFlagsForBothSlots()
        {
            ProjectPvpMenuSelection selection = ProjectPvpMenuSelectionService.BuildDefault(
                new[]
                {
                    new CombatantSlotConfig { slotId = CombatantSlotId.SlotOne },
                    new CombatantSlotConfig { slotId = CombatantSlotId.SlotTwo },
                },
                new CharacterBootstrapProfile[0],
                ProjectPvpMenuGameMode.Versus);

            ProjectPvpMenuSelectionService.ApplyGameMode(selection, ProjectPvpMenuGameMode.AiArena);

            Assert.That(selection.GetSlot(CombatantSlotId.SlotOne).AiEnabled, Is.True);
            Assert.That(selection.GetSlot(CombatantSlotId.SlotTwo).AiEnabled, Is.True);

            ProjectPvpMenuSelectionService.ApplyGameMode(selection, ProjectPvpMenuGameMode.Versus);

            Assert.That(selection.GetSlot(CombatantSlotId.SlotOne).AiEnabled, Is.False);
            Assert.That(selection.GetSlot(CombatantSlotId.SlotTwo).AiEnabled, Is.False);
        }

        [Test]
        public void CreateRuntimeControlProfile_PreservesBindingsAndSetsAiMode()
        {
            CombatantSlotProfile source = ScriptableObject.CreateInstance<CombatantSlotProfile>();

            try
            {
                source.displayName = "Slot 2";
                source.enableGamepad = true;
                source.preferredGamepadIndex = 1;

                CombatantSlotProfile runtime = ProjectPvpMenuSelectionService.CreateRuntimeControlProfile(
                    source,
                    CombatantSlotId.SlotTwo,
                    aiEnabled: true,
                    AiBrainKind.LocalHeuristic);

                Assert.That(runtime, Is.Not.Null);
                Assert.That(runtime, Is.Not.SameAs(source));
                Assert.That(runtime.ResolveControlMode(), Is.EqualTo(CombatantControlMode.AI));
                Assert.That(runtime.ResolveAiBrain(), Is.EqualTo(AiBrainKind.LocalHeuristic));
                Assert.That(runtime.enableGamepad, Is.True);
                Assert.That(runtime.preferredGamepadIndex, Is.EqualTo(1));

                Object.DestroyImmediate(runtime);
            }
            finally
            {
                Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void SetSlotBotControlEnabled_TogglesBackToHumanProfile()
        {
            Assert.That(MatchRosterField, Is.Not.Null);

            GameObject matchRoot = new GameObject("RuntimeControlToggleMatch");
            MatchController matchController = matchRoot.AddComponent<MatchController>();
            MatchRoster roster = new MatchRoster();
            roster.EnsureDefaults();

            CharacterBootstrapProfile mizu = CreateProfile("mizu", "Mizu");
            CombatantSlotProfile slotOneProfile = ScriptableObject.CreateInstance<CombatantSlotProfile>();
            PlayerController slotOneController = null;

            try
            {
                slotOneProfile.controlMode = CombatantControlMode.Human;
                slotOneController = CharacterBootstrapFactory.CreateCombatant(
                    mizu,
                    CombatantSlotId.SlotOne,
                    slotOneProfile,
                    null);

                CombatantSlotConfig slotOne = roster.GetSlot(CombatantSlotId.SlotOne);
                slotOne.controller = slotOneController;
                slotOne.characterProfile = mizu;
                slotOne.playerProfile = slotOneProfile;
                MatchRosterField.SetValue(matchController, roster);

                Assert.That(matchController.SetSlotBotControlEnabled(CombatantSlotId.SlotOne, true), Is.True);
                Assert.That(slotOne.playerProfile, Is.Not.SameAs(slotOneProfile));
                Assert.That(slotOne.playerProfile.ResolveControlMode(), Is.EqualTo(CombatantControlMode.AI));

                Assert.That(matchController.SetSlotBotControlEnabled(CombatantSlotId.SlotOne, false), Is.True);
                Assert.That(slotOne.playerProfile, Is.SameAs(slotOneProfile));
                Assert.That(slotOne.playerProfile.ResolveControlMode(), Is.EqualTo(CombatantControlMode.Human));
            }
            finally
            {
                Object.DestroyImmediate(matchRoot);
                if (slotOneController != null)
                {
                    Object.DestroyImmediate(slotOneController.gameObject);
                }

                DestroyProfile(mizu);
                Object.DestroyImmediate(slotOneProfile);
            }
        }

        [Test]
        public void ApplyMenuSelection_RebuildsChangedCharacterAndAppliesControlProfiles()
        {
            Assert.That(MatchRosterField, Is.Not.Null);

            GameObject matchRoot = new GameObject("MenuSelectionMatch");
            MatchController matchController = matchRoot.AddComponent<MatchController>();
            MatchRoster roster = new MatchRoster();
            roster.EnsureDefaults();

            CharacterBootstrapProfile mizu = CreateProfile("mizu", "Mizu");
            CharacterBootstrapProfile storm = CreateProfile("storm_dragon", "Storm Dragon");
            CombatantSlotProfile slotOneProfile = ScriptableObject.CreateInstance<CombatantSlotProfile>();
            CombatantSlotProfile slotTwoProfile = ScriptableObject.CreateInstance<CombatantSlotProfile>();
            PlayerController originalSlotOne = null;

            try
            {
                originalSlotOne = CharacterBootstrapFactory.CreateCombatant(
                    mizu,
                    CombatantSlotId.SlotOne,
                    slotOneProfile,
                    null);

                CombatantSlotConfig slotOne = roster.GetSlot(CombatantSlotId.SlotOne);
                slotOne.controller = originalSlotOne;
                slotOne.characterProfile = mizu;
                slotOne.playerProfile = slotOneProfile;
                slotOne.fallbackSpawnPoint = new Vector2(-200f, -100f);

                CombatantSlotConfig slotTwo = roster.GetSlot(CombatantSlotId.SlotTwo);
                slotTwo.characterProfile = storm;
                slotTwo.playerProfile = slotTwoProfile;
                slotTwo.fallbackSpawnPoint = new Vector2(200f, -100f);

                MatchRosterField.SetValue(matchController, roster);

                ProjectPvpMenuSelection selection = ProjectPvpMenuSelectionService.BuildDefault(
                    roster.Slots,
                    new[] { mizu, storm },
                    ProjectPvpMenuGameMode.HumanVsAi);
                selection.GetSlot(CombatantSlotId.SlotOne).CharacterProfile = storm;
                selection.GetSlot(CombatantSlotId.SlotOne).CharacterIndex = 1;
                selection.GetSlot(CombatantSlotId.SlotTwo).CharacterProfile = mizu;
                selection.GetSlot(CombatantSlotId.SlotTwo).CharacterIndex = 0;

                matchController.ApplyMenuSelection(selection);

                CombatantSlotConfig resolvedSlotOne = matchController.GetSlot(CombatantSlotId.SlotOne);
                CombatantSlotConfig resolvedSlotTwo = matchController.GetSlot(CombatantSlotId.SlotTwo);

                Assert.That(resolvedSlotOne.controller, Is.Not.Null);
                Assert.That(resolvedSlotOne.controller, Is.Not.SameAs(originalSlotOne));
                Assert.That(resolvedSlotOne.characterProfile, Is.SameAs(storm));
                Assert.That(resolvedSlotOne.controller.characterDefinition, Is.SameAs(storm.ResolveCharacterDefinition()));
                Assert.That(resolvedSlotOne.playerProfile.ResolveControlMode(), Is.EqualTo(CombatantControlMode.Human));

                Assert.That(resolvedSlotTwo.controller, Is.Not.Null);
                Assert.That(resolvedSlotTwo.characterProfile, Is.SameAs(mizu));
                Assert.That(resolvedSlotTwo.controller.characterDefinition, Is.SameAs(mizu.ResolveCharacterDefinition()));
                Assert.That(resolvedSlotTwo.playerProfile.ResolveControlMode(), Is.EqualTo(CombatantControlMode.AI));
            }
            finally
            {
                Object.DestroyImmediate(matchRoot);
                if (originalSlotOne != null)
                {
                    Object.DestroyImmediate(originalSlotOne.gameObject);
                }

                DestroyProfile(mizu);
                DestroyProfile(storm);
                Object.DestroyImmediate(slotOneProfile);
                Object.DestroyImmediate(slotTwoProfile);
            }
        }

        private static CharacterBootstrapProfile CreateProfile(string id, string displayName)
        {
            CharacterDefinition definition = ScriptableObject.CreateInstance<CharacterDefinition>();
            definition.id = id;
            definition.displayName = displayName;
            definition.defaultSprite = CreateSprite(new Color(0.8f, 0.9f, 1f, 1f));

            CharacterBootstrapProfile profile = ScriptableObject.CreateInstance<CharacterBootstrapProfile>();
            profile.id = id;
            profile.displayName = displayName;
            profile.characterDefinition = definition;
            return profile;
        }

        private static Sprite CreateSprite(Color color)
        {
            Texture2D texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            for (int y = 0; y < texture.height; y += 1)
            {
                for (int x = 0; x < texture.width; x += 1)
                {
                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 4f);
        }

        private static void DestroyProfile(CharacterBootstrapProfile profile)
        {
            if (profile == null)
            {
                return;
            }

            CharacterDefinition definition = profile.characterDefinition;
            Sprite sprite = definition != null ? definition.defaultSprite : null;
            Texture texture = sprite != null ? sprite.texture : null;
            Object.DestroyImmediate(profile);
            Object.DestroyImmediate(definition);
            Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(texture);
        }
    }
}
