using UnityEngine;

namespace ProjectPVP.Gameplay
{
    public enum PlayerCombatAnchorKind
    {
        Spawn = 0,
        MeleeHitbox = 1,
        UltimateHitbox = 2,
        UltimateReplayHitbox = 3,
    }

    [ExecuteAlways]
    public sealed class PlayerCombatAnchor : MonoBehaviour
    {
        public PlayerCombatAnchorKind anchorKind = PlayerCombatAnchorKind.MeleeHitbox;
        public bool mirrorX = true;
        public Vector2 boxSize = new Vector2(96f, 72f);
        [Min(0f)] public float radius = 180f;

        [SerializeField] private Vector3 authoredLocalPosition = Vector3.zero;
        [SerializeField] private Vector3 authoredLocalEulerAngles = Vector3.zero;
        [SerializeField] private Vector3 authoredLocalScale = Vector3.one;

        public Collider2D AttachedCollider => GetComponent<Collider2D>();

        private void Reset()
        {
            CaptureAuthoredPose();
        }

        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                CaptureAuthoredPose();
            }
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying)
            {
                CaptureAuthoredPose();
            }
        }

        public Vector2 ResolveWorldPosition(Transform root, int facingDirection)
        {
            Transform referenceRoot = root != null ? root : transform.parent;
            if (referenceRoot == null)
            {
                return transform.position;
            }

            Vector3 localPosition = ResolveMirroredLocalPosition(facingDirection);
            if (mirrorX)
            {
                localPosition.x = ResolveMirroredLocalPosition(facingDirection).x;
            }

            return referenceRoot.TransformPoint(localPosition);
        }

        public void ApplyRuntimePose(int facingDirection)
        {
            if (!mirrorX)
            {
                transform.localPosition = authoredLocalPosition;
                transform.localEulerAngles = authoredLocalEulerAngles;
                transform.localScale = authoredLocalScale;
                return;
            }

            float direction = facingDirection < 0 ? -1f : 1f;
            Vector3 localPosition = authoredLocalPosition;
            localPosition.x = Mathf.Abs(authoredLocalPosition.x) * direction;

            Vector3 localScale = authoredLocalScale;
            localScale.x = Mathf.Abs(authoredLocalScale.x) * direction;

            transform.localPosition = localPosition;
            transform.localEulerAngles = authoredLocalEulerAngles;
            transform.localScale = localScale;
        }

        private void OnDrawGizmosSelected()
        {
            Transform referenceRoot = transform.parent != null ? transform.parent : transform;
            Vector2 worldPosition = ResolveWorldPosition(referenceRoot, 1);
            Gizmos.color = ResolveGizmoColor();

            if (TryDrawAttachedColliderGizmo())
            {
                return;
            }

            switch (anchorKind)
            {
                case PlayerCombatAnchorKind.Spawn:
                    Gizmos.DrawWireSphere(worldPosition, 20f);
                    Gizmos.DrawLine(worldPosition + Vector2.left * 20f, worldPosition + Vector2.right * 20f);
                    Gizmos.DrawLine(worldPosition + Vector2.up * 20f, worldPosition + Vector2.down * 20f);
                    break;
                case PlayerCombatAnchorKind.UltimateHitbox:
                case PlayerCombatAnchorKind.UltimateReplayHitbox:
                    Gizmos.DrawWireSphere(worldPosition, Mathf.Max(1f, radius));
                    break;
                default:
                    Gizmos.DrawWireCube(worldPosition, new Vector3(Mathf.Max(1f, boxSize.x), Mathf.Max(1f, boxSize.y), 0f));
                    break;
            }
        }

        public void CaptureAuthoredPose()
        {
            authoredLocalPosition = transform.localPosition;
            authoredLocalEulerAngles = transform.localEulerAngles;
            authoredLocalScale = transform.localScale;
        }

        private Vector3 ResolveMirroredLocalPosition(int facingDirection)
        {
            if (!mirrorX)
            {
                return authoredLocalPosition;
            }

            float direction = facingDirection < 0 ? -1f : 1f;
            Vector3 localPosition = authoredLocalPosition;
            localPosition.x = Mathf.Abs(authoredLocalPosition.x) * direction;
            return localPosition;
        }

        private bool TryDrawAttachedColliderGizmo()
        {
            Collider2D attachedCollider = AttachedCollider;
            if (attachedCollider == null || anchorKind == PlayerCombatAnchorKind.Spawn)
            {
                return false;
            }

            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = attachedCollider.transform.localToWorldMatrix;

            switch (attachedCollider)
            {
                case BoxCollider2D box:
                    Gizmos.DrawWireCube(box.offset, box.size);
                    break;
                case CircleCollider2D circle:
                    Gizmos.DrawWireSphere(circle.offset, circle.radius);
                    break;
                case CapsuleCollider2D capsule:
                    Gizmos.DrawWireCube(capsule.offset, capsule.size);
                    break;
                default:
                    Gizmos.DrawWireCube(Vector3.zero, Vector3.one * 24f);
                    break;
            }

            Gizmos.matrix = previousMatrix;
            return true;
        }

        private Color ResolveGizmoColor()
        {
            switch (anchorKind)
            {
                case PlayerCombatAnchorKind.Spawn:
                    return new Color(1f, 0.92f, 0.25f, 0.95f);
                case PlayerCombatAnchorKind.UltimateHitbox:
                    return new Color(1f, 0.25f, 0.9f, 0.95f);
                case PlayerCombatAnchorKind.UltimateReplayHitbox:
                    return new Color(0.2f, 0.95f, 1f, 0.98f);
                default:
                    return new Color(1f, 0.32f, 0.28f, 0.95f);
            }
        }
    }
}
