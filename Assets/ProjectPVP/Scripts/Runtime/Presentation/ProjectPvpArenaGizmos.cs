using ProjectPVP.Data;
using ProjectPVP.Match;
using UnityEngine;

namespace ProjectPVP.Presentation
{
    public sealed class ProjectPvpArenaGizmos : MonoBehaviour
    {
        public MatchController matchController;
        public Color boundsColor = new Color(0.2f, 0.85f, 1f, 0.9f);
        public Color spawnColor = new Color(1f, 0.9f, 0.25f, 0.95f);
        public float spawnMarkerRadius = 24f;

        private void Reset()
        {
            matchController = GetComponent<MatchController>();
        }

        private void OnDrawGizmos()
        {
            Rect bounds = matchController != null ? matchController.ActiveWrapBounds : new Rect(-1280f, -720f, 2560f, 1440f);

            Gizmos.color = boundsColor;
            Gizmos.DrawWireCube(bounds.center, bounds.size);

            if (matchController == null)
            {
                return;
            }

            Gizmos.color = spawnColor;
            if (matchController.Slots.Count > 0)
            {
                for (int index = 0; index < matchController.Slots.Count; index += 1)
                {
                    CombatantSlotConfig slot = matchController.Slots[index];
                    if (slot == null)
                    {
                        continue;
                    }

                    DrawSpawnMarker(matchController.GetSpawnPoint(slot.slotId));
                }

                return;
            }
        }

        private void DrawSpawnMarker(Vector2 spawnPoint)
        {
            Gizmos.DrawWireSphere(spawnPoint, spawnMarkerRadius);
            Gizmos.DrawLine(spawnPoint + Vector2.left * spawnMarkerRadius, spawnPoint + Vector2.right * spawnMarkerRadius);
            Gizmos.DrawLine(spawnPoint + Vector2.up * spawnMarkerRadius, spawnPoint + Vector2.down * spawnMarkerRadius);
        }
    }
}
