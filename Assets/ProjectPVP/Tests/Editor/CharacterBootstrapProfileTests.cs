using System.Reflection;
using NUnit.Framework;
using ProjectPVP.Characters;
using ProjectPVP.Data;
using ProjectPVP.Gameplay;
using ProjectPVP.Input;
using ProjectPVP.Match;
using ProjectPVP.Presentation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectPVP.Tests.Editor
{
    public sealed class CharacterBootstrapProfileTests
    {
        private static readonly FieldInfo MatchRosterField = typeof(MatchController).GetField("roster", BindingFlags.Instance | BindingFlags.NonPublic);

        [Test]
        public void ResolveCharacterDefinition_UsesCharacterBootstrapProfile_WhenDirectSelectionIsMissing()
        {
            CombatantSlotConfig slot = new CombatantSlotConfig();
            CharacterBootstrapProfile bootstrapProfile = ScriptableObject.CreateInstance<CharacterBootstrapProfile>();
            CharacterDefinition definition = ScriptableObject.CreateInstance<CharacterDefinition>();

            try
            {
                bootstrapProfile.characterDefinition = definition;
                slot.characterProfile = bootstrapProfile;

                CharacterDefinition resolvedDefinition = slot.ResolveCharacterDefinition();

                Assert.That(resolvedDefinition, Is.SameAs(definition));
            }
            finally
            {
                Object.DestroyImmediate(bootstrapProfile);
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void CreateCombatant_BuildsCharacterOwnedRuntimeHierarchy()
        {
            CharacterBootstrapProfile bootstrapProfile = ScriptableObject.CreateInstance<CharacterBootstrapProfile>();
            CharacterDefinition definition = ScriptableObject.CreateInstance<CharacterDefinition>();
            CombatantSlotProfile slotProfile = ScriptableObject.CreateInstance<CombatantSlotProfile>();
            TestCharacterMechanicsModule mechanicsModule = ScriptableObject.CreateInstance<TestCharacterMechanicsModule>();
            GameObject projectilePrefabRoot = new GameObject("ProjectilePrefab");
            ProjectileController projectilePrefab = projectilePrefabRoot.AddComponent<ProjectileController>();

            try
            {
                definition.id = "mizu";
                definition.displayName = "Mizu";
                definition.defaultSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 8f, 8f), new Vector2(0.5f, 0.5f));
                definition.projectileOriginOffset = new Vector2(54f, 80f);
                definition.spriteAnchorOffset = new Vector2(15f, 0f);
                definition.spriteScale = new Vector2(1.2f, 1.4f);
                definition.colliderSize = new Vector2(90f, 240f);
                definition.mechanicsModule = mechanicsModule;

                bootstrapProfile.characterDefinition = definition;
                bootstrapProfile.projectilePrefab = projectilePrefab;
                bootstrapProfile.meleeHitbox.localPosition = new Vector2(70.5f, 36f);
                bootstrapProfile.meleeHitbox.collider.shapeKind = CharacterBootstrapColliderShape.Box;
                bootstrapProfile.meleeHitbox.collider.offset = new Vector2(33.9f, -30.8f);
                bootstrapProfile.meleeHitbox.collider.size = new Vector2(144.3f, 46.2f);
                bootstrapProfile.ultimateHitbox.localPosition = new Vector2(158f, 24f);
                bootstrapProfile.ultimateHitbox.collider.shapeKind = CharacterBootstrapColliderShape.Box;
                bootstrapProfile.ultimateHitbox.collider.offset = new Vector2(2.5925674f, -17.942284f);
                bootstrapProfile.ultimateHitbox.collider.size = new Vector2(231.12022f, 72f);

                PlayerController controller = CharacterBootstrapFactory.CreateCombatant(
                    bootstrapProfile,
                    CombatantSlotId.SlotTwo,
                    slotProfile,
                    null);

                try
                {
                    Assert.That(controller, Is.Not.Null);
                    Assert.That(controller.characterDefinition, Is.SameAs(definition));
                    Assert.That(controller.projectilePrefab, Is.SameAs(projectilePrefab));
                    Assert.That(controller.SlotProfile, Is.SameAs(slotProfile));
                    Assert.That(controller.SlotId, Is.EqualTo(CombatantSlotId.SlotTwo));
                    Assert.That(controller.GetComponent<Rigidbody2D>(), Is.Not.Null);
                    Assert.That(controller.GetComponent<BoxCollider2D>(), Is.Not.Null);
                    Assert.That(controller.GetComponent<KeyboardPlayerInputSource>(), Is.Not.Null);
                    Assert.That(controller.GetComponentInChildren<CharacterSpriteAnimator>(), Is.Not.Null);
                    Assert.That(controller.transform.Find("Sprite"), Is.Not.Null);
                    Assert.That(controller.transform.Find("ProjectileOrigin"), Is.Not.Null);
                    Assert.That(controller.transform.Find("SpawnAnchor"), Is.Not.Null);
                    Assert.That(controller.transform.Find("MeleeHitbox"), Is.Not.Null);
                    Assert.That(controller.transform.Find("UltimateHitbox"), Is.Not.Null);
                    Assert.That(controller.transform.Find("ReplayAnchor"), Is.Not.Null);

                    Assert.That(controller.transform.Find("ProjectileOrigin").localPosition, Is.EqualTo(new Vector3(54f, 80f, 0f)));

                    BoxCollider2D rootCollider = controller.GetComponent<BoxCollider2D>();
                    Assert.That(rootCollider.size, Is.EqualTo(definition.colliderSize));
                }
                finally
                {
                    Object.DestroyImmediate(controller.gameObject);
                }
            }
            finally
            {
                Object.DestroyImmediate(bootstrapProfile);
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(slotProfile);
                Object.DestroyImmediate(mechanicsModule);
                Object.DestroyImmediate(projectilePrefabRoot);
            }
        }

        [Test]
        public void EnsureRuntimeCombatantsForConfiguredSlots_CreatesRuntimeControllers_FromCharacterBootstrapProfiles()
        {
            GameObject matchRoot = new GameObject("MatchControllerTests");
            MatchController matchController = matchRoot.AddComponent<MatchController>();
            MatchRoster roster = new MatchRoster();
            roster.EnsureDefaults();

            CharacterBootstrapProfile slotOneProfile = ScriptableObject.CreateInstance<CharacterBootstrapProfile>();
            CharacterBootstrapProfile slotTwoProfile = ScriptableObject.CreateInstance<CharacterBootstrapProfile>();
            CharacterDefinition slotOneDefinition = ScriptableObject.CreateInstance<CharacterDefinition>();
            CharacterDefinition slotTwoDefinition = ScriptableObject.CreateInstance<CharacterDefinition>();
            CombatantSlotProfile inputProfileOne = ScriptableObject.CreateInstance<CombatantSlotProfile>();
            CombatantSlotProfile inputProfileTwo = ScriptableObject.CreateInstance<CombatantSlotProfile>();
            GameObject projectilePrefabRoot = new GameObject("ProjectilePrefab");
            ProjectileController projectilePrefab = projectilePrefabRoot.AddComponent<ProjectileController>();

            try
            {
                slotOneDefinition.displayName = "Mizu";
                slotTwoDefinition.displayName = "Storm Dragon";
                slotOneProfile.characterDefinition = slotOneDefinition;
                slotTwoProfile.characterDefinition = slotTwoDefinition;
                slotOneProfile.projectilePrefab = projectilePrefab;
                slotTwoProfile.projectilePrefab = projectilePrefab;

                CombatantSlotConfig slotOne = roster.GetSlot(CombatantSlotId.SlotOne);
                slotOne.playerProfile = inputProfileOne;
                slotOne.characterProfile = slotOneProfile;
                slotOne.fallbackSpawnPoint = new Vector2(-639f, -572f);

                CombatantSlotConfig slotTwo = roster.GetSlot(CombatantSlotId.SlotTwo);
                slotTwo.playerProfile = inputProfileTwo;
                slotTwo.characterProfile = slotTwoProfile;
                slotTwo.fallbackSpawnPoint = new Vector2(690f, -576f);

                Assert.That(MatchRosterField, Is.Not.Null);
                MatchRosterField.SetValue(matchController, roster);

                matchController.EnsureRuntimeCombatantsForConfiguredSlots();

                CombatantSlotConfig resolvedSlotOne = matchController.GetSlot(CombatantSlotId.SlotOne);
                CombatantSlotConfig resolvedSlotTwo = matchController.GetSlot(CombatantSlotId.SlotTwo);

                Assert.That(resolvedSlotOne.controller, Is.Not.Null);
                Assert.That(resolvedSlotTwo.controller, Is.Not.Null);
                Assert.That(resolvedSlotOne.controller.characterDefinition, Is.SameAs(slotOneDefinition));
                Assert.That(resolvedSlotTwo.controller.characterDefinition, Is.SameAs(slotTwoDefinition));
                Assert.That(matchController.PlayerOneController, Is.SameAs(resolvedSlotOne.controller));
                Assert.That(matchController.PlayerTwoController, Is.SameAs(resolvedSlotTwo.controller));
            }
            finally
            {
                Object.DestroyImmediate(matchRoot);
                Object.DestroyImmediate(slotOneProfile);
                Object.DestroyImmediate(slotTwoProfile);
                Object.DestroyImmediate(slotOneDefinition);
                Object.DestroyImmediate(slotTwoDefinition);
                Object.DestroyImmediate(inputProfileOne);
                Object.DestroyImmediate(inputProfileTwo);
                Object.DestroyImmediate(projectilePrefabRoot);
            }
        }

        [Test]
        public void CharacterCatalog_ExposesVisibleCharacterList_ForEditorWorkflows()
        {
            CharacterCatalog catalog = ScriptableObject.CreateInstance<CharacterCatalog>();
            CharacterBootstrapProfile mizu = ScriptableObject.CreateInstance<CharacterBootstrapProfile>();
            CharacterBootstrapProfile stormDragon = ScriptableObject.CreateInstance<CharacterBootstrapProfile>();

            try
            {
                mizu.id = "mizu";
                mizu.displayName = "Mizu";
                stormDragon.id = "storm_dragon";
                stormDragon.displayName = "Storm Dragon";
                catalog.characters.Add(mizu);
                catalog.characters.Add(stormDragon);

                Assert.That(catalog.Characters.Count, Is.EqualTo(2));
                Assert.That(catalog.FindById("mizu"), Is.SameAs(mizu));
                Assert.That(catalog.FindById("storm_dragon"), Is.SameAs(stormDragon));
            }
            finally
            {
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(mizu);
                Object.DestroyImmediate(stormDragon);
            }
        }

        [Test]
        public void MatchController_ExposesAvailableCharacters_FromCharacterCatalog()
        {
            GameObject matchRoot = new GameObject("MatchControllerCatalogTests");
            MatchController matchController = matchRoot.AddComponent<MatchController>();
            CharacterCatalog catalog = ScriptableObject.CreateInstance<CharacterCatalog>();
            CharacterBootstrapProfile mizu = ScriptableObject.CreateInstance<CharacterBootstrapProfile>();
            CharacterBootstrapProfile stormDragon = ScriptableObject.CreateInstance<CharacterBootstrapProfile>();

            try
            {
                mizu.id = "mizu";
                mizu.displayName = "Mizu";
                stormDragon.id = "storm_dragon";
                stormDragon.displayName = "Storm Dragon";
                catalog.characters.Add(mizu);
                catalog.characters.Add(stormDragon);
                matchController.characterCatalog = catalog;

                Assert.That(matchController.AvailableCharacters.Count, Is.EqualTo(2));
                Assert.That(matchController.AvailableCharacters[0], Is.SameAs(mizu));
                Assert.That(matchController.AvailableCharacters[1], Is.SameAs(stormDragon));
            }
            finally
            {
                Object.DestroyImmediate(matchRoot);
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(mizu);
                Object.DestroyImmediate(stormDragon);
            }
        }

        [Test]
        public void BootstrapScene_UsesCharacterHierarchy_InsteadOfPlayerNames()
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/ProjectPVP/Scenes/Bootstrap.unity", OpenSceneMode.Single);

            Assert.That(scene.IsValid(), Is.True);

            GameObject projectRoot = GameObject.Find("ProjectPVP");
            Assert.That(projectRoot, Is.Not.Null);

            Transform gameplay = projectRoot.transform.Find("Gameplay");
            Assert.That(gameplay, Is.Not.Null);

            Transform characters = gameplay.Find("Characters");
            Assert.That(characters, Is.Not.Null);
            Assert.That(characters.Find("Mizu"), Is.Not.Null);
            Assert.That(characters.Find("StormDragon"), Is.Not.Null);
            Assert.That(gameplay.Find("Player1"), Is.Null);
            Assert.That(gameplay.Find("Player2"), Is.Null);
        }

        [Test]
        public void MoveCharacter_WhenGroundedOnSlope_SnapsBodyToRampHeight()
        {
            GameObject slopeRoot = new GameObject("Slope");
            EdgeCollider2D slope = slopeRoot.AddComponent<EdgeCollider2D>();
            slope.points = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(200f, 100f),
            };

            GameObject playerRoot = new GameObject("SlopePlayer");
            Rigidbody2D body = playerRoot.AddComponent<Rigidbody2D>();
            BoxCollider2D bodyCollider = playerRoot.AddComponent<BoxCollider2D>();
            PlayerController controller = playerRoot.AddComponent<PlayerController>();

            try
            {
                body.bodyType = RigidbodyType2D.Kinematic;
                body.useFullKinematicContacts = true;
                body.simulated = true;
                bodyCollider.size = new Vector2(20f, 40f);
                controller.body = body;
                controller.bodyCollider = bodyCollider;
                controller.groundCheckDistance = 16f;

                body.position = new Vector2(20f, 32f);
                playerRoot.transform.position = body.position;

                FieldInfo groundedField = typeof(PlayerController).GetField("_isGrounded", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(groundedField, Is.Not.Null);
                groundedField.SetValue(controller, true);

                MethodInfo moveCharacter = typeof(PlayerController).GetMethod("MoveCharacter", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(moveCharacter, Is.Not.Null);

                Vector2 velocity = new Vector2(100f, 0f);
                object[] parameters = { velocity, 0.2f };
                float initialY = body.position.y;

                moveCharacter.Invoke(controller, parameters);

                Assert.That(body.position.x, Is.GreaterThan(20f));
                Assert.That(body.position.y, Is.GreaterThan(initialY + 0.1f),
                    "Expected grounded horizontal movement on a slope to raise the body so it follows the ramp.");
            }
            finally
            {
                Object.DestroyImmediate(playerRoot);
                Object.DestroyImmediate(slopeRoot);
            }
        }

        [Test]
        public void ResolveBaseVisualActionKey_UsesWalkDuringBriefSlopeGroundGrace()
        {
            GameObject playerRoot = new GameObject("SlopeVisualPlayer");
            Rigidbody2D body = playerRoot.AddComponent<Rigidbody2D>();
            BoxCollider2D bodyCollider = playerRoot.AddComponent<BoxCollider2D>();
            PlayerController controller = playerRoot.AddComponent<PlayerController>();
            CharacterDefinition definition = ScriptableObject.CreateInstance<CharacterDefinition>();

            try
            {
                definition.displayName = "Mizu";
                definition.actions.Add(new CharacterActionConfig { actionName = "walk" });
                controller.characterDefinition = definition;
                controller.body = body;
                controller.bodyCollider = bodyCollider;
                body.linearVelocity = new Vector2(24f, 0f);

                FieldInfo groundedField = typeof(PlayerController).GetField("_isGrounded", BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo coyoteField = typeof(PlayerController).GetField("_coyoteTimeLeft", BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo inputFrameField = typeof(PlayerController).GetField("_currentInputFrame", BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo resolveActionMethod = typeof(PlayerController).GetMethod("ResolveBaseVisualActionKey", BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.That(groundedField, Is.Not.Null);
                Assert.That(coyoteField, Is.Not.Null);
                Assert.That(inputFrameField, Is.Not.Null);
                Assert.That(resolveActionMethod, Is.Not.Null);

                groundedField.SetValue(controller, false);
                coyoteField.SetValue(controller, 0.08f);
                inputFrameField.SetValue(controller, new PlayerInputFrame { axis = 1f });

                string actionKey = (string)resolveActionMethod.Invoke(controller, null);

                Assert.That(actionKey, Is.EqualTo("walk"));
            }
            finally
            {
                Object.DestroyImmediate(playerRoot);
                Object.DestroyImmediate(definition);
            }
        }

        private sealed class TestCharacterMechanicsModule : CharacterMechanicsModule
        {
            public override System.Collections.Generic.IEnumerable<CharacterMechanicsSceneAnchorDefinition> GetAdditionalSceneAnchors(PlayerController player, CharacterDefinition definition)
            {
                yield return new CharacterMechanicsSceneAnchorDefinition
                {
                    childName = "ReplayAnchor",
                    anchorKind = PlayerCombatAnchorKind.UltimateReplayHitbox,
                    shapeKind = CombatShapeKind.Circle,
                    localPosition = new Vector3(120f, 0f, 0f),
                    colliderOffset = new Vector2(16f, 8f),
                    radius = 88f,
                    mirrorX = true,
                };
            }

            public override CharacterMechanicsRuntime CreateRuntime(PlayerController player, CharacterDefinition definition)
            {
                return new TestCharacterMechanicsRuntime(player, definition);
            }
        }

        private sealed class TestCharacterMechanicsRuntime : CharacterMechanicsRuntime
        {
            public TestCharacterMechanicsRuntime(PlayerController player, CharacterDefinition definition)
                : base(player, definition)
            {
            }
        }
    }
}
