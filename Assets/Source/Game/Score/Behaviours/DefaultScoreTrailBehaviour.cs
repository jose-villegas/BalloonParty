using System;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pool;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace BalloonParty.Game.Score.Behaviours
{
    /// <summary>
    ///     Byte-for-byte parity with the pre-seam pipeline: one pooled trail per point, fanned out into a
    ///     scatter ring and staggered, each reporting a single point on landing.
    /// </summary>
    internal sealed class DefaultScoreTrailBehaviour : IScoreTrailBehaviour
    {
        public TrailId GetPrincipalId(in ScorePointsGroupMessage msg)
        {
            // The FIRST trail spawns immediately, so its registration is timeout-safe under scatter stagger;
            // the last trail can spawn seconds later and would race the cinematic's bounded registry wait.
            return new TrailId(msg.ColorName, msg.FirstScore);
        }

        public void Begin(in ScoreTrailContext context)
        {
            SpawnGroupAsync(context).Forget();
        }

        // One state machine per group reproduces today's per-point schedule (spawn i at 0.02 s × i):
        // the first spawn is immediate, then each iteration awaits until its shared-start target time.
        // Scheduling against t0 (scaled, like the delay) instead of chaining fixed waits keeps frame
        // rounding from accumulating per step — a chained 20 ms wait rounds up to a whole frame every
        // iteration, stretching a long group's tail by tens of percent at 60 Hz. A late frame simply
        // spawns the overdue points immediately, exactly like the old parallel per-point delays.
        private static async UniTaskVoid SpawnGroupAsync(ScoreTrailContext context)
        {
            var center = context.Origin;
            var count = context.Points;
            var delay = context.ScoreConfig.ScorePointsScatterDelay;
            var start = Time.time;

            for (var i = 0; i < count; i++)
            {
                var remainingMs = Mathf.RoundToInt((start + delay * i - Time.time) * 1000f);
                if (remainingMs > 0)
                {
                    await UniTask.Delay(remainingMs, cancellationToken: context.CancellationToken);
                }

                var score = context.FirstScore + i;
                var id = new TrailId(context.ColorName, score);
                var origin = ComputeScatterOrigin(in context, center, i, count);
                SpawnTrail(in context, center, origin, id, score);
            }
        }

        private static Vector3 ComputeScatterOrigin(in ScoreTrailContext context, Vector3 center, int index, int count)
        {
            if (count <= 1)
            {
                return center;
            }

            var radius = Mathf.Min(context.GridConfig.SlotSeparation.x, context.GridConfig.SlotSeparation.y) * 1.5f;
            var angle = 2f * Mathf.PI * index / count;
            Vector3 direction = VectorMathExtensions.DirectionFromAngle(angle);
            return center + direction * radius;
        }

        private static void SpawnTrail(in ScoreTrailContext context, Vector3 center, Vector3 scatterOrigin, TrailId id, int score)
        {
            var target = context.Target != null ? context.Target.RandomPosition() : Vector3.zero;
            var color = context.Color;
            var spawner = context.Spawner;
            var flights = context.Flights;
            var reporter = context.Reporter;
            var config = context.ScoreConfig;
            var hasBurst = scatterOrigin != center;

            Action onArrived = () =>
            {
                flights.Unregister(id);
                reporter.ReportArrival(score, points: 1, target);
            };

            Transform transform;
            if (hasBurst)
            {
                transform = spawner.SpawnBurst(center,
                    scatterOrigin,
                    target,
                    config.ScorePointBurstDuration,
                    config.ScorePointTraceDuration,
                    color,
                    onArrived);
            }
            else
            {
                transform = spawner.Spawn(scatterOrigin,
                    target,
                    config.ScorePointTraceDuration,
                    color,
                    onArrived);
            }

            flights.Register(id, transform, center);
        }
    }
}
