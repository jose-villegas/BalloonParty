using System.Collections.Generic;
using BalloonParty.Configuration;
using BalloonParty.Shared;
using BalloonParty.Shared.Diagnostics;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Messages;
using UnityEngine;
using VContainer;

namespace BalloonParty.Game.Score.Behaviours
{
    /// <summary>
    ///     Decomposes a big score into a catalog of 3D shapes and launches them all at once. Every point is one
    ///     orbiting pen trail: the group's total splits into the fewest pieces over
    ///     <see cref="ShapeCatalog.Denominations"/> (optimal coin change; see <see cref="Decompose"/>); with 2 and 3
    ///     both denominations, every total this handler ever sees decomposes remainder-free, so each piece becomes
    ///     one formation of that many pens with nothing left over (see <see cref="AssertNoRemainder"/>). The
    ///     formations bloom around the pop at spread sub-centres, tumble and orbit toward the bar, and each reports
    ///     its own contiguous score range on landing — the LARGEST takes the top range (so it carries
    ///     <c>LastScore</c> and is the principal the level-up cinematic tracks). The
    ///     <see cref="ShapeFormationTicker"/> owns the per-frame simulation; this handler only decomposes, lays out,
    ///     fits, and launches.
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

        // A hit-aligned line tilts this far from its flight axis and precesses about it, so the spinning
        // segment (and its persisting ink) sweeps a BICONE along the shot — depth from a 2-vertex shape.
        // 90 would flatten it to a propeller disk, ~0 would hide the spin entirely.
        private const float ConeHalfAngleDegrees = 55f;

        // Defensive fallback if the config's curve is unwired; the real asset always supplies one.
        private static readonly BigScoreFormationSettings FallbackSettings =
            new(1.2f, AnimationCurve.EaseInOut(0f, 0f, 1.8f, 0f), 6f, 1.15f, 60f);

        private readonly ShapeFormationTicker _ticker;
        private readonly IScoreTrailBehaviourConfiguration _config;
        private readonly List<int> _denominations = new(16);

        // Grow-only DP scratch for the optimal decomposition, indexed by total: fewest pieces and whether that
        // count needs a terminal remainder. Main-thread only (Decompose runs on the score-report path), so a
        // shared static buffer never allocates in steady state — it only grows when a larger total appears.
        private static int[] _dpPieces = new int[0];
        private static bool[] _dpRemainder = new bool[0];

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
            AssertNoRemainder(_denominations);
            var settings = _config != null ? _config.BigScoreSettings : FallbackSettings;
            var formationCount = _denominations.Count;

            var limits = new WallLimits(context.FlightConfig.LimitsClockwise);
            var fitScale = FitScale(settings.BaseRadius, limits);
            var fittedMaxRadius = settings.BaseRadius * fitScale * ShapeCatalog.MaxRadiusScale;
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

