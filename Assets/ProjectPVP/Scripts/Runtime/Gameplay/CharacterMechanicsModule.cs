using System.Collections.Generic;
using ProjectPVP.Data;
using UnityEngine;

namespace ProjectPVP.Gameplay
{
    public sealed class CharacterMechanicsSceneAnchorDefinition
    {
        public string childName = string.Empty;
        public PlayerCombatAnchorKind anchorKind = PlayerCombatAnchorKind.MeleeHitbox;
        public CombatShapeKind shapeKind = CombatShapeKind.Box;
        public Vector3 localPosition = Vector3.zero;
        public Vector3 localEulerAngles = Vector3.zero;
        public Vector2 colliderOffset = Vector2.zero;
        public Vector2 boxSize = new Vector2(96f, 72f);
        public float radius = 96f;
        public CapsuleDirection2D capsuleDirection = CapsuleDirection2D.Horizontal;
        public bool mirrorX = true;
    }

    public abstract class CharacterMechanicsModule : ScriptableObject
    {
        public virtual IEnumerable<string> GetAdditionalActionKeys(CharacterDefinition definition)
        {
            yield break;
        }

        public virtual IEnumerable<CharacterMechanicsSceneAnchorDefinition> GetAdditionalSceneAnchors(PlayerController player, CharacterDefinition definition)
        {
            yield break;
        }

        public abstract CharacterMechanicsRuntime CreateRuntime(PlayerController player, CharacterDefinition definition);
    }

    public abstract class CharacterMechanicsRuntime
    {
        protected CharacterMechanicsRuntime(PlayerController player, CharacterDefinition definition)
        {
            Player = player;
            Definition = definition;
        }

        protected PlayerController Player { get; }
        protected CharacterDefinition Definition { get; }

        public virtual void OnSpawned() { }
        public virtual void OnResetState() { }
        public virtual void OnKilled() { }
        public virtual void OnTick(float deltaTime) { }
        public virtual void OnUltimateStarted() { }
        public virtual void OnUltimateImpactApplied() { }
        public virtual void DrawGizmos(bool selected) { }
    }
}
