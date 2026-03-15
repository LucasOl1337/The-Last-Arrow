using ProjectPVP.Gameplay;
using UnityEditor;
using UnityEngine;

namespace ProjectPVP.Editor
{
    [CustomEditor(typeof(PlayerCombatAnchor))]
    internal sealed class PlayerCombatAnchorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var anchor = (PlayerCombatAnchor)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Hitbox Shape", EditorStyles.boldLabel);

            if (anchor.anchorKind == PlayerCombatAnchorKind.Spawn)
            {
                EditorGUILayout.HelpBox("SpawnAnchor usa apenas Transform. Para editar a posicao de spawn, mova este objeto na cena.", MessageType.Info);
                return;
            }

            Collider2D collider = anchor.AttachedCollider;
            if (collider == null)
            {
                EditorGUILayout.HelpBox("Este anchor ainda nao tem Collider2D. Crie um shape abaixo.", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Edite Offset, Size, Radius, Direction e rotacao diretamente no Transform/Collider abaixo. O runtime usa este Collider2D real como hitbox.",
                    MessageType.Info);
                EditorGUILayout.LabelField("Collider atual", collider.GetType().Name);
            }

            EditorGUILayout.BeginHorizontal();
            DrawShapeButton(anchor, "Box", typeof(BoxCollider2D));
            DrawShapeButton(anchor, "Circle", typeof(CircleCollider2D));
            DrawShapeButton(anchor, "Capsule", typeof(CapsuleCollider2D));
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawShapeButton(PlayerCombatAnchor anchor, string label, System.Type colliderType)
        {
            bool isCurrent = anchor.AttachedCollider != null && anchor.AttachedCollider.GetType() == colliderType;
            using (new EditorGUI.DisabledScope(isCurrent))
            {
                if (GUILayout.Button(label))
                {
                    SwapCollider(anchor, colliderType);
                }
            }
        }

        private static void SwapCollider(PlayerCombatAnchor anchor, System.Type colliderType)
        {
            if (anchor == null)
            {
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(anchor.gameObject, "Change Anchor Collider");

            Collider2D previous = anchor.AttachedCollider;
            Vector2 previousOffset = previous != null ? previous.offset : Vector2.zero;
            Vector2 fallbackBoxSize = anchor.boxSize == Vector2.zero ? new Vector2(96f, 72f) : anchor.boxSize;
            float fallbackRadius = Mathf.Max(1f, anchor.radius);
            Vector2 previousSize = fallbackBoxSize;
            CapsuleDirection2D previousCapsuleDirection = CapsuleDirection2D.Vertical;

            switch (previous)
            {
                case BoxCollider2D box:
                    previousSize = box.size;
                    break;
                case CircleCollider2D circle:
                    fallbackRadius = Mathf.Max(1f, circle.radius);
                    break;
                case CapsuleCollider2D capsule:
                    previousSize = capsule.size;
                    previousCapsuleDirection = capsule.direction;
                    break;
            }

            if (previous != null)
            {
                Undo.DestroyObjectImmediate(previous);
            }

            Collider2D created = (Collider2D)Undo.AddComponent(anchor.gameObject, colliderType);
            created.isTrigger = true;
            created.offset = previousOffset;

            switch (created)
            {
                case BoxCollider2D box:
                    box.size = previousSize;
                    break;
                case CircleCollider2D circle:
                    circle.radius = fallbackRadius;
                    break;
                case CapsuleCollider2D capsule:
                    capsule.size = previousSize;
                    capsule.direction = previousCapsuleDirection;
                    break;
            }

            EditorUtility.SetDirty(anchor);
        }
    }
}
