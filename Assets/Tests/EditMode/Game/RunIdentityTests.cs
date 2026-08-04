using BalloonParty.Balloon.Type;
using BalloonParty.Game.Run;
using BalloonParty.Game.Telemetry;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Capabilities;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Game
{
    // Run identity across retries (R14–R14c, RK-3/3a/3b). A retry is a continuation of ONE run — the
    // score carries over and the domain records it as the same run — while RunController's generation
    // increments on every reset, so using the generation as run identity corrupts every levels-reached
    // distribution.
    //
    // NOTE: NSubstitute returns 0 for IRetryState.RetryLevel by default, so every retry case below sets
    // it explicitly. A test that forgets silently exercises the FRESH-run branch and passes green.
    [TestFixture]
    public class RunIdentityTests
    {
        private GameplayMetricsHarness _harness;

        [SetUp]
        public void SetUp()
        {
            _harness = new GameplayMetricsHarness();
            _harness.EnterGame();
        }

        [TearDown]
        public void TearDown()
        {
            _harness.Dispose();
        }

        [Test]
        public void ResetOrder_RunsBeforeRetryTrackerZeroesRetryLevel()
        {
            var config = Substitute.For<IRunConfig>();
            config.MaxRetries.Returns(3);
            var tracker = new RetryTracker(config);

            // RK-4: the whole three-id derivation reads IRetryState.RetryLevel, and RetryTracker zeroes
            // it. If these ever cross, every retry silently reports as a fresh run.
            Assert.Less(_harness.Service.ResetOrder, tracker.ResetOrder);
            Assert.AreEqual(RunResetOrder.Quiesce, _harness.Service.ResetOrder);
        }

        [Test]
        public void FirstRunOfTheSession_StartsAtRunOneAttemptZero()
        {
            Play();
            _harness.GameOver.Handle(new GameOverMessage(3, 1200));

            var run = _harness.LastOf(RecordKind.Run);
            Assert.AreEqual(1, run.RunId);
            Assert.AreEqual(1, run.AttemptId);
            Assert.AreEqual(0, run.AttemptIndex);
        }

        [Test]
        public void ScriptedRetrySequence_HoldsTheChainAndWalksTheAttemptIndex()
        {
            // Reset 1 — fresh: a new chain.
            _harness.RetryState.RetryLevel.Returns(0);
            _harness.Service.ResetRun(2);
            Play();
            _harness.GameOver.Handle(new GameOverMessage(4, 500));
            var afterFresh = _harness.LastOf(RecordKind.Run);

            // Reset 2 — retry: same chain, next attempt.
            _harness.RetryState.RetryLevel.Returns(4);
            _harness.Service.ResetRun(3);
            Play();
            _harness.GameOver.Handle(new GameOverMessage(4, 900));
            var afterRetry = _harness.LastOf(RecordKind.Run);

            // Reset 3 — fresh again: the chain advances and the attempt index falls back to 0.
            _harness.RetryState.RetryLevel.Returns(0);
            _harness.Service.ResetRun(4);
            Play();
            _harness.GameOver.Handle(new GameOverMessage(2, 300));
            var afterSecondFresh = _harness.LastOf(RecordKind.Run);

            Assert.AreEqual(2, afterFresh.RunId);
            Assert.AreEqual(2, afterRetry.RunId, "RunId must hold across a retry — it is the run chain.");
            Assert.AreEqual(3, afterSecondFresh.RunId);

            Assert.AreEqual(2, afterFresh.AttemptId);
            Assert.AreEqual(3, afterRetry.AttemptId);
            Assert.AreEqual(4, afterSecondFresh.AttemptId, "AttemptId mirrors the generation — every reset.");

            Assert.AreEqual(0, afterFresh.AttemptIndex);
            Assert.AreEqual(1, afterRetry.AttemptIndex);
            Assert.AreEqual(0, afterSecondFresh.AttemptIndex);
        }

        [Test]
        public void RetriesUsed_TracksTheAttemptIndexOnTheRunRecord()
        {
            _harness.RetryState.RetryLevel.Returns(7);
            _harness.Service.ResetRun(2);
            _harness.RetryState.RetryLevel.Returns(7);
            _harness.Service.ResetRun(3);

            Play();
            _harness.GameOver.Handle(new GameOverMessage(7, 100));

            Assert.AreEqual(2, _harness.LastOf(RecordKind.Run).Metrics[MetricId.RetriesUsed]);
        }

        [Test]
        public void LevelAttemptOrdinal_CountsTheReplayedLevelOnly()
        {
            // Walk the chain up to level 3, so level 3 has been opened exactly once.
            _harness.CompleteLevel(2);
            _harness.CompleteLevel(3);

            Play();
            _harness.GameOver.Handle(new GameOverMessage(3, 400));
            Assert.AreEqual(1, _harness.LastOf(RecordKind.Level).LevelAttemptOrdinal);

            // Retry at level 3: the same level number opens a second time within the chain.
            _harness.RetryState.RetryLevel.Returns(3);
            _harness.Service.ResetRun(2);

            Play();
            _harness.CompleteLevel(4);
            Assert.AreEqual(2, _harness.LastOf(RecordKind.Level).LevelAttemptOrdinal,
                "The replayed level is the second try.");

            // Level 4 happens during attempt 1 but is a FIRST attempt — this is exactly what
            // AttemptIndex cannot express (RK-3a).
            Play();
            _harness.CompleteLevel(5);
            var levelFour = _harness.LastOf(RecordKind.Level);
            Assert.AreEqual(1, levelFour.LevelAttemptOrdinal);
            Assert.AreEqual(1, levelFour.AttemptIndex, "…while AttemptIndex still says 'second attempt'.");
        }

        [Test]
        public void FreshRun_ClearsTheAttemptTable()
        {
            _harness.CompleteLevel(2);
            Play();
            _harness.GameOver.Handle(new GameOverMessage(2, 100));

            _harness.RetryState.RetryLevel.Returns(0);
            _harness.Service.ResetRun(2);
            _harness.CompleteLevel(2);

            Assert.AreEqual(1, _harness.LastOf(RecordKind.Level).LevelAttemptOrdinal);
        }

        [Test]
        public void TotalScore_ComesFromGameOverAndIsNotSummedAcrossLevels()
        {
            // Two levels banking 100 each; a naive sum would report 200 on the run record.
            _harness.TrailArrived.Handle(new ScoreTrailArrivedMessage(GameplayMetricsHarness.Red, 100, 100,
                Vector3.zero));
            _harness.CompleteLevel(2);
            _harness.TrailArrived.Handle(new ScoreTrailArrivedMessage(GameplayMetricsHarness.Red, 200, 100,
                Vector3.zero));
            _harness.CompleteLevel(3);

            Play();
            _harness.GameOver.Handle(new GameOverMessage(3, 175));

            Assert.AreEqual(175, _harness.LastOf(RecordKind.Run).Metrics[MetricId.TotalScore],
                "RK-3b: a chain with a replayed level has two records for it, so summing double-counts.");
        }

        // TotalScore is its own metric rather than an override of PointsBanked, because PointsBanked
        // is a Sum metric carrying a Color axis: the roll-up builds both its scalar and its per-colour
        // buckets, so overwriting just the scalar would ship a run record whose points_banked
        // disagreed with the sum of its own points_banked_color.
        [Test]
        public void RunRecord_PointsBanked_StillAgreesWithItsOwnColorBuckets()
        {
            _harness.TrailArrived.Handle(new ScoreTrailArrivedMessage(GameplayMetricsHarness.Red, 100, 100,
                Vector3.zero));
            _harness.TrailArrived.Handle(new ScoreTrailArrivedMessage(GameplayMetricsHarness.Blue, 140, 40,
                Vector3.zero));
            _harness.CompleteLevel(2);

            Play();
            _harness.GameOver.Handle(new GameOverMessage(2, 999));

            var run = _harness.LastOf(RecordKind.Run);
            var buckets = run.Metrics.AxisBucketsOf(MetricId.PointsBanked, MetricAxis.Color);
            var bucketSum = 0;
            foreach (var value in buckets)
            {
                bucketSum += value;
            }

            Assert.AreEqual(run.Metrics[MetricId.PointsBanked], bucketSum,
                "the scalar and its axis must describe the same points");
            Assert.AreEqual(999, run.Metrics[MetricId.TotalScore], "and the final score lives elsewhere");
        }

        // Any real play at all, so the partial level record at game over is not skipped as a phantom.
        private void Play()
        {
            _harness.ProjectileFired.Handle(new ProjectileFiredMessage(Vector3.zero, Vector3.up));
            _harness.ActorHit.Handle(GameplayMetricsHarness.Hit(
                GameplayMetricsHarness.Balloon(BalloonType.Simple, GameplayMetricsHarness.Red),
                HitOutcome.Pop));
        }
    }
}
