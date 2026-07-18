using System;
using System.Collections.Generic;
using BalloonParty.Configuration;
using BalloonParty.Shared;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Messages;
using UnityEngine;
using VContainer;

namespace BalloonParty.Game.Score.Behaviours
{
    /// <summary>
    ///     Decomposes a big score into a catalog of 3D shapes and launches them all at once. Every point is one
    ///     orbiting pen trail: the group's total splits greedily over <see cref="ShapeCatalog.Denominations"/>
    ///     (largest-first, remainders recurse), each denomination becomes one formation of that many pens, and a
    ///     terminal remainder of 1 becomes a single classic default trail. The formations bloom around the pop at
    ///     spread sub-centres, tumble and orbit toward the bar, and each reports its own contiguous score range on
    ///     landing — the LARGEST takes the top range (so it carries <c>LastScore</c> and is the principal the
    ///     level-up cinematic tracks). The <see cref="ShapeFormationTicker"/> owns the per-frame simulation; this
    ///     handler only decomposes, lays out, fits, and launches.
    /// </summary>
    internal sealed class BigScoreTrailBehaviour : IScoreTrailBehaviour
    {
        // Each formation's radius stays inside this fraction of the board's half-extent so the largest shape
        // never draws off-screen; a radius past half the extent would pin every sub-centre to the board middle.
        private const float MaxRadiusExtent = 0.9f;
        private const float GoldenAngle = 2.399963f;

        // Out-of-plane tilt blended into the roll axis (see TumbleAxis) — 0 restores pure rolling,
        // ~1 approaches pure in-plane twirl. Promote to the settings SO if it wants live tuning.
        private const float ScreenTwirl = 0.45f;

        // Defensive fallback if the config's curve is unwired; the real asset always supplies one.
        private static readonly BigScoreFormationSettings FallbackSettings =
            new(1.2f, AnimationCurve.EaseInOut(0f, 0f, 1.8f, 0f), 6f, 1.15f, 60f);

        private readonly ShapeFormationTicker _ticker;
        private readonly IScoreTrailBehaviourConfiguration _config;
        private readonly List<int> _denominations = new(16);

        [Inject]
        internal BigScoreTrailBehaviour(ShapeFormationTicker ticker, IScoreTrailBehaviourConfiguration config)
        {
            _ticker = ticker;
            _config = config;
        }

        // The anchor registers immediately at the principal centre, so it can nominate LastScore (unlike the
        // staggered default fan-out): the cinematic's bounded registry wait is timeout-safe from frame one.
        public TrailId GetPrincipalId(in ScorePointsGroupMessage msg)
        {
            return new TrailId(msg.ColorName, msg.LastScore);
        }

        public void Begin(in ScoreTrailContext context)
        {
            Decompose(context.Points, _denominations);
            var settings = _config != null ? _config.BigScoreSettings : FallbackSettings;
            var hasRemainder = _denominations.Count > 0 && _denominations[^1] == 1;
            var formationCount = hasRemainder ? _denominations.Count - 1 : _denominations.Count;

            var limits = new WallLimits(context.Config.LimitsClockwise);
            var fitScale = FitScale(settings.BaseRadius, limits);
            var fittedMaxRadius = settings.BaseRadius * fitScale * MaxRadiusScale();
            var spacing = 2f * fittedMaxRadius;

            var carrierId = new TrailId(context.ColorName, context.LastScore);
            // Clamp the principal (the largest formation) with the max fitted radius so even a 30-sphere stays on-board.
            var principalCenter = ClampCenter(context.Origin, fittedMaxRadius, limits);
            var hasSpinAxis = TumbleAxis(context.HitDirection, out var spinAxis);

            var groupRequest = new BigScoreGroupRequest(
                principalCenter,
                carrierId,
                context.Color,
                context.Target,
                context.Spawner,
                context.Flights,
                context.Reporter,
                settings,
                spinAxis,
                hasSpinAxis,
                context.CancellationToken);

            // Synchronous: registers the anchor in Flights before this returns (the cinematic depends on it).
            var group = _ticker.BeginGroup(in groupRequest);

            var hasHitDirection = context.HitDirection.sqrMagnitude >= 1e-6f;
            var cursor = context.LastScore;
            for (var i = 0; i < formationCount; i++)
            {
                var value = _denominations[i];
                ShapeCatalog.TryGet(value, out var shape);
                var isPrincipal = i == 0;
                var radius = settings.BaseRadius * fitScale * shape.RadiusScale;
                var origin = isPrincipal
                    ? principalCenter
                    : ClampCenter(SubCenter(context.Origin, i, spacing), radius, limits);

                // A hit-aligned shape spawns with its local X along the shot (the line: its slope IS the
                // shot's linear equation); everything else starts at a uniform random orientation.
                var initialRotation = shape.AlignToHit && hasHitDirection
                    ? Quaternion.FromToRotation(Vector3.right, context.HitDirection.normalized)
                    : UnityEngine.Random.rotationUniform;

                _ticker.LaunchFormation(group, new BigScoreFormationRequest(
                    shape, value, cursor, origin, radius, isPrincipal, initialRotation));
                cursor -= value;
            }

            if (hasRemainder)
            {
                // The lone leftover point flies as a classic default trail (parity with DefaultScore's single point).
                SpawnDefaultTrail(in context, cursor);
            }
        }