                if (shape.AlignToHit && hasHitDirection)
                {
                    // The line's slope starts at the shot's linear equation, tilted ConeHalfAngle from the
                    // flight axis, and PRECESSES about that axis — the sweep (and its persisting ink)
                    // traces a bicone along the shot, giving the 2-vertex shape real volume.
                    var flightAxis = context.HitDirection.normalized;
                    var tiltAxis = Vector3.Cross(Vector3.back, flightAxis).normalized;
                    var coneDirection = Quaternion.AngleAxis(ConeHalfAngleDegrees, tiltAxis) * flightAxis;
                    var alignedRotation = Quaternion.FromToRotation(Vector3.right, coneDirection);

                    _ticker.LaunchFormation(group, new BigScoreFormationRequest(
                        shape, value, cursor, origin, radius, isPrincipal, alignedRotation,
                        flightAxis, hasSpinAxis: true));
                }
                else
                {
                    _ticker.LaunchFormation(group, new BigScoreFormationRequest(
                        shape, value, cursor, origin, radius, isPrincipal, UnityEngine.Random.rotationUniform));
                }
                cursor -= value;
            }
        }

        // Optimal coin change over the catalog ladder: the FEWEST pieces, where an unavoidable terminal 1 is a
        // piece carrying a heavy penalty (any remainder-free split beats any split with a remainder, whatever the
        // piece count). Among optimal splits the reconstruction is deterministic — always the largest denomination
        // that stays on an optimal path — which also yields the descending order the pipeline expects
        // (13 = 10+3, not 12+1; 7 = 5+2, not 6+1). 2 and 3 both being denominations, only total 1 needs a remainder.
        internal static void Decompose(int total, List<int> result)
        {
            result.Clear();
            if (total <= 0)
            {
                return;
            }

            var denominations = ShapeCatalog.Denominations;
            ComputeDpCosts(total, denominations);
            ReconstructLargestFirst(total, denominations, result);
        }

        // Decompose can only leave a terminal remainder of 1 if 2 and/or 3 drop out of the catalog ladder — with
        // both present, every total >= 2 (BigScore's floor) decomposes clean. Insurance against that regressing
        // silently, since the default-trail fallback for a leftover 1 was removed as dead code.
        private static void AssertNoRemainder(IReadOnlyList<int> denominations)
        {
            Log.Assert(
                denominations.Count == 0 || denominations[^1] != 1, "BigScoreTrail",
                "Decompose left a terminal remainder of 1 — did 2 or 3 drop out of ShapeCatalog.Denominations?");
        }

        // Fills the grow-only cost tables for every t in [0, total]: the fewest pieces to build t and whether that
        // count needs a terminal remainder. t == 1 is the only total no denomination reaches (min is 2).
        private static void ComputeDpCosts(int total, IReadOnlyList<int> denominations)
        {
            EnsureDpCapacity(total);
            _dpPieces[0] = 0;
            _dpRemainder[0] = false;
            for (var t = 1; t <= total; t++)
            {
                var bestPieces = int.MaxValue;
                var bestRemainder = true;
                for (var i = 0; i < denominations.Count; i++)
                {
                    var d = denominations[i];
                    if (d <= t && IsBetterDp(_dpRemainder[t - d], _dpPieces[t - d] + 1, bestRemainder, bestPieces))
                    {
                        bestRemainder = _dpRemainder[t - d];
                        bestPieces = _dpPieces[t - d] + 1;
                    }
                }

                _dpRemainder[t] = bestPieces == int.MaxValue || bestRemainder;
                _dpPieces[t] = bestPieces == int.MaxValue ? 1 : bestPieces;
            }
        }

        // Deterministic reconstruction: at each step take the largest denomination that stays on an optimal path
        // (this also emits the pieces in descending order). A leftover 1 is the terminal remainder piece.
        private static void ReconstructLargestFirst(int total, IReadOnlyList<int> denominations, List<int> result)
        {
            var remaining = total;
            while (remaining > 1)
            {
                for (var i = 0; i < denominations.Count; i++)
                {
                    var d = denominations[i];
                    if (d <= remaining
                        && _dpPieces[remaining - d] + 1 == _dpPieces[remaining]
                        && _dpRemainder[remaining - d] == _dpRemainder[remaining])
                    {
                        result.Add(d);
                        remaining -= d;
                        break;
                    }
                }
            }

            if (remaining == 1)
            {
                result.Add(1);
            }
        }

        // A candidate split is better when it avoids a remainder the other needs, else when it uses fewer pieces.
        private static bool IsBetterDp(bool remainder, int pieces, bool bestRemainder, int bestPieces)
        {
            if (remainder != bestRemainder)
            {
                return !remainder;
            }

            return pieces < bestPieces;
        }

        private static void EnsureDpCapacity(int total)
        {
            if (_dpPieces.Length >= total + 1)
            {
                return;
            }

            _dpPieces = new int[total + 1];
            _dpRemainder = new bool[total + 1];
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

        // Phyllotaxis (golden-angle spiral) so the sub-centres spread evenly; index 0 (the principal) sits at the
        // pop, the rest fan outward at sqrt-growing radii spaced by neighbouring diameters. Off-board centres are
        // pulled in by the per-formation wall clamp (a very large burst just packs densely near the edges).
        private static Vector3 SubCenter(Vector3 origin, int index, float spacing)
        {
            var angle = index * GoldenAngle;
            var distance = spacing * Mathf.Sqrt(index);
            return origin + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * distance;
        }

        // Shrinks every formation uniformly so the LARGEST shape's radius fits the board's playable half-extent.
        private static float FitScale(float baseRadius, in WallLimits limits)
        {
            var halfExtent = Mathf.Min(limits.Right - limits.Left, limits.Top - limits.Bottom) * 0.5f;
            var largest = baseRadius * ShapeCatalog.MaxRadiusScale;
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
