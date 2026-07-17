using UnityEngine;

namespace BalloonParty.Editor.ShotSolver
{
    /// <summary>Pure math shared by the dynamic-board sim: the nudge envelope (mirrors
    /// <c>BalloonMotionTicker.Reach</c> exactly — @ref plan_shot_geometry §7c) and the moving-circle
    /// entry solve (relative-velocity quadratic, §7b) used for contacts against a balloon whose centre
    /// is a function of time rather than fixed. No allocation, no state — safe to call from the sim's
    /// hot per-event loop (thousands of calls per sweep).</summary>
    internal static class ShotMotionMath
    {
        // Below this the relative-velocity term is ~0 (projectile and balloon moving in lockstep) —
        // degenerate for the quadratic (a ~ 0). No realistic speed combination reaches this, but the
        // guard keeps the solve from dividing by ~0.
        private const float RelativeVelocityEpsilonSqr = 1e-8f;
        private const float EntryEpsilon = 1e-4f;

        /// <summary>Analytic out-and-back envelope — ease-out-quad to 1 at progress 0.5, mirrored back
        /// to 0 — identical to <c>BalloonMotionTicker.Reach</c>.</summary>
        internal static float Reach(float progress)
        {
            return progress < 0.5f
                ? EaseOutQuad(progress * 2f)
                : 1f - EaseOutQuad((progress - 0.5f) * 2f);
        }

        /// <summary>Smallest positive distance travelled by a projectile moving at <paramref name="speed" />
        /// along unit <paramref name="direction" /> from <paramref name="origin" /> at which it enters a
        /// circle of <paramref name="combinedRadius" /> centred at <paramref name="center" /> moving at
        /// constant world-units/second <paramref name="velocity" />. Distance-parameterised (like the
        /// static sim's wall/balloon solves) rather than time-parameterised: with
        /// <c>d</c> the projectile's travelled distance, the projectile sits at
        /// <c>origin + d·direction</c> and the balloon at <c>center + (d/speed)·velocity</c>, so the
        /// relative displacement is <c>(origin − center) + d·(direction − velocity/speed)</c>. Solving
        /// its squared length against <paramref name="combinedRadius" /> reduces to the EXACT static
        /// line-circle entry when <paramref name="velocity" /> is zero (no rounding introduced — the
        /// velocity/speed term is exactly zero regardless of speed).</summary>
        internal static bool TrySolveMovingEntry(
            Vector2 origin, Vector2 direction, float speed, Vector2 center, Vector2 velocity,
            float combinedRadius, out float distance)
        {
            distance = 0f;
            if (speed <= 0f)
            {
                return false;
            }

            var relative = direction - (velocity / speed);
            var toCenter = origin - center;
            var a = relative.sqrMagnitude;
            if (a < RelativeVelocityEpsilonSqr)
            {
                return false;
            }

            var b = 2f * Vector2.Dot(toCenter, relative);
            var c = toCenter.sqrMagnitude - (combinedRadius * combinedRadius);
            var discriminant = (b * b) - (4f * a * c);
            if (discriminant < 0f)
            {
                return false;
            }

            var d = (-b - Mathf.Sqrt(discriminant)) / (2f * a);
            if (d <= EntryEpsilon)
            {
                return false;
            }

            distance = d;
            return true;
        }

        private static float EaseOutQuad(float t)
        {
            return 1f - (1f - t) * (1f - t);
        }
    }
}
