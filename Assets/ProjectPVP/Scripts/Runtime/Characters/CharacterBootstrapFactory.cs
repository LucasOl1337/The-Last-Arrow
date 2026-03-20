using ProjectPVP.Data;
using ProjectPVP.Gameplay;
using ProjectPVP.Input;
using ProjectPVP.Match;
using ProjectPVP.Presentation;
using UnityEngine;

namespace ProjectPVP.Characters
{
    public static class CharacterBootstrapFactory
    {
        public static PlayerController CreateCombatant(
            CharacterBootstrapProfile bootstrapProfile,
            CombatantSlotId slotId,
            CombatantSlotProfile slotProfile,
            Transform parent)
        {
            if (bootstrapProfile == null)
            {
                return null;
            }

            CharacterDefinition definition = bootstrapProfile.ResolveCharacterDefinition();
            if (definition == null)
            {
                return null;
            }

            GameObject root = new GameObject(bootstrapProfile.ResolveDisplayName());
            if (parent != null)
            {
                root.transform.SetParent(parent, false);
            }

            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            BoxCollider2D bodyCollider = root.AddComponent<BoxCollider2D>();
            KeyboardPlayerInputSource keyboardInput = root.AddComponent<KeyboardPlayerInputSource>();
            PlayerController controller = root.AddComponent<PlayerController>();

            GameObject spriteObject = new GameObject("Sprite");
            spriteObject.transform.SetParent(root.transform, false);
            SpriteRenderer spriteRenderer = spriteObject.AddComponent<SpriteRenderer>();
            CharacterSpriteAnimator spriteAnimator = spriteObject.AddComponent<CharacterSpriteAnimator>();

            Transform projectileOrigin = CreateProjectileOrigin(root.transform, definition);
            PlayerCombatAnchor spawnAnchor = CreateAnchor(root.transform, bootstrapProfile.spawnAnchor);
            PlayerCombatAnchor meleeHitbox = CreateAnchor(root.transform, bootstrapProfile.meleeHitbox);
            PlayerCombatAnchor ultimateHitbox = CreateAnchor(root.transform, bootstrapProfile.ultimateHitbox);

            controller.slotId = Mathf.Max(1, slotId.ToInt());
            controller.slotProfile = slotProfile != null ? slotProfile : CombatantSlotProfile.ResolveRuntimeFallback(slotId);
            controller.characterDefinition = definition;
            controller.inputSource = keyboardInput;
            controller.body = body;
            controller.bodyCollider = bodyCollider;
            controller.spriteRenderer = spriteRenderer;
            controller.spawnAnchor = spawnAnchor;
            controller.projectileOrigin = projectileOrigin;
            controller.meleeHitboxAnchor = meleeHitbox;
            controller.ultimateHitboxAnchor = ultimateHitbox;
            controller.projectilePrefab = bootstrapProfile.projectilePrefab;
            controller.anchorRig = new CombatantAnchorRig
            {
                spawnAnchor = spawnAnchor,
                projectileOrigin = projectileOrigin,
                meleeHitboxAnchor = meleeHitbox,
                ultimateHitboxAnchor = ultimateHitbox,
            };

            spriteAnimator.player = controller;
            spriteAnimator.spriteRenderer = spriteRenderer;

            ApplyBodyCollider(definition, bodyCollider);
            ApplyVisualDefaults(definition, spriteObject.transform, spriteRenderer);
            ConfigureKeyboardInput(keyboardInput, controller.slotProfile, slotId);
            CreateMechanicsAnchors(root.transform, controller, definition);

            return controller;
        }

        private static void ConfigureKeyboardInput(KeyboardPlayerInputSource keyboardInput, CombatantSlotProfile slotProfile, CombatantSlotId slotId)
        {
            if (keyboardInput == null)
            {
                return;
            }

            if (slotProfile != null)
            {
                keyboardInput.ApplySlotProfile(slotProfile, slotId);
                return;
            }

            keyboardInput.ConfigureForSlot(slotId);
        }

        private static void ApplyBodyCollider(CharacterDefinition definition, BoxCollider2D bodyCollider)
        {
            if (definition == null || bodyCollider == null)
            {
                return;
            }

            bodyCollider.size = definition.colliderSize;
            bodyCollider.offset = definition.colliderOffset;
        }

        private static void ApplyVisualDefaults(CharacterDefinition definition, Transform spriteTransform, SpriteRenderer spriteRenderer)
        {
            if (definition == null || spriteTransform == null || spriteRenderer == null)
            {
                return;
            }

            spriteTransform.localPosition = new Vector3(definition.spriteAnchorOffset.x, definition.spriteAnchorOffset.y, 0f);
            spriteTransform.localScale = new Vector3(definition.spriteScale.x, definition.spriteScale.y, 1f);
            spriteRenderer.sprite = definition.defaultSprite;
            spriteRenderer.sortingOrder = 10;
            spriteRenderer.color = Color.white;
        }

