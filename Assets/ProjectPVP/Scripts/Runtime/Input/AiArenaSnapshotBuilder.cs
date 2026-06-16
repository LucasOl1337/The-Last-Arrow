using System.Collections.Generic;

namespace ProjectPVP.Input
{
    internal static class AiArenaSnapshotBuilder
    {
        public static AiArenaSnapshotEnvelope Build(
            AiArenaControllerSnapshot self,
            AiArenaControllerSnapshot target,
            IReadOnlyList<AiArenaProjectileSnapshot> projectiles,
            AiArenaArenaSnapshot arena,
            int frame,
            float desiredCombatDistance,
            float closeRetreatDistance,
            float meleeRange,
            float ultimateRange,
            float shootRange,
            float verticalTolerance)
        {
            var envelope = new AiArenaSnapshotEnvelope
            {
                frame = frame,
                selfSlotId = self.slotId,
                arena = AiArenaObservationMapper.ToObservation(arena),
                self = AiArenaObservationMapper.ToObservation(self),
                opponents = new List<AiArenaCombatantObservation>(),
                projectiles = new List<AiArenaProjectileObservation>(),
            };

            if (target.isValid)
            {
                envelope.opponents.Add(AiArenaObservationMapper.ToObservation(target));
            }

            // Preserve a stable, character-agnostic projectile list for both local and brokered AI.
            if (projectiles != null)
            {
                for (int index = 0; index < projectiles.Count; index += 1)
                {
                    AiArenaProjectileSnapshot projectile = projectiles[index];
                    if (!projectile.isValid)
                    {
                        continue;
                    }

                    envelope.projectiles.Add(AiArenaObservationMapper.ToObservation(projectile));
                }
            }

            envelope.semantics = AiArenaSemanticObservationBuilder.Build(
                self,
                target,
                projectiles,
                arena,
                desiredCombatDistance,
                closeRetreatDistance,
                meleeRange,
                ultimateRange,
                shootRange,
                verticalTolerance);
            return envelope;
        }
    }
}
