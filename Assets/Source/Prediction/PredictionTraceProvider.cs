using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Prediction
{
    /// <summary>
    ///     Shared reference to the current aim-prediction trace, decoupled from direct access — mirrors
    ///     <see cref="BalloonParty.Projectile.ProjectilePositionProvider"/>. Readers (e.g. per-actor hit
    ///     markers) poll <see cref="Version"/> instead of subscribing, so 50+ pooled views can cheaply skip
    ///     work on frames where the trace hasn't changed.
    /// </summary>
    internal class PredictionTraceProvider
    {
        // Sub-millimetre in world units (the play area is a few units across) — big enough to absorb
        // float jitter from transform reads each Tick, small enough that no real aim movement hides under it.
        private const float PointEpsilon = 0.0005f;
        private const float PointEpsilonSqr = PointEpsilon * PointEpsilon;

        private readonly List<Vector3> _points = new();

        internal bool IsActive { get; private set; }
        internal int Version { get; private set; }
        internal IReadOnlyList<Vector3> Points => _points;

        /// <summary>Where piercing began and what the line ran into, mirroring
        /// <c>PredictionTraceCalculator.Calculate</c>'s own out parameter.</summary>
        internal PredictionTraceEnd End { get; private set; } = PredictionTraceEnd.OpenAir(-1);

        // Copies rather than aliasing — the writer's buffer (ThrowerController._tracePoints) is mutated
        // in place every Tick, so holding a reference to it would let readers see a half-written trace.
        //
        // Version only bumps when the incoming trace actually differs from what's currently published,
        // so readers gated on it (ItemRangePreviewController, PredictionSightProbe, TraceHitMarker) can
        // skip real work while the aim is held still. The comparison baseline MUST stay the last
        // *published* state — the copy below is skipped along with the bump when nothing changed. If
        // _points were overwritten every frame regardless, a slow sub-epsilon drift would accumulate
        // against a moving baseline and never cross the epsilon, so Version would never bump and readers
        // would never refresh even once the drift became real. Keeping copy-and-bump together by
        // construction is what prevents that.
        internal void SetTrace(IReadOnlyList<Vector3> points, PredictionTraceEnd end = default)
        {
            IsActive = true;

            if (!HasChanged(points, end))
            {
                return;
            }

            _points.Clear();
            if (points != null)
            {
                for (var i = 0; i < points.Count; i++)
                {
                    _points.Add(points[i]);
                }
            }

            End = end;
            Version++;
        }

        internal void Clear()
        {
            if (!IsActive)
            {
                return;
            }

            End = PredictionTraceEnd.OpenAir(-1);
            IsActive = false;
            Version++;
        }

        private bool HasChanged(IReadOnlyList<Vector3> points, PredictionTraceEnd end)
        {
            var count = points?.Count ?? 0;
            if (count != _points.Count)
            {
                return true;
            }

            for (var i = 0; i < count; i++)
            {
                if ((points[i] - _points[i]).sqrMagnitude > PointEpsilonSqr)
                {
                    return true;
                }
            }

            return end.PierceStartIndex != End.PierceStartIndex
                || end.Kind != End.Kind
                || (end.Normal - End.Normal).sqrMagnitude > PointEpsilonSqr;
        }
    }
}
