using UnityEngine;

namespace BalloonParty.Game.Score.Behaviours
{
    /// <summary>
    ///     Pure, allocation-free pairwise overlap resolution for <see cref="ShapeFormationTicker"/>'s
    ///     BigScore formations — a lightweight, per-frame relaxation pass ("push circles apart"), not a
    ///     real Lubachevsky-Stillinger solver. No Unity-object or <c>ShapeFormationTicker</c> dependency,
    ///     so it's independently testable against plain arrays. Concrete array parameters (not
    ///     <c>IReadOnlyList</c>) so indexing inside the O(n²) loop is direct, not through the array-
    ///     covariance interface shim.
    ///
    ///     This is a JACOBI relaxation: every pair reads the frame's ORIGINAL centers and offsets only
    ///     accumulate, never feed back into the same pass — a 3+-body mutual overlap can overshoot in one
    ///     call. That's deliberate, not a bug: since <see cref="Resolve"/> is recomputed fresh every frame
    ///     against a moving deterministic target (never against a persisted, potentially-drifted offset —
    ///     see <see cref="ShapeFormationTicker.ComputeCandidateCenter"/>), convergence-in-one-pass was
    ///     never the goal. Switching to Gauss-Seidel (feeding corrections back within the same pass) would
    ///     make the result depend on iteration order — which the caller's swap-remove pooling shuffles
    ///     nondeterministically — so don't "fix" the overshoot that way.
    /// </summary>
    internal static class FormationOverlapResolver
    {
        // Golden angle (radians) — the same constant BigScoreTrailBehaviour's phyllotaxis spiral uses,
        // kept local here rather than shared since this is otherwise a fully self-contained algorithm.
        private const float GoldenAngle = 2.399963f;

        private const float DistanceEpsilon = 1e-5f;

        /// <summary>
        ///     Single relaxation pass over the first <paramref name="count"/> entries: for every
        ///     overlapping pair, splits the correction by <paramref name="moveWeights"/> (0 = immovable —
        ///     its partner absorbs the whole correction instead of a 50/50 split) and clamps each
        ///     formation's TOTAL accumulated offset to <c>radius * maxPushFractions[i]</c> (per-formation,
        ///     not a single shared value — two concurrent groups could author different settings).
        ///     Recomputed fresh from the CURRENT <paramref name="centers"/> every call — no persisted
        ///     velocity/spring state — so calling this once per frame against that frame's own
        ///     deterministic travel position settles overlaps without ever drifting from the ideal path.
        ///
        ///     Distances are measured in the XY PLANE ONLY (Z is zeroed before every distance/direction
        ///     computation) — the board reads as flat 2D under an orthographic camera, but formation
        ///     centers travel a large, camera-invisible Z distance toward the score-bar canvas as they fly.
        ///     Resolving in 3D would judge two formations "apart" the instant their travel progress
        ///     differs by even a couple of frames, silently defeating the cross-group case this exists
        ///     for. Projecting to XY also leaves each candidate center's Z (the travel depth curve)
        ///     completely untouched by the correction.
        /// </summary>
        internal static void Resolve(
            Vector3[] centers,
            float[] radii,
            float[] moveWeights,
            float[] paddings,
            float[] maxPushFractions,
            int count,
            Vector3[] offsets)
        {
            for (var i = 0; i < count; i++)
            {
                offsets[i] = Vector3.zero;
            }

            for (var i = 0; i < count; i++)
            {
                for (var j = i + 1; j < count; j++)
                {
                    var minDist = radii[i] + radii[j] + Mathf.Max(paddings[i], paddings[j]);
                    var delta = centers[j] - centers[i];
                    delta.z = 0f;
                    var dist = delta.magnitude;
                    if (dist >= minDist)
                    {
                        continue;
                    }

                    var totalWeight = moveWeights[i] + moveWeights[j];
                    if (totalWeight <= DistanceEpsilon)
                    {
                        continue;
                    }

                    var direction = dist > DistanceEpsilon ? delta / dist : FallbackDirection(i, j);
                    var overlap = minDist - dist;
                    offsets[i] -= direction * (overlap * (moveWeights[i] / totalWeight));
                    offsets[j] += direction * (overlap * (moveWeights[j] / totalWeight));
                }
            }

            for (var i = 0; i < count; i++)
            {
                offsets[i] = Vector3.ClampMagnitude(offsets[i], radii[i] * maxPushFractions[i]);
            }
        }

        // Deterministic separation axis for near-coincident centers (e.g. two formations launched at the
        // exact same origin this frame) — index-derived, so it's stable frame to frame instead of jittering
        // the way a per-frame Random pick would while the pair is still settling apart. Always in the XY
        // plane (z = 0), matching every direction this resolver ever produces.
        private static Vector3 FallbackDirection(int i, int j)
        {
            var angle = (j - i) * GoldenAngle;
            return new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
        }
    }
}
