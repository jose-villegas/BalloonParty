using System;
using System.Threading;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration.Items;
using BalloonParty.Game.Telemetry;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pause;
using BalloonParty.Slots.Capabilities;
using Cysharp.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Game
{
    // Orchestration and boundaries for GameplayMetricsService (R2, R3, R8/R8a, R9–R11, R13, R15–R17,
    // R21–R23, R28c). Every [Test] here is deliberately public — NUnit silently SKIPS a non-public test
    // method in this project, which has produced false green before.
    [TestFixture]
    public class GameplayMetricsServiceTests
    {
        private GameplayMetricsHarness _harness;

        [SetUp]
        public void SetUp()
        {
            _harness = new GameplayMetricsHarness();
        }

        [TearDown]
        public void TearDown()
        {
            _harness.Dispose();
        }

        [Test]
        public void BeforeNavigationReachesGame_NothingIsCounted()
        {
            // R23: entry points start during the Launcher's additive preload. Clocks starting in Start()
            // would charge the whole main-menu dwell to level 1.
            _harness.Now = 90f;
            _harness.ScoreLevelUp.Handle(new ScoreLevelUpMessage(2));
            _harness.LevelUpDismissed.Handle(new LevelUpDismissedMessage());
            _harness.TransitionCompleted.Handle(new LevelTransitionCompletedMessage());

            Assert.AreEqual(0, _harness.Sink.Written.Count);
        }

        // The clock must start when navigation reaches Game, not when the service is constructed —
        // entry points start during the Launcher's additive preload, long before the player taps Play.
        //
        // Time is advanced BEFORE EnterGame on purpose. With Now still at 0 there, a RefreshClocks()
        // slipped into the constructor reads the same 0 as one in OnNavigationChanged and the two are
        // indistinguishable; the 30 seconds of "preload" below is what makes them differ.
        [Test]
        public void EnteringGame_StartsTheWallAndGameplayClocks_AndNotOneTickBefore()
        {
            _harness.Now = 30f; // the service has existed since preload, unstarted
            _harness.EnterGame();
            _harness.Now = 42f;
            Play();
            _harness.CompleteLevel(2);

            var level = _harness.LastOf(RecordKind.Level);
            Assert.AreEqual(12f, level.Metrics[TimerId.Gameplay], 0.01f, "12s of play, not 42s since construction");
            Assert.AreEqual(12f, level.Metrics[TimerId.Wall], 0.01f);
        }

        [Test]
        public void LevelIndex_ComesFromScoreLevelUpNewLevelMinusOne()
        {
            _harness.EnterGame();
            Play();

            // Fired cold with NewLevel 5 — never an internal counter, because CheatState.StartLevel lets
            // a dev run start anywhere and a counter drifts immediately.
            _harness.CompleteLevel(5);

            Assert.AreEqual(4, _harness.LastOf(RecordKind.Level).LevelIndex);
        }

        // The two sources are offset from each other: NewLevel is the level being entered, so
        // NewLevel - 1 names the one just completed; FinalLevel is the level being played, which is
        // already the level the partial record is about. Subtracting there would file a death on
        // level 2 under completed-level-1's index — two different levels in one bucket, and the
        // levels-reached distribution silently wrong.
        [Test]
        public void PartialRecordAtGameOver_TakesFinalLevelUnchanged_AndDoesNotCollideWithTheCompletedLevelBelowIt()
        {
            _harness.EnterGame();
            Play();
            _harness.CompleteLevel(2); // completes level 1 → index 1

            Play();
            _harness.GameOver.Handle(new GameOverMessage(2, 500)); // dies on level 2 → index 2

            var levels = _harness.AllOf(RecordKind.Level);
            Assert.AreEqual(2, levels.Count, "one completed record and one partial");
            Assert.AreEqual(1, levels[0].LevelIndex, "the completed level");
            Assert.AreEqual(2, levels[1].LevelIndex, "the level died on — not 1");
            Assert.IsTrue(levels[0].Completed);
            Assert.IsFalse(levels[1].Completed);
        }

        [Test]
        public void StragglerPoints_AfterDismissal_ChargeToTheCompletedLevel()
        {
            _harness.EnterGame();
            Play();
            _harness.TrailArrived.Handle(new ScoreTrailArrivedMessage(GameplayMetricsHarness.Red, 40, 40,
                Vector3.zero));

            _harness.ScoreLevelUp.Handle(new ScoreLevelUpMessage(2));
            _harness.LevelUpDismissed.Handle(new LevelUpDismissedMessage());

            // HoldOutgoingContent triggers CompleteAll, so trails land between the dismissal and the
            // transition. Flushing on the dismissal would charge these to the NEXT level (guardrail 4).
            _harness.TrailArrived.Handle(new ScoreTrailArrivedMessage(GameplayMetricsHarness.Red, 55, 15,
                Vector3.zero));
            _harness.TransitionCompleted.Handle(new LevelTransitionCompletedMessage());

            Assert.AreEqual(55, _harness.LastOf(RecordKind.Level).Metrics[MetricId.PointsBanked]);
        }

        [Test]
        public void CeremonySnapshot_IsImmutableAndDivergesFromTheFlushSnapshot()
        {
            _harness.EnterGame();
            Play();
            _harness.TrailArrived.Handle(new ScoreTrailArrivedMessage(GameplayMetricsHarness.Red, 40, 40,
                Vector3.zero));
            _harness.TotalScore.Value = 900;

            _harness.ScoreLevelUp.Handle(new ScoreLevelUpMessage(2));
            var ceremony = _harness.Service.CeremonyLevel.Value;

            _harness.LevelUpDismissed.Handle(new LevelUpDismissedMessage());
            _harness.TrailArrived.Handle(new ScoreTrailArrivedMessage(GameplayMetricsHarness.Red, 55, 15,
                Vector3.zero));
            _harness.TransitionCompleted.Handle(new LevelTransitionCompletedMessage());

            // R17: the two snapshots differ by exactly the stragglers, and that gap is data.
            Assert.AreEqual(40, ceremony[MetricId.PointsBanked], "The ceremony snapshot must not move.");
            Assert.AreEqual(55, _harness.Service.LastFlushedLevel.Value[MetricId.PointsBanked]);

            // R18: the popup renders PROJECTED points, matching the score shown beside it.
            Assert.AreEqual(900, ceremony[MetricId.PointsProjected]);
        }

        [Test]
        public void LevelUpAborted_ClearsTheCeremonySnapshotAndDoesNotFlush()
        {
            _harness.EnterGame();
            Play();
            _harness.TrailArrived.Handle(new ScoreTrailArrivedMessage(GameplayMetricsHarness.Red, 40, 40,
                Vector3.zero));
            _harness.ScoreLevelUp.Handle(new ScoreLevelUpMessage(2));

            _harness.LevelUpAborted.Handle(new LevelUpAbortedMessage());

            // RK-9: a stale snapshot would let the NEXT popup render the aborted level's numbers.
            Assert.AreEqual(0, _harness.Service.CeremonyLevel.Value[MetricId.PointsBanked]);
            Assert.AreEqual(0, _harness.Sink.Written.Count);
        }

        [Test]
        public void AbandonedThenAborted_ReturnsToPlayingOnceAndResumesTheGameplayClock()
        {
            _harness.EnterGame();
            Play();
            _harness.ScoreLevelUp.Handle(new ScoreLevelUpMessage(2));

            _harness.Now = 5f;

            // The real order on the abort path: AbandonCeremony publishes Abandoned synchronously from
            // INSIDE the Aborted handler, so metrics receives Abandoned first (R8a). Treating either as
            // terminal wedges the gameplay clock at ~0 for every later level.
            _harness.LevelUpAbandoned.Handle(new LevelUpAbandonedMessage());
            _harness.LevelUpAborted.Handle(new LevelUpAbortedMessage());

            _harness.Now = 15f;
            _harness.CompleteLevel(2);

            var level = _harness.LastOf(RecordKind.Level);
            Assert.AreEqual(10f, level.Metrics[TimerId.Gameplay], 0.01f,
                "The clock must resume — an idempotent abandon is the whole point of R8a.");
            Assert.AreEqual(1, _harness.CountOf(RecordKind.Level), "Neither abort may flush.");
        }

        [Test]
        public void GameOverDuringCeremony_StillWritesBothRecordsDespiteTheNestedAbandon()
        {
            _harness.EnterGame();
            Play();
            _harness.ScoreLevelUp.Handle(new ScoreLevelUpMessage(3));

            // LevelController.AbandonCeremony publishes this from inside its own GameOverMessage handler,
            // so it lands BEFORE the outer GameOver reaches metrics. Had Abandoned entered Ended, R9's
            // gate would swallow the GameOver and no run record would ever exist for a lost run.
            _harness.LevelUpAbandoned.Handle(new LevelUpAbandonedMessage());
            _harness.GameOver.Handle(new GameOverMessage(2, 640));

            Assert.AreEqual(1, _harness.CountOf(RecordKind.Level));
            Assert.AreEqual(1, _harness.CountOf(RecordKind.Run));
            Assert.AreEqual(640, _harness.LastOf(RecordKind.Run).Metrics[MetricId.TotalScore]);
            Assert.IsFalse(_harness.LastOf(RecordKind.Level).Completed);
        }

        [Test]
        public void TransitionCompletedWithoutACeremony_DoesNotFlush()
        {
            _harness.EnterGame();
            Play();

            _harness.TransitionCompleted.Handle(new LevelTransitionCompletedMessage());
            _harness.CompleteLevel(2);
            _harness.TransitionCompleted.Handle(new LevelTransitionCompletedMessage());

            Assert.AreEqual(1, _harness.CountOf(RecordKind.Level));
        }

        [Test]
        public void GameOverImmediatelyAfterATransition_WritesNoPhantomLevelRecord()
        {
            _harness.EnterGame();
            Play();
            _harness.CompleteLevel(2);

            // A loss deferred by the level-up gate fires the instant play resumes, i.e. right here, on a
            // level that has seen nothing.
            _harness.GameOver.Handle(new GameOverMessage(2, 100));

            Assert.AreEqual(1, _harness.CountOf(RecordKind.Level));
            Assert.AreEqual(1, _harness.CountOf(RecordKind.Run));
        }

        [Test]
        public void AfterGameOver_StragglersMutateNothing()
        {
            _harness.EnterGame();
            Play();
            _harness.GameOver.Handle(new GameOverMessage(2, 100));
            var written = _harness.Sink.Written.Count;

            // RK-8: the loss cinematic completes straggler score trails after GameOverMessage.
            _harness.TrailArrived.Handle(new ScoreTrailArrivedMessage(GameplayMetricsHarness.Red, 999, 999,
                Vector3.zero));
            _harness.ActorHit.Handle(GameplayMetricsHarness.Hit(
                GameplayMetricsHarness.Balloon(BalloonType.Simple, GameplayMetricsHarness.Red),
                HitOutcome.Pop));
            _harness.ScoreLevelUp.Handle(new ScoreLevelUpMessage(3));
            _harness.TransitionCompleted.Handle(new LevelTransitionCompletedMessage());

            Assert.AreEqual(written, _harness.Sink.Written.Count);

            _harness.RetryState.RetryLevel.Returns(0);
            _harness.Service.ResetRun(2);
            Play();
            _harness.CompleteLevel(2);

            Assert.AreEqual(0, _harness.LastOf(RecordKind.Level).Metrics[MetricId.PointsBanked],
                "Post-game-over points must not leak into the next run.");
        }

        [Test]
        public void ResetRunMidLevel_ClearsSilently()
        {
            _harness.EnterGame();
            Play();
            _harness.TrailArrived.Handle(new ScoreTrailArrivedMessage(GameplayMetricsHarness.Red, 70, 70,
                Vector3.zero));

            _harness.RetryState.RetryLevel.Returns(0);
            Assert.DoesNotThrow(() => _harness.Service.ResetRun(2));
            Assert.AreEqual(0, _harness.Sink.Written.Count, "A reset is not a flush.");

            Play();
            _harness.CompleteLevel(2);
            Assert.AreEqual(0, _harness.LastOf(RecordKind.Level).Metrics[MetricId.PointsBanked]);
        }

        [Test]
        public void MaxStreak_KeepsTheRunningMaximumNotTheLastValue()
        {
            _harness.EnterGame();
            Play();
            _harness.StreakChanged.Handle(new StreakChangedMessage(GameplayMetricsHarness.Red, 3));
            _harness.StreakChanged.Handle(new StreakChangedMessage(GameplayMetricsHarness.Red, 9));
            _harness.StreakChanged.Handle(new StreakChangedMessage(GameplayMetricsHarness.Red, 0));
            _harness.StreakChanged.Handle(new StreakChangedMessage(GameplayMetricsHarness.Blue, 2));
            _harness.CompleteLevel(2);

            var level = _harness.LastOf(RecordKind.Level);
            Assert.AreEqual(9, level.Metrics[MetricId.MaxStreak]);
            Assert.AreEqual(1, level.Metrics[MetricId.StreakBreaks]);
        }

        [Test]
        public void LevelsCompleted_CountsCleanFlushesOnly()
        {
            _harness.EnterGame();
            Play();
            _harness.CompleteLevel(2);
            Play();
            _harness.CompleteLevel(3);
            Play();
            _harness.GameOver.Handle(new GameOverMessage(3, 800));

            Assert.AreEqual(2, _harness.LastOf(RecordKind.Run).Metrics[MetricId.LevelsCompleted],
                "The partial record at game over is not a completed level.");
        }

        [Test]
        public void AccuracyUsesDirectHitPopsNotTotalPops()
        {
            _harness.EnterGame();
            var balloon = GameplayMetricsHarness.Balloon(BalloonType.Simple, GameplayMetricsHarness.Red);

            _harness.ProjectileFired.Handle(new ProjectileFiredMessage(Vector3.zero, Vector3.up));
            _harness.ActorHit.Handle(GameplayMetricsHarness.Hit(balloon, HitOutcome.Pop));

            // An item/AOE pop carries no DirectHit flag (R13) — counting it would make accuracy > 100%.
            _harness.ActorHit.Handle(GameplayMetricsHarness.Hit(balloon, HitOutcome.Pop, DamageFlags.Normal));
            _harness.CompleteLevel(2);

            var level = _harness.LastOf(RecordKind.Level);
            Assert.AreEqual(1, level.Metrics[MetricId.ShotsFired]);
            Assert.AreEqual(2, level.Metrics[MetricId.Pops]);
            Assert.AreEqual(1, level.Metrics[MetricId.DirectHitPops]);
        }

        [Test]
        public void UnknownColourAndColourlessActors_LandInTheTrailingOtherBucket()
        {
            _harness.EnterGame();

            Assert.DoesNotThrow(() =>
            {
                _harness.ActorHit.Handle(GameplayMetricsHarness.Hit(
                    GameplayMetricsHarness.Balloon(BalloonType.Rainbow, "__rainbow__"), HitOutcome.Pop));
                _harness.ActorHit.Handle(GameplayMetricsHarness.ColorlessHit(HitOutcome.Pop));
                _harness.ActorHit.Handle(GameplayMetricsHarness.Hit(
                    GameplayMetricsHarness.Balloon(BalloonType.Simple, null), HitOutcome.Pop));
            });

            _harness.CompleteLevel(2);

            // Two progress colours plus the trailing "other" bucket.
            var byColor = _harness.Service.LastFlushedLevel.Value.PopsByColor;
            Assert.AreEqual(3, byColor.Count);
            Assert.AreEqual(3, byColor[2].Count);
            Assert.AreEqual(0, byColor[0].Count);
        }

        [Test]
        public void EveryItemTypeIndexesAndAnItemlessBalloonNoOps()
        {
            _harness.EnterGame();

            Assert.DoesNotThrow(() =>
            {
                foreach (ItemType item in Enum.GetValues(typeof(ItemType)))
                {
                    _harness.ItemActivated.Handle(
                        new ItemActivatedMessage(GameplayMetricsHarness.ItemBalloon(item)));
                }

                _harness.ItemActivated.Handle(
                    new ItemActivatedMessage(GameplayMetricsHarness.ItemlessBalloon()));
            });

            _harness.CompleteLevel(2);

            var activations = _harness.Service.LastFlushedLevel.Value.ItemsActivated;
            Assert.AreEqual(Enum.GetValues(typeof(ItemType)).Length, activations.Count);
            foreach (var activation in activations)
            {
                Assert.AreEqual(1, activation.Count, $"{activation.Type} should have exactly one activation.");
            }
        }

        [Test]
        public void FlightSeal_ReadsTheBalloonTypeBreakdownFromFlightStats()
        {
            _harness.EnterGame();
            _harness.FlightStats.PopsOf(BalloonType.Tough).Returns(3);
            _harness.FlightStats.DeflectsOf(BalloonType.Unbreakable).Returns(2);
            _harness.FlightStats.WallBounces.Returns(4);
            _harness.FlightStats.PierceDischarges.Returns(1);

            _harness.ProjectileLoaded.Handle(new ProjectileLoadedMessage(null));
            _harness.FlightLoaded.Value = true;
            _harness.ProjectileDestroyed.Handle(new ProjectileDestroyedMessage());
            _harness.FlightLoaded.Value = false;

            _harness.CompleteLevel(2);

            var level = _harness.LastOf(RecordKind.Level);
            Assert.AreEqual(1, level.Metrics[MetricId.FlightsStarted]);
            Assert.AreEqual(4, level.Metrics[MetricId.WallBounces]);
            Assert.AreEqual(4, level.Metrics[MetricId.MaxWallBouncesInFlight]);
            Assert.AreEqual(1, level.Metrics[MetricId.PierceDischarges]);
            Assert.AreEqual(3, level.Metrics.AxisBucketsOf(MetricId.Pops, MetricAxis.BalloonType)[(int)BalloonType.Tough]);
            Assert.AreEqual(2,
                level.Metrics.AxisBucketsOf(MetricId.Deflects, MetricAxis.BalloonType)[(int)BalloonType.Unbreakable]);
        }

        [Test]
        public void InterflightPops_BelongToTheLevelAndToNoFlight()
        {
            _harness.EnterGame();
            _harness.ProjectileLoaded.Handle(new ProjectileLoadedMessage(null));
            _harness.FlightLoaded.Value = true;
            _harness.ProjectileDestroyed.Handle(new ProjectileDestroyedMessage());
            _harness.FlightLoaded.Value = false;

            // R2: the [Destroyed, next Loaded) window is interflight — a board-clear or cheat pop here
            // belongs to the Level. FlightStatsService zeroes at the next Loaded, so nothing would ever
            // seal it.
            _harness.ActorHit.Handle(GameplayMetricsHarness.Hit(
                GameplayMetricsHarness.Balloon(BalloonType.Simple, GameplayMetricsHarness.Blue),
                HitOutcome.Pop));
            _harness.CompleteLevel(2);

            var level = _harness.LastOf(RecordKind.Level);
            Assert.AreEqual(1, level.Metrics[MetricId.Pops]);
            Assert.AreEqual(1, level.Metrics.AxisBucketsOf(MetricId.Pops, MetricAxis.BalloonType)[(int)BalloonType.Simple]);
        }

        [Test]
        public void HoldSpeedUp_TagsTheFlightAndRunsItsOwnClock()
        {
            _harness.EnterGame();
            _harness.ProjectileLoaded.Handle(new ProjectileLoadedMessage(null));
            _harness.FlightLoaded.Value = true;

            _harness.HoldState.IsHolding.Returns(true);
            _harness.ProjectileFired.Handle(new ProjectileFiredMessage(Vector3.zero, Vector3.up));

            _harness.Now = 3f;
            _harness.HoldState.IsHolding.Returns(false);
            _harness.ProjectileDestroyed.Handle(new ProjectileDestroyedMessage());
            _harness.FlightLoaded.Value = false;

            _harness.CompleteLevel(2);

            var level = _harness.LastOf(RecordKind.Level);
            Assert.AreEqual(1, level.Metrics[MetricId.HoldSpeedUpFlights]);
            Assert.AreEqual(3f, level.Metrics[TimerId.Hold], 0.01f);
        }

        [Test]
        public void PauseGatesTheGameplayClockButNeverTheCeremonyClock()
        {
            _harness.EnterGame();
            Play();

            _harness.Now = 10f;
            _harness.Pause.Pause(PauseSource.Cheat);
            _harness.Now = 30f;
            _harness.Pause.Resume(PauseSource.Cheat);
            _harness.Now = 35f;

            _harness.ScoreLevelUp.Handle(new ScoreLevelUpMessage(2));

            // The ceremony IS a pause (LevelUpCinematic and LevelUpPopUp both hold one for its whole
            // duration), so a pause-gated ceremony clock would read ~0.
            _harness.Pause.Pause(PauseSource.LevelUp);
            _harness.Now = 45f;
            _harness.Pause.Resume(PauseSource.LevelUp);

            _harness.LevelUpDismissed.Handle(new LevelUpDismissedMessage());
            _harness.TransitionCompleted.Handle(new LevelTransitionCompletedMessage());

            var level = _harness.LastOf(RecordKind.Level);
            Assert.AreEqual(15f, level.Metrics[TimerId.Gameplay], 0.01f);
            Assert.AreEqual(10f, level.Metrics[TimerId.Ceremony], 0.01f);
            Assert.AreEqual(45f, level.Metrics[TimerId.Wall], 0.01f);
        }

        [Test]
        public void RunClocksAccumulateAcrossLevelsWhileLevelClocksReset()
        {
            _harness.EnterGame();
            Play();
            _harness.Now = 10f;
            _harness.CompleteLevel(2);
            Play();
            _harness.Now = 25f;
            _harness.CompleteLevel(3);

            // Captured before the game over, which writes a third (partial) level record of its own.
            var secondLevel = _harness.LastOf(RecordKind.Level);

            Play();
            _harness.GameOver.Handle(new GameOverMessage(3, 10));

            Assert.AreEqual(15f, secondLevel.Metrics[TimerId.Gameplay], 0.01f,
                "A level's clock restarts at its own boundary.");
            Assert.AreEqual(25f, _harness.LastOf(RecordKind.Run).Metrics[TimerId.Gameplay], 0.01f,
                "…while the run's own clock has measured the whole run — which is why Absorb never folds timers.");
        }

        [Test]
        public void ASinkThatThrows_NeverEscapesTheFlushBoundary()
        {
            using var harness = new GameplayMetricsHarness(new ThrowingSink());
            harness.EnterGame();
            harness.ProjectileFired.Handle(new ProjectileFiredMessage(Vector3.zero, Vector3.up));

            // MessagePipe runs handlers on the publisher's stack with no exception isolation, and both
            // flush points publish before releasing critical state — an escape here soft-locks the game.
            Assert.DoesNotThrow(() => harness.CompleteLevel(2));
            Assert.DoesNotThrow(() => harness.GameOver.Handle(new GameOverMessage(2, 5)));
        }

        // Asserts the disposal itself, not its consequences. The harness invokes captured handlers
        // directly, so it bypasses whatever CompositeDisposable.Dispose() did to the real subscription —
        // firing a message afterwards would prove nothing either way. What is checkable is that every
        // subscription handed back by Subscribe() actually gets disposed, i.e. none was collected
        // outside the CompositeDisposable.
        [Test]
        public void Dispose_DisposesEverySubscriptionItTookOut()
        {
            _harness.EnterGame();
            Play();

            _harness.Service.Dispose();

            Assert.IsNotEmpty(_harness.Subscriptions, "the harness must have captured the subscriptions");
            foreach (var subscription in _harness.Subscriptions)
            {
                subscription.Received(1).Dispose();
            }
        }

        [Test]
        public void BoardDepleted_MarksTheLevelCleared()
        {
            _harness.EnterGame();
            Play();
            _harness.BoardDepleted.Handle(new BoardDepletedMessage());
            _harness.BoardDepleted.Handle(new BoardDepletedMessage());
            _harness.CompleteLevel(2);

            Assert.AreEqual(1, _harness.LastOf(RecordKind.Level).Metrics[MetricId.BoardCleared]);
        }

        [Test]
        public void WaveDamage_ContributesThreeMetricsNotOne()
        {
            _harness.EnterGame();
            Play();
            _harness.WaveDamage.Handle(new WaveDamageMessage(1, 4, 8));
            _harness.WaveDamage.Handle(new WaveDamageMessage(3, 2, 8));
            _harness.CompleteLevel(2);

            var level = _harness.LastOf(RecordKind.Level);
            Assert.AreEqual(4, level.Metrics[MetricId.HeartsLost]);
            Assert.AreEqual(3, level.Metrics[MetricId.MaxHeartsLostInWave]);
            Assert.AreEqual(6, level.Metrics[MetricId.BlockedSlots]);
        }

        // The routing for these seven handlers is otherwise untested — the harness wires their captures,
        // so a wrong MetricId or a dropped increment compiles, asserts nothing, and stays green. Each
        // assertion below is the metric that handler is the sole writer of.
        [Test]
        public void PierceDischarge_RecordsToughsClearedAndTheRainbowVariant()
        {
            _harness.EnterGame();
            Play();
            _harness.PierceDischarged.Handle(new PierceDischargedMessage(Vector3.zero, 3, false));
            _harness.PierceDischarged.Handle(new PierceDischargedMessage(Vector3.zero, 2, true));
            _harness.CompleteLevel(2);

            var level = _harness.LastOf(RecordKind.Level);
            Assert.AreEqual(5, level.Metrics[MetricId.PierceToughsCleared], "ToughCount sums across discharges");
            Assert.AreEqual(1, level.Metrics[MetricId.RainbowPierceDischarges], "only the rainbow one counts");
        }

        [Test]
        public void SpeedTapMinted_CountsTapsAndKeepsTheHighestPerFlightTotal()
        {
            _harness.EnterGame();
            Play();
            _harness.SpeedTapMinted.Handle(new SpeedTapMintedMessage(Vector3.zero, 1));
            _harness.SpeedTapMinted.Handle(new SpeedTapMintedMessage(Vector3.zero, 4));
            _harness.SpeedTapMinted.Handle(new SpeedTapMintedMessage(Vector3.zero, 2));
            _harness.CompleteLevel(2);

            var level = _harness.LastOf(RecordKind.Level);
            Assert.AreEqual(3, level.Metrics[MetricId.SpeedTapsMinted]);
            Assert.AreEqual(4, level.Metrics[MetricId.MaxSpeedTapsInFlight], "running max, not the last value");
        }

        [Test]
        public void ScorePointsGroup_KeepsTheHighestMultiplier()
        {
            _harness.EnterGame();
            Play();
            _harness.PointsGroup.Handle(new ScorePointsGroupMessage(
                GameplayMetricsHarness.Red, Vector3.zero, 10, 10, 2, Vector3.up));
            _harness.PointsGroup.Handle(new ScorePointsGroupMessage(
                GameplayMetricsHarness.Red, Vector3.zero, 10, 20, 5, Vector3.up));
            _harness.PointsGroup.Handle(new ScorePointsGroupMessage(
                GameplayMetricsHarness.Red, Vector3.zero, 10, 30, 3, Vector3.up));
            _harness.CompleteLevel(2);

            Assert.AreEqual(5, _harness.LastOf(RecordKind.Level).Metrics[MetricId.MaxMultiplier]);
        }

        [Test]
        public void Strikethrough_ShieldGained_AndShieldLost_EachCountTheirOwnMetric()
        {
            _harness.EnterGame();
            Play();
            _harness.Strikethrough.Handle(new StrikethroughArrivedMessage(0));
            _harness.Strikethrough.Handle(new StrikethroughArrivedMessage(1));
            _harness.ShieldGained.Handle(new ShieldGainedMessage(Vector2Int.zero));
            _harness.ShieldLost.Handle(new ShieldLostMessage(Vector3.zero));
            _harness.ShieldLost.Handle(new ShieldLostMessage(Vector3.zero));
            _harness.CompleteLevel(2);

            var level = _harness.LastOf(RecordKind.Level);
            Assert.AreEqual(2, level.Metrics[MetricId.Strikethroughs]);
            Assert.AreEqual(1, level.Metrics[MetricId.ShieldsGained]);
            Assert.AreEqual(2, level.Metrics[MetricId.ShieldsSpent], "spent, not gained");
        }

        // Danger is a 0..1 float and MetricSet is int-only, so the catalog stores hundredths — its unit
        // is "level_hundredths" for exactly this reason. Rounding to a bare 0/1 would make it useless.
        [Test]
        public void DangerLevel_KeepsTheRunningMaximumInHundredths()
        {
            _harness.EnterGame();
            Play();
            _harness.Danger.Value = 0.25f;
            _harness.Danger.Value = 0.8f;
            _harness.Danger.Value = 0.4f;
            _harness.CompleteLevel(2);

            Assert.AreEqual(80, _harness.LastOf(RecordKind.Level).Metrics[MetricId.MaxDangerLevel]);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD || CHEATS_IN_RELEASE
        [Test]
        public void CheatTag_IsStickyForTheWholeRun()
        {
            var previous = BalloonParty.Cheats.CheatState.AnyCheatUsed;
            try
            {
                _harness.EnterGame();
                Play();
                BalloonParty.Cheats.CheatState.AnyCheatUsed = true;
                _harness.CompleteLevel(2);

                BalloonParty.Cheats.CheatState.AnyCheatUsed = false;
                Play();
                _harness.CompleteLevel(3);
                Play();
                _harness.GameOver.Handle(new GameOverMessage(3, 10));

                Assert.IsTrue(_harness.Sink.Written[0].CheatActive, "The tagged level stays tagged.");
                Assert.IsFalse(_harness.Sink.Written[1].CheatActive, "A later clean level is not tagged.");
                Assert.IsTrue(_harness.LastOf(RecordKind.Run).CheatActive, "…but the run stays tagged.");
            }
            finally
            {
                BalloonParty.Cheats.CheatState.AnyCheatUsed = previous;
            }
        }
#endif

        // Enough activity that the level is not treated as untouched at a partial flush.
        // The deferred-loss path publishes GameOverMessage from inside LevelController's OWN
        // LevelTransitionCompletedMessage handler (via RunController.OnNavigationChanged), so it can
        // land before this service has seen the transition. The completed level must still be filed as
        // completed, under its own index — the interleaving below is the runtime one, and the ordinary
        // CompleteLevel() helper deliberately encodes the other.
        [Test]
        public void GameOverArrivingMidTransition_StillFilesTheCompletedLevelUnderItsOwnIndex()
        {
            _harness.EnterGame();
            Play();
            _harness.ScoreLevelUp.Handle(new ScoreLevelUpMessage(2));
            _harness.LevelUpDismissed.Handle(new LevelUpDismissedMessage());

            // Nested, before OnLevelTransitionCompleted reaches this service.
            _harness.GameOver.Handle(new GameOverMessage(2, 700));
            _harness.TransitionCompleted.Handle(new LevelTransitionCompletedMessage());

            var levels = _harness.AllOf(RecordKind.Level);
            Assert.AreEqual(1, levels.Count, "the level the player finished, and no phantom for level 2");
            Assert.AreEqual(1, levels[0].LevelIndex);
            Assert.IsTrue(levels[0].Completed, "it was completed — the loss was deferred from before it");
            Assert.AreEqual(1, _harness.LastOf(RecordKind.Run).Metrics[MetricId.LevelsCompleted]);
        }

        // Hearts reach zero from a wave while the shot is still in the air, so the fatal flight's
        // ProjectileDestroyedMessage lands after Ended and is dropped. Everything it counted has to be
        // folded at the loss instead, or the most interesting flight of the run is discarded whole.
        [Test]
        public void GameOverWithAProjectileStillAirborne_FoldsThatFlightIntoTheLevelRecord()
        {
            _harness.EnterGame();
            _harness.FlightLoaded.Value = true;
            Play();
            _harness.ActorHit.Handle(GameplayMetricsHarness.Hit(
                GameplayMetricsHarness.Balloon(BalloonType.Simple, GameplayMetricsHarness.Red),
                HitOutcome.Pop));
            _harness.FlightStats.WallBounces.Returns(3);

            _harness.GameOver.Handle(new GameOverMessage(1, 250));

            var level = _harness.LastOf(RecordKind.Level);
            Assert.AreEqual(1, level.Metrics[MetricId.Pops], "the pop happened during the fatal flight");
            Assert.AreEqual(3, level.Metrics[MetricId.WallBounces]);
            Assert.AreEqual(3, level.Metrics[MetricId.MaxWallBouncesInFlight]);
        }

        [Test]
        public void GameOverDuringTheCeremony_ClearsTheSnapshotWithoutWaitingForTheAbandon()
        {
            _harness.EnterGame();
            Play();
            _harness.TrailArrived.Handle(new ScoreTrailArrivedMessage(GameplayMetricsHarness.Red, 40, 40,
                Vector3.zero));
            _harness.ScoreLevelUp.Handle(new ScoreLevelUpMessage(2));

            _harness.GameOver.Handle(new GameOverMessage(1, 40));

            // LevelUpAbandonedMessage would also clear it, but only if LevelController's handler happens
            // to run after this one.
            Assert.AreEqual(0, _harness.Service.CeremonyLevel.Value[MetricId.PointsBanked]);
        }

        [Test]
        public void AbortedCeremony_DoesNotLeaveItsProjectedPointsOnTheLevelRecord()
        {
            _harness.EnterGame();
            Play();
            _harness.TotalScore.Value = 900;
            _harness.ScoreLevelUp.Handle(new ScoreLevelUpMessage(2));
            _harness.LevelUpAborted.Handle(new LevelUpAbortedMessage());

            _harness.GameOver.Handle(new GameOverMessage(1, 120));

            Assert.AreEqual(0, _harness.LastOf(RecordKind.Level).Metrics[MetricId.PointsProjected],
                "a level-up that never happened must not project points onto the level it aborted from");
        }

        private void Play()
        {
            _harness.ProjectileFired.Handle(new ProjectileFiredMessage(Vector3.zero, Vector3.up));
        }

        private sealed class ThrowingSink : ITelemetrySink
        {
            public void Write(in TelemetryEnvelope envelope)
            {
                throw new InvalidOperationException("sink failure");
            }

            public UniTask FlushAsync(CancellationToken cancellationToken)
            {
                throw new InvalidOperationException("sink failure");
            }

            public void Dispose()
            {
            }
        }
    }
}
