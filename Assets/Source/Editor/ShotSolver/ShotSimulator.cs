using System;
using System.Collections.Generic;
using BalloonParty.Projectile.Controller;
using BalloonParty.Shared;
using UnityEngine;

namespace BalloonParty.Editor.ShotSolver
{
    /// <summary>One board actor as the solver sees it — enough to reproduce pop/deflect/score rules
    /// without a live <c>IBalloonModel</c>. <see cref="ColorId" /> null/empty means colourless
    /// (mirrors a balloon that does NOT implement <c>IHasColor</c>, e.g. <c>ToughBalloonModel</c>) —
    /// that flag, not <see cref="HitsRemaining" />, is what the simulator uses to choose flat
    /// streak-breaking tough scoring over multiplied green scoring.</summary>
    internal readonly struct ShotBalloonSnapshot
    {
        public readonly Vector2 Position;
        public readonly float Radius;
        public readonly string ColorId;
        public readonly int ScoreValue;
        public readonly int HitsRemaining;

        public ShotBalloonSnapshot(Vector2 position, float radius, string colorId, int scoreValue, int hitsRemaining)
        {
            Position = position;
            Radius = radius;
            ColorId = colorId;
            ScoreValue = scoreValue;
            HitsRemaining = hitsRemaining;
        }
    }

    /// <summary>Mutable per-simulation copy of a <see cref="ShotBalloonSnapshot" /> — <see cref="HitsRemaining" />
    /// decrements on a deflect; a popped entry is swap-removed from the caller's active count.</summary>
    internal struct ShotBalloonState
    {
        public Vector2 Position;
        public float Radius;
        public string ColorId;
        public int ScoreValue;
        public int HitsRemaining;

        public ShotBalloonState(in ShotBalloonSnapshot snapshot)
        {
            Position = snapshot.Position;
            Radius = snapshot.Radius;
            ColorId = snapshot.ColorId;
            ScoreValue = snapshot.ScoreValue;
            HitsRemaining = snapshot.HitsRemaining;
        }
    }

    /// <summary>Outcome of one deterministic flight — see <see cref="ShotSimulator.Simulate" />.</summary>
    internal readonly struct ShotSimulationResult
    {
        public readonly int RawScore;
        public readonly int Pops;
        public readonly int ToughsCleared;
        public readonly bool BoardCleared;
        public readonly int Events;
        public readonly bool Died;
        public readonly bool Capped;

        public ShotSimulationResult(
            int rawScore, int pops, int toughsCleared, bool boardCleared, int events, bool died, bool capped)
        {
            RawScore = rawScore;
            Pops = pops;
            ToughsCleared = toughsCleared;
            BoardCleared = boardCleared;
            Events = events;
            Died = died;
            Capped = capped;
        }
    }

    /// <summary>Pure, headless, deterministic billiard simulator for one aim direction (see
    /// @ref plan_shot_geometry). Motion is linear at constant speed, so speed never enters the model —
    /// flight is simulated EVENT TO EVENT (next analytic wall crossing or next analytic balloon-corridor
    /// entry along the ray), not fixed-step, which keeps a full sweep of thousands of angles cheap and
    /// exact rather than an approximation of one. Reuses
    /// <see cref="ProjectileMotionResolver.TryComputeContactNormal" /> for deflect contacts — the exact
    /// entry point this class already solved for satisfies that method's own backtrack (it returns
    /// backtrack = 0 there), so a simulated deflect lands on the identical normal a live shot would.</summary>
    internal static class ShotSimulator
    {
        internal const int DefaultMaxEvents = 500;

        // Below this, a candidate crossing is the event we just resolved, not a new one — otherwise a
        // ray sitting exactly on a wall or circle boundary re-triggers the same event forever.
        private const float EventEpsilon = 1e-4f;

        // Below this, a direction's axis component is treated as parallel to that wall pair (no
        // crossing possible), avoiding a near-zero divide in the analytic wall-time formula.
        private const float AxisEpsilon = 1e-6f;

