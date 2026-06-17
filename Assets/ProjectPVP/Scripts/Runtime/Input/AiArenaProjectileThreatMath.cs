using UnityEngine;

namespace ProjectPVP.Input
{
    internal static class AiArenaProjectileThreatMath
    {
        internal static float EstimateTimeToClosestApproach(
            Vector2 selfPosition,
            Vector2 selfVelocity,
            Vector2 projectilePosition,
            Vector2 projectileVelocity)
        {
            Vector2 relativePosition = selfPosition - projectilePosition;
            Vector2 relativeVelocity = projectileVelocity - selfVelocity;
            float relativeSpeedSqr = relativeVelocity.sqrMagnitude;
            if (relativeSpeedSqr <= 1f || Vector2.Dot(relativePosition, relativeVelocity) <= 0f)
            {
                return -1f;
            }

            return Mathf.Clamp(Vector2.Dot(relativePosition, relativeVelocity) / relativeSpeedSqr, 0f, 1.5f);
        }

        internal static bool TryEstimateClosestApproach(
            Vector2 selfPosition,
            Vector2 selfVelocity,
            Vector2 projectilePosition,
            Vector2 projectileVelocity,
            out float timeToClosest,
            out Vector2 closestOffset)
        {
            timeToClosest = EstimateTimeToClosestApproach(
                selfPosition,
                selfVelocity,
                projectilePosition,
                projectileVelocity);
            if (timeToClosest < 0f)
            {
                closestOffset = Vector2.zero;
                return false;
            }

            Vector2 relativePosition = selfPosition - projectilePosition;
            Vector2 relativeVelocity = projectileVelocity - selfVelocity;
            closestOffset = relativePosition - relativeVelocity * timeToClosest;
            return true;
        }
    }
}
