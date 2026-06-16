namespace ProjectPVP.Input
{
    internal static class AiArenaObservationMapper
    {
        internal static AiArenaArenaObservation ToObservation(AiArenaArenaSnapshot snapshot)
        {
            return new AiArenaArenaObservation
            {
                roundResetPending = snapshot.roundResetPending,
                roundsToChampion = snapshot.roundsToChampion,
                playerOneWins = snapshot.playerOneWins,
                playerTwoWins = snapshot.playerTwoWins,
                currentRespawnSeedIndex = snapshot.currentRespawnSeedIndex,
                currentRespawnSeedLabel = snapshot.currentRespawnSeedLabel ?? string.Empty,
                pendingRoundWinnerSlot = snapshot.pendingRoundWinnerSlot,
                pendingChampionSlot = snapshot.pendingChampionSlot,
                championAnnouncementSlot = snapshot.championAnnouncementSlot,
                wrapXMin = snapshot.wrapBounds.xMin,
                wrapXMax = snapshot.wrapBounds.xMax,
                wrapYMin = snapshot.wrapBounds.yMin,
                wrapYMax = snapshot.wrapBounds.yMax,
            };
        }

        internal static AiArenaCombatantObservation ToObservation(AiArenaControllerSnapshot snapshot)
        {
            return new AiArenaCombatantObservation
            {
                slotId = snapshot.slotId,
                botId = snapshot.botId,
                botDisplayName = snapshot.botDisplayName,
                characterId = snapshot.characterId,
                displayName = snapshot.displayName,
                actionKey = snapshot.actionKey,
                isDead = snapshot.isDead,
                isGrounded = snapshot.isGrounded,
                isTouchingWall = snapshot.isTouchingWall,
                isDashing = snapshot.isDashing,
                isMeleeActive = snapshot.isMeleeActive,
                isShootAnimating = snapshot.isShootAnimating,
                isUltimateActive = snapshot.isUltimateActive,
                isHitStunned = snapshot.isHitStunned,
                canParryProjectile = snapshot.canParryProjectile,
                canBlockProjectiles = snapshot.canBlockProjectiles,
                facing = snapshot.facing,
                arrows = snapshot.arrows,
                position = snapshot.position,
                velocity = snapshot.velocity,
                shootCooldownLeft = snapshot.shootCooldownLeft,
                meleeCooldownLeft = snapshot.meleeCooldownLeft,
                dashCooldownLeft = snapshot.dashCooldownLeft,
                ultimateCooldownLeft = snapshot.ultimateCooldownLeft,
                hitStunTimeLeft = snapshot.hitStunTimeLeft,
                meleeHitboxCenter = snapshot.meleeHitboxCenter,
                meleeHitboxSize = snapshot.meleeHitboxSize,
                ultimateHitboxCenter = snapshot.ultimateHitboxCenter,
                ultimateHitboxRadius = snapshot.ultimateHitboxRadius,
            };
        }

        internal static AiArenaProjectileObservation ToObservation(AiArenaProjectileSnapshot snapshot)
        {
            return new AiArenaProjectileObservation
            {
                sourceSlotId = snapshot.sourceSlotId,
                isStuck = snapshot.isStuck,
                isDisarmed = snapshot.isDisarmed,
                position = snapshot.position,
                velocity = snapshot.velocity,
                travelDirection = snapshot.travelDirection,
            };
        }
    }
}
