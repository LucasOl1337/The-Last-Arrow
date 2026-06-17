using UnityEngine;

namespace ProjectPVP.Gameplay
{
    internal static class ProjectileTrajectoryMath
    {
        internal static Vector2 EvaluatePosition(
            Vector2 origin,
            Vector2 initialVelocity,
            float gravity,
            float time)
        {
            return origin
                + (initialVelocity * time)
                + (Vector2.down * (0.5f * gravity * time * time));
        }

        internal static float ResolveEstimatedFlightTime(
            Vector2 origin,
            Vector2 target,
            Vector2 initialVelocity)
        {
            float horizontalSpeed = initialVelocity.x;
            float dx = target.x - origin.x;
            if (Mathf.Abs(horizontalSpeed) > 0.01f)
            {
                float time = dx / horizontalSpeed;
                if (time > 0f)
                {
                    return Mathf.Clamp(time, 0.05f, 2.5f);
                }
            }

            return Mathf.Clamp(Vector2.Distance(origin, target) / Mathf.Max(initialVelocity.magnitude, 0.01f), 0.05f, 2.5f);
        }

        internal static bool TryResolvePreferredTravelDirection(
            Vector2 origin,
            Vector2 target,
            float baseSpeed,
            float gravity,
            Vector2 inheritedVelocity,
            LayerMask groundMask,
            out Vector2 travelDirection)
        {
            travelDirection = Vector2.zero;
            if (!TrySolveBallisticArc(origin, target, baseSpeed, gravity, out Vector2 lowArc, out Vector2 highArc))
            {
                Vector2 fallback = target - origin;
                travelDirection = fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector2.right;
                return false;
            }

            Vector2 targetDirection = target - origin;
            if (targetDirection.sqrMagnitude <= 0.0001f)
            {
                targetDirection = Vector2.right;
            }

            Vector2 lowVelocity = (lowArc.normalized * baseSpeed) + inheritedVelocity;
            Vector2 highVelocity = (highArc.normalized * baseSpeed) + inheritedVelocity;
            bool lowClear = IsTrajectoryClear(origin, lowVelocity, gravity, target, groundMask);
            bool highClear = IsTrajectoryClear(origin, highVelocity, gravity, target, groundMask);
            bool preferLowArc = Vector2.Angle(lowVelocity.normalized, targetDirection.normalized) <= Vector2.Angle(highVelocity.normalized, targetDirection.normalized);

            Vector2 chosenVelocity;
            if (lowClear && highClear)
            {
                chosenVelocity = preferLowArc ? lowVelocity : highVelocity;
            }
            else if (highClear)
            {
                chosenVelocity = highVelocity;
            }
            else if (lowClear)
            {
                chosenVelocity = lowVelocity;
            }
            else
            {
                chosenVelocity = preferLowArc ? lowVelocity : highVelocity;
            }

            if (chosenVelocity.sqrMagnitude <= 0.0001f)
            {
                travelDirection = targetDirection.normalized;
                return false;
            }

            travelDirection = chosenVelocity.normalized;
            return true;
        }

        private static bool IsTrajectoryClear(
            Vector2 origin,
            Vector2 initialVelocity,
            float gravity,
            Vector2 target,
            LayerMask groundMask)
        {
            if (initialVelocity.sqrMagnitude <= 0.01f)
            {
                return false;
            }

            const int sampleCount = 24;
            const float targetRadius = 24f;
            float estimatedFlightTime = ResolveEstimatedFlightTime(origin, target, initialVelocity);
            Vector2 previous = origin;

            for (int step = 1; step <= sampleCount; step += 1)
            {
                float t = estimatedFlightTime * (step / (float)sampleCount);
                Vector2 current = EvaluatePosition(origin, initialVelocity, gravity, t);
                if (groundMask.value != 0 && Physics2D.Linecast(previous, current, groundMask))
                {
                    return false;
                }

                if ((current - target).sqrMagnitude <= targetRadius * targetRadius)
                {
                    return true;
                }

                previous = current;
            }

            return (previous - target).sqrMagnitude <= targetRadius * targetRadius;
        }

        private static bool TrySolveBallisticArc(
            Vector2 origin,
            Vector2 target,
            float speed,
            float gravity,
            out Vector2 lowArcDir,
            out Vector2 highArcDir)
        {
            lowArcDir = highArcDir = Vector2.zero;

            if (speed < 0.1f)
            {
                Vector2 fallback = (target - origin).normalized;
                lowArcDir = fallback;
                highArcDir = fallback;
                return true;
            }

            float dx = target.x - origin.x;
            float dy = target.y - origin.y;
            if (Mathf.Abs(dx) < 1f)
            {
                Vector2 direct = (target - origin).normalized;
                lowArcDir = direct;
                highArcDir = direct;
                return true;
            }

            float speedSq = speed * speed;
            float a = gravity * dx * dx / (2f * speedSq);
            if (Mathf.Abs(a) < 0.0001f)
            {
                return false;
            }

            float b = -dx;
            float c = dy + a;
            float discriminant = b * b - (4f * a * c);
            if (discriminant < 0f)
            {
                return false;
            }

            float sqrtDiscriminant = Mathf.Sqrt(discriminant);
            float tanA = (-b + sqrtDiscriminant) / (2f * a);
            float tanB = (-b - sqrtDiscriminant) / (2f * a);

            Vector2 TanToDirection(float tanValue)
            {
                float cos = 1f / Mathf.Sqrt(1f + (tanValue * tanValue));
                float sin = tanValue * cos;
                return new Vector2(cos * (dx >= 0f ? 1f : -1f), sin).normalized;
            }

            Vector2 first = TanToDirection(tanA);
            Vector2 second = TanToDirection(tanB);
            if (Mathf.Abs(tanA) <= Mathf.Abs(tanB))
            {
                lowArcDir = first;
                highArcDir = second;
            }
            else
            {
                lowArcDir = second;
                highArcDir = first;
            }

            return true;
        }
    }
}