        private static Transform CreateProjectileOrigin(Transform parent, CharacterDefinition definition)
        {
            GameObject projectileOrigin = new GameObject("ProjectileOrigin");
            projectileOrigin.transform.SetParent(parent, false);

            Vector2 configuredOffset = definition != null
                ? definition.projectileOriginOffset
                : Vector2.zero;
            projectileOrigin.transform.localPosition = new Vector3(configuredOffset.x, configuredOffset.y, 0f);
            return projectileOrigin.transform;
        }

        private static PlayerCombatAnchor CreateAnchor(Transform parent, CharacterBootstrapAnchorConfig config)
        {
            CharacterBootstrapAnchorConfig resolvedConfig = config ?? CharacterBootstrapAnchorConfig.CreateSpawnAnchor();
            string childName = string.IsNullOrWhiteSpace(resolvedConfig.childName) ? resolvedConfig.anchorKind.ToString() : resolvedConfig.childName.Trim();

            GameObject anchorObject = new GameObject(childName);
            anchorObject.transform.SetParent(parent, false);
            anchorObject.transform.localPosition = new Vector3(resolvedConfig.localPosition.x, resolvedConfig.localPosition.y, 0f);
            anchorObject.transform.localEulerAngles = resolvedConfig.localEulerAngles;

            PlayerCombatAnchor anchor = anchorObject.AddComponent<PlayerCombatAnchor>();
            anchor.anchorKind = resolvedConfig.anchorKind;
            anchor.mirrorX = resolvedConfig.mirrorX;

            ApplyCollider(anchorObject, anchor, resolvedConfig.collider);
            anchor.CaptureAuthoredPose();
            return anchor;
        }

        private static void ApplyCollider(GameObject anchorObject, PlayerCombatAnchor anchor, CharacterBootstrapColliderConfig colliderConfig)
        {
            if (anchorObject == null || anchor == null || colliderConfig == null)
            {
                return;
            }

            switch (colliderConfig.shapeKind)
            {
                case CharacterBootstrapColliderShape.Circle:
                {
                    CircleCollider2D circle = anchorObject.AddComponent<CircleCollider2D>();
                    circle.isTrigger = true;
                    circle.offset = colliderConfig.offset;
                    circle.radius = Mathf.Max(0f, colliderConfig.radius);
                    anchor.radius = circle.radius;
                    break;
                }
                case CharacterBootstrapColliderShape.Capsule:
                {
                    CapsuleCollider2D capsule = anchorObject.AddComponent<CapsuleCollider2D>();
                    capsule.isTrigger = true;
                    capsule.offset = colliderConfig.offset;
                    capsule.size = colliderConfig.size;
                    capsule.direction = colliderConfig.capsuleDirection;
                    anchor.boxSize = colliderConfig.size;
                    break;
                }
                case CharacterBootstrapColliderShape.Box:
                {
                    BoxCollider2D box = anchorObject.AddComponent<BoxCollider2D>();
                    box.isTrigger = true;
                    box.offset = colliderConfig.offset;
                    box.size = colliderConfig.size;
                    anchor.boxSize = colliderConfig.size;
                    break;
                }
            }
        }

        private static void CreateMechanicsAnchors(Transform parent, PlayerController controller, CharacterDefinition definition)
        {
            CharacterMechanicsModule mechanicsModule = definition != null ? definition.mechanicsModule : null;
            if (parent == null || controller == null || definition == null || mechanicsModule == null)
            {
                return;
            }

            foreach (CharacterMechanicsSceneAnchorDefinition sceneAnchor in mechanicsModule.GetAdditionalSceneAnchors(controller, definition))
            {
                if (sceneAnchor == null || string.IsNullOrWhiteSpace(sceneAnchor.childName) || parent.Find(sceneAnchor.childName) != null)
                {
                    continue;
                }

                CreateAnchor(parent, Convert(sceneAnchor));
            }
        }

        private static CharacterBootstrapAnchorConfig Convert(CharacterMechanicsSceneAnchorDefinition sceneAnchor)
        {
            return new CharacterBootstrapAnchorConfig
            {
                childName = sceneAnchor.childName,
                anchorKind = sceneAnchor.anchorKind,
                mirrorX = sceneAnchor.mirrorX,
                localPosition = new Vector2(sceneAnchor.localPosition.x, sceneAnchor.localPosition.y),
                localEulerAngles = sceneAnchor.localEulerAngles,
                collider = new CharacterBootstrapColliderConfig
                {
                    shapeKind = Convert(sceneAnchor.shapeKind),
                    offset = sceneAnchor.colliderOffset,
                    size = sceneAnchor.boxSize,
                    radius = sceneAnchor.radius,
                    capsuleDirection = sceneAnchor.capsuleDirection,
                },
            };
        }

        private static CharacterBootstrapColliderShape Convert(CombatShapeKind shapeKind)
        {
            return shapeKind switch
            {
                CombatShapeKind.Circle => CharacterBootstrapColliderShape.Circle,
                CombatShapeKind.Capsule => CharacterBootstrapColliderShape.Capsule,
                _ => CharacterBootstrapColliderShape.Box,
            };
        }
    }
}
