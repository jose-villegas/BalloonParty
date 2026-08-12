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
        private readonly List<Vector3> _points = new();

        internal bool IsActive { get; private set; }
        internal int Version { get; private set; }
        internal IReadOnlyList<Vector3> Points => _points;

        /// <summary>Where piercing began and what the line ran into, mirroring
        /// <c>PredictionTraceCalculator.Calculate</c>'s own out parameter.</summary>
        internal PredictionTraceEnd End { get; private set; } = PredictionTraceEnd.OpenAir(-1);

        // Copies rather than aliasing — the writer's buffer (ThrowerController._tracePoints) is mutated
        // in place every Tick, so holding a reference to it would let readers see a half-written trace.
        internal void SetTrace(IReadOnlyList<Vector3> points, PredictionTraceEnd end = default)
        {
            _points.Clear();
            if (points != null)
            {
                for (var i = 0; i < points.Count; i++)
                {
                    _points.Add(points[i]);
                }
            }

            End = end;
            IsActive = true;
            Version++;
        }

        internal void Clear()
        {
            End = PredictionTraceEnd.OpenAir(-1);
            IsActive = false;
            Version++;
        }
    }
}