        // Greedy largest-first over the catalog ladder; remainders recurse. Since 2 and 3 are both denominations
        // the remainder after the pass is 0 or 1, and a terminal 1 (the single default trail) is appended.
        internal static void Decompose(int total, List<int> result)
        {
            result.Clear();
            var denominations = ShapeCatalog.Denominations;
            for (var i = 0; i < denominations.Count; i++)
            {
                var denomination = denominations[i];
                while (total >= denomination)
                {
                    result.Add(denomination);
                    total -= denomination;
                }
            }

            if (total == 1)
            {
                result.Add(1);
            }
        }

        // Rolls the shapes head-over-heels ALONG the hit direction, like a ball struck forward: the roll axis is
        // the screen-plane perpendicular to travel (ω ∝ n × v, n toward the camera = Vector3.back), TILTED out
        // of the plane by ScreenTwirl. The tilt is load-bearing, not flavour: a pure in-plane axis leaves any
        // geometry lying along it motionless (a straight-up shot's roll axis IS the line shape's own local X —
        // both its vertices sit ON the axis), and flat shapes only foreshorten about in-plane axes under the
        // ortho camera. A z component guarantees every shape — lines and flat polygons included — visibly
        // twirls as well as rolls. A near-zero hit direction (item/laser/board pops carry none) leaves the
        // axis unset for a random fallback.
        private static bool TumbleAxis(Vector3 hitDirection, out Vector3 axis)
        {
            if (hitDirection.sqrMagnitude < 1e-6f)
            {
                axis = Vector3.zero;
                return false;
            }

            var roll = Vector3.Cross(Vector3.back, hitDirection).normalized;
            axis = (roll + Vector3.back * ScreenTwirl).normalized;
            return true;
        }

        private void SpawnDefaultTrail(in ScoreTrailContext context, int score)
        {
            var target = context.Target != null ? context.Target.RandomPosition() : Vector3.zero;
            var id = new TrailId(context.ColorName, score);
            var flights = context.Flights;
            var reporter = context.Reporter;

            Action onArrived = () =>
            {
                flights.Unregister(id);
                reporter.ReportArrival(score, points: 1, target);
            };

            var transform = context.Spawner.Spawn(
                context.Origin, target, context.Config.ScorePointTraceDuration, context.Color, onArrived);
            flights.Register(id, transform, context.Origin);
        }

        // Phyllotaxis (golden-angle spiral) so the sub-centres spread evenly; index 0 (the principal) sits at the
        // pop, the rest fan outward at sqrt-growing radii spaced by neighbouring diameters. Off-board centres are
        // pulled in by the per-formation wall clamp (a very large burst just packs densely near the edges).
        private static Vector3 SubCenter(Vector3 origin, int index, float spacing)
        {
            var angle = index * GoldenAngle;
            var distance = spacing * Mathf.Sqrt(index);
            return origin + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * distance;
        }

        private static float MaxRadiusScale()
        {
            var max = 0f;
            var denominations = ShapeCatalog.Denominations;
            for (var i = 0; i < denominations.Count; i++)
            {
                if (ShapeCatalog.TryGet(denominations[i], out var shape) && shape.RadiusScale > max)
                {
                    max = shape.RadiusScale;
                }
            }

            return max > 0f ? max : 1f;
        }

        // Shrinks every formation uniformly so the LARGEST shape's radius fits the board's playable half-extent.
        private static float FitScale(float baseRadius, in WallLimits limits)
        {
            var halfExtent = Mathf.Min(limits.Right - limits.Left, limits.Top - limits.Bottom) * 0.5f;
            var largest = baseRadius * MaxRadiusScale();
            return largest > Mathf.Epsilon ? Mathf.Min(1f, halfExtent * MaxRadiusExtent / largest) : 1f;
        }

        // Shifts the centre inward so C +/- radius stays inside the walls (the radius was already fitted to the
        // board, so a clamp range can only invert on a degenerate play area).
        private static Vector3 ClampCenter(Vector3 origin, float radius, in WallLimits limits)
        {
            var center = origin;
            center.x = ClampAxis(origin.x, limits.Left + radius, limits.Right - radius);
            center.y = ClampAxis(origin.y, limits.Bottom + radius, limits.Top - radius);
            return center;
        }

        private static float ClampAxis(float value, float min, float max)
        {
            // A play area narrower than the shape just centres it rather than clamping to a crossed bound.
            return min > max ? 0.5f * (min + max) : Mathf.Clamp(value, min, max);
        }
    }
}
