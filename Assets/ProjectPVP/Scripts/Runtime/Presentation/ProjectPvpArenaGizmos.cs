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
        public Color alternateSpawnColor = new Color(0.35f, 1f, 0.72f, 0.9f);
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

            if (matchController.RoundRespawnSeeds.Count > 0)
            {
                for (int index = 0; index < matchController.RoundRespawnSeeds.Count; index += 1)
                {
                    float alpha = index == matchController.CurrentRespawnSeedIndex ? 1f : 0.35f;
                    float radius = index == matchController.CurrentRespawnSeedIndex ? spawnMarkerRadius : spawnMarkerRadius * 0.72f;
                    Vector2 slotOnePoint = matchController.GetRespawnSeedPoint(index, CombatantSlotId.SlotOne);
                    Vector2 slotTwoPoint = matchController.GetRespawnSeedPoint(index, CombatantSlotId.SlotTwo);

                    DrawSpawnMarker(slotOnePoint, new Color(spawnColor.r, spawnColor.g, spawnColor.b, alpha), radius);
                    DrawSpawnMarker(slotTwoPoint, new Color(alternateSpawnColor.r, alternateSpawnColor.g, alternateSpawnColor.b, alpha), radius);

                    Gizmos.color = new Color(1f, 1f, 1f, index == matchController.CurrentRespawnSeedIndex ? 0.45f : 0.18f);
                    Gizmos.DrawLine(slotOnePoint, slotTwoPoint);
                }

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

                    DrawSpawnMarker(matchController.GetSpawnPoint(slot.slotId), spawnColor, spawnMarkerRadius);
                }

                return;
            }
        }

        private void DrawSpawnMarker(Vector2 spawnPoint, Color color, float radius)
        {
            Gizmos.color = color;
            Gizmos.DrawWireSphere(spawnPoint, radius);
            Gizmos.DrawLine(spawnPoint + Vector2.left * radius, spawnPoint + Vector2.right * radius);
            Gizmos.DrawLine(spawnPoint + Vector2.up * radius, spawnPoint + Vector2.down * radius);
        }
    }
}