        /// <summary>Simulates one aim direction to completion. <paramref name="workingSet" /> is a
        /// caller-owned scratch buffer (sized to at least <paramref name="board" />.Count) reused
        /// across calls — the only per-call cost is copying the board into it, so a sweep of
        /// thousands of angles allocates nothing. <paramref name="pathOut" />, when non-null, is
        /// cleared and filled with the flight's event positions (origin first) for scene-view
        /// drawing — leave it null during a bulk sweep.</summary>
        internal static ShotSimulationResult Simulate(
            IReadOnlyList<ShotBalloonSnapshot> board,
            Vector4 wallLimitsClockwise,
            Vector2 origin,
            Vector2 aimDirection,
            int startingShields,
            float projectileContactRadius,
            ShotBalloonState[] workingSet,
            int maxEvents = DefaultMaxEvents,
            List<Vector2> pathOut = null)
        {
            var walls = new WallLimits(wallLimitsClockwise);
            var activeCount = CopyIntoWorkingSet(board, workingSet);

            var position = origin;
            var direction = aimDirection.sqrMagnitude > AxisEpsilon ? aimDirection.normalized : Vector2.right;
            var shields = startingShields;

            string streakColor = null;
            var streakCount = 0;
            string projectileColor = null;

            var rawScore = 0;
            var pops = 0;
            var toughsCleared = 0;
            var events = 0;
            var died = false;
            var capped = false;

            if (pathOut != null)
            {
                pathOut.Clear();
                pathOut.Add(position);
            }

            while (activeCount > 0)
            {
                if (events >= maxEvents)
                {
                    capped = true;
                    break;
                }

                var hasWallEvent = TryFindWallCrossing(walls, position, direction, out var wallT, out var wallNormal);
                var hasBalloonEvent = TryFindNearestBalloonEntry(
                    workingSet, activeCount, position, direction, projectileContactRadius,
                    out var balloonT, out var balloonIndex);

                if (!hasWallEvent && !hasBalloonEvent)
                {
                    break;
                }

                events++;

                if (hasBalloonEvent && (!hasWallEvent || balloonT < wallT))
                {
                    position += direction * balloonT;
                    pathOut?.Add(position);
                    ResolveBalloonContact(
                        workingSet, ref activeCount, balloonIndex, position, projectileContactRadius,
                        ref direction, ref streakColor, ref streakCount, ref projectileColor,
                        ref rawScore, ref pops, ref toughsCleared, ref shields);
                    continue;
                }

                position += direction * wallT;
                pathOut?.Add(position);
                shields--;
                if (shields < 0)
                {
                    died = true;
                    break;
                }

                direction = Vector2.Reflect(direction, wallNormal.normalized);
            }

            return new ShotSimulationResult(rawScore, pops, toughsCleared, activeCount == 0, events, died, capped);
        }

        private static int CopyIntoWorkingSet(IReadOnlyList<ShotBalloonSnapshot> board, ShotBalloonState[] workingSet)
        {
            var count = Mathf.Min(board.Count, workingSet.Length);
            for (var i = 0; i < count; i++)
            {
                workingSet[i] = new ShotBalloonState(board[i]);
            }

            return count;
        }

        // hitsRemaining > 1 survives as a deflect (mirrors BalloonModelBase.EvaluateNormalHit, damage
        // always 1 for a direct hit); == 1 pops. The pop path scores via the flat/streak-breaking tough
        // rule or the multiplied/colour-adopting green rule (see the class doc), then swap-removes the
        // entry — the ray pierces on, unbent.
        private static void ResolveBalloonContact(
            ShotBalloonState[] workingSet, ref int activeCount, int index, Vector2 contactPosition,
            float projectileContactRadius, ref Vector2 direction,
            ref string streakColor, ref int streakCount, ref string projectileColor,
            ref int rawScore, ref int pops, ref int toughsCleared, ref int shields)
        {
            ref var balloon = ref workingSet[index];

            if (balloon.HitsRemaining > 1)
            {
                balloon.HitsRemaining--;
                DeflectOffBalloon(balloon.Position, balloon.Radius + projectileContactRadius, contactPosition, ref direction);
                return;
            }

            if (string.IsNullOrEmpty(balloon.ColorId))
            {
                ResolveToughPop(balloon.ScoreValue, ref streakColor, ref streakCount, ref rawScore, ref toughsCleared);
            }
            else
            {
                ResolveGreenPop(
                    balloon.ColorId, balloon.ScoreValue, ref streakColor, ref streakCount, ref projectileColor,
                    ref rawScore, ref shields);
            }

            pops++;
            RemoveActive(workingSet, ref activeCount, index);
        }

        private static void DeflectOffBalloon(
            Vector2 balloonPosition, float combinedRadius, Vector2 contactPosition, ref Vector2 direction)
        {
            if (!ProjectileMotionResolver.TryComputeContactNormal(
                    contactPosition, direction, balloonPosition, combinedRadius, out var normal))
            {
                // Same degenerate fallback as ProjectileMotionResolver.Deflect — a radial normal off
                // the (here, exact) contact point is still a sane reflection.
                normal = (contactPosition - balloonPosition).normalized;
            }

            direction = Vector2.Reflect(direction, normal);
        }

        // Tough pops always reset the streak and score their flat ScoreValue — mirrors
        // ScoreController.RecordStreakMultiplier collapsing ToughBalloonModel's per-point
        // breaksStreak:true attributions to a locked ×1 multiplier regardless of ScoreValue.
        private static void ResolveToughPop(
            int scoreValue, ref string streakColor, ref int streakCount, ref int rawScore, ref int toughsCleared)
        {
            rawScore += scoreValue;
            toughsCleared++;
            streakColor = null;
            streakCount = 0;
        }

        // Mirrors ProjectileHitResolver: colour adoption (ApplyColorChange) off the projectile's OLD
        // colour first, then the streak record (ColorStreakTracker.Record) off the balloon's own
        // colour, then the shield refund (streak >= 2 of the projectile's now-current colour).
        private static void ResolveGreenPop(
            string colorId, int scoreValue, ref string streakColor, ref int streakCount, ref string projectileColor,
            ref int rawScore, ref int shields)
        {
            if (!string.Equals(projectileColor, colorId, StringComparison.Ordinal))
            {
                projectileColor = colorId;
            }

            streakCount = string.Equals(streakColor, colorId, StringComparison.Ordinal) ? streakCount + 1 : 1;
            streakColor = colorId;
            rawScore += scoreValue * streakCount;

            if (streakCount >= 2 && string.Equals(streakColor, projectileColor, StringComparison.Ordinal))
            {
                shields++;
            }
        }

        private static void RemoveActive(ShotBalloonState[] workingSet, ref int activeCount, int index)
        {
            activeCount--;
            workingSet[index] = workingSet[activeCount];
        }

        // Nearest analytic line-circle entry among the active set — same family as
        // TraceHitGeometry.TryFindSurfaceHit / ProjectileMotionResolver.TryComputeContactNormal, solved
        // here for the smallest positive entry distance rather than a backtrack.
        private static bool TryFindNearestBalloonEntry(
            ShotBalloonState[] workingSet, int activeCount, Vector2 position, Vector2 direction,
            float projectileContactRadius, out float bestT, out int bestIndex)
        {
            bestT = float.PositiveInfinity;
            bestIndex = -1;

            for (var i = 0; i < activeCount; i++)
            {
                var combinedRadius = workingSet[i].Radius + projectileContactRadius;
                var toCenter = position - workingSet[i].Position;
                var along = Vector2.Dot(toCenter, direction);
                var discriminant = (along * along) - toCenter.sqrMagnitude + (combinedRadius * combinedRadius);
                if (discriminant < 0f)
                {
                    continue;
                }

                var entryT = -along - Mathf.Sqrt(discriminant);
                if (entryT <= EventEpsilon || entryT >= bestT)
                {
                    continue;
                }

                bestT = entryT;
                bestIndex = i;
            }

            return bestIndex >= 0;
        }

        // Analytic per-axis wall time — only walls the ray is heading toward are candidates. A tie
        // within EventEpsilon sums both normals, mirroring WallLimits.Clamp's simultaneous-crossing
        // (exact corner) case.
        private static bool TryFindWallCrossing(
            in WallLimits walls, Vector2 position, Vector2 direction, out float bestT, out Vector2 normal)
        {
            bestT = float.PositiveInfinity;
            normal = Vector2.zero;
            var hasCandidate = false;

            TryAxisCandidate(direction.x, walls.Right - position.x, Vector2.left, ref bestT, ref normal, ref hasCandidate);
            TryAxisCandidate(-direction.x, position.x - walls.Left, Vector2.right, ref bestT, ref normal, ref hasCandidate);
            TryAxisCandidate(direction.y, walls.Top - position.y, Vector2.down, ref bestT, ref normal, ref hasCandidate);
            TryAxisCandidate(-direction.y, position.y - walls.Bottom, Vector2.up, ref bestT, ref normal, ref hasCandidate);

            return hasCandidate;
        }

        private static void TryAxisCandidate(
            float rate, float distance, Vector2 wallNormal, ref float bestT, ref Vector2 normal, ref bool hasCandidate)
        {
            if (rate <= AxisEpsilon)
            {
                return;
            }

            var candidateT = distance / rate;
            if (candidateT <= EventEpsilon)
            {
                return;
            }

            if (!hasCandidate || candidateT < bestT - EventEpsilon)
            {
                bestT = candidateT;
                normal = wallNormal;
                hasCandidate = true;
                return;
            }

            if (candidateT < bestT + EventEpsilon)
            {
                normal += wallNormal;
            }
        }
    }
}
