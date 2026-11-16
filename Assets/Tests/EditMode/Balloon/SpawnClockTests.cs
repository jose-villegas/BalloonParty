using System;
using BalloonParty.Balloon.Spawner;
using BalloonParty.Configuration.Level;
using BalloonParty.Game.Assist;
using BalloonParty.Game.Danger;
using BalloonParty.Game.Level;
using BalloonParty.Game.Score.Behaviours;
using BalloonParty.Projectile;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pause;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UniRx;
using UnityEngine;

namespace BalloonParty.Tests.Balloon
{
    [TestFixture]
    public class SpawnClockTests
    {
        private ReactiveProperty<int> _level;
        private ReactiveProperty<LevelUpPhase> _phase;
        private PauseService _pauseService;
        private ProjectileRegistry _registry;
        private IPublisher<SpawnBalloonLineMessage> _publisher;
        private IMessageHandler<ProjectileFiredMessage> _firedHandler;
        private IBoardFill _boardFill;
        private ILevelProgress _progress;
        private ISubscriber<ProjectileFiredMessage> _firedSubscriber;
        private IScoreShapeCover _shapeCover;
        private IProjectileFlightConfig _flightConfig;
        private ReactiveProperty<float> _danger;
        private IDangerLevel _dangerLevel;
        private IPlayerNeed _need;
        private ISubscriber<WaveDamageMessage> _damageSubscriber;
        private IMessageHandler<WaveDamageMessage> _damageHandler;
        private SpawnClock _clock;
        private NavigationState _previousNavigation;
        private int _lines;

        [SetUp]
        public void SetUp()
        {
            _previousNavigation = Navigation.Current.Value;
            Navigation.TransitionTo(NavigationState.Game);

            _level = new ReactiveProperty<int>(1);
            _phase = new ReactiveProperty<LevelUpPhase>(LevelUpPhase.Playing);

            _progress = Substitute.For<ILevelProgress>();
            _progress.Level.Returns(_level);
            _progress.Phase.Returns(_phase);

            // Grace 4s, then 3s at level 1 and 1.5s at level 2 (halving, floor 1s); an overlap shot pulls 25%.
            // An empty board runs the clock 3x; a full one (the default here) at normal pace. Fill reads as an
            // even blend of the starting fill and occupancy against a 50% target. Flight never matters here;
            // the flight tests build their own clock.

            _boardFill = Substitute.For<IBoardFill>();
            _boardFill.StartingFill.Returns(1f);
            _boardFill.Occupancy.Returns(1f);

            _pauseService = new PauseService(
                Substitute.For<IPublisher<PausedMessage>>(), Substitute.For<IPublisher<ResumedMessage>>());
            _registry = new ProjectileRegistry();

            _lines = 0;
            _publisher = Substitute.For<IPublisher<SpawnBalloonLineMessage>>();
            _publisher.When(p => p.Publish(Arg.Any<SpawnBalloonLineMessage>())).Do(_ => _lines++);

            _firedSubscriber = Substitute.For<ISubscriber<ProjectileFiredMessage>>();
            _firedSubscriber
                .Subscribe(
                    Arg.Do<IMessageHandler<ProjectileFiredMessage>>(h => _firedHandler = h),
                    Arg.Any<MessageHandlerFilter<ProjectileFiredMessage>[]>())
                .Returns(Substitute.For<IDisposable>());

            // A 4 x 8 play area (32 units squared), with no score shapes in the air.
            _shapeCover = Substitute.For<IScoreShapeCover>();
            _danger = new ReactiveProperty<float>(0f);
            _dangerLevel = Substitute.For<IDangerLevel>();
            _dangerLevel.Level.Returns(_danger);
            _need = Substitute.For<IPlayerNeed>();
            _damageSubscriber = Substitute.For<ISubscriber<WaveDamageMessage>>();
            _damageSubscriber
                .Subscribe(
                    Arg.Do<IMessageHandler<WaveDamageMessage>>(h => _damageHandler = h),
                    Arg.Any<MessageHandlerFilter<WaveDamageMessage>[]>())
                .Returns(Substitute.For<IDisposable>());
            _flightConfig = Substitute.For<IProjectileFlightConfig>();
            _flightConfig.LimitsClockwise.Returns(new Vector4(4f, 2f, -4f, -2f));

            _clock = null;
            BuildClock(new SpawnClockSettings(3f, 0.5f, 1f, 4f, overlapShotPull: 0.25f,
                sparseBoardSpeedup: 3f, targetOccupancy: 0.5f, occupancyWeight: 0.5f));
        }

        [TearDown]
        public void TearDown()
        {
            _clock.Dispose();
            _registry.Dispose();
            Navigation.TransitionTo(_previousNavigation);
        }

        [Test]
        public void Advance_DuringGrace_PublishesNoLine()
        {
            _clock.Advance(3.5f);

            Assert.AreEqual(0, _lines);
        }

        [Test]
        public void Advance_ReachingGraceEnd_PublishesOneLine()
        {
            _clock.Advance(3.5f);
            _clock.Advance(0.5f);

            Assert.AreEqual(1, _lines);
        }

        [Test]
        public void Advance_AfterFirstLine_NextLineArrivesOneIntervalLater()
        {
            _clock.Advance(4f);

            _clock.Advance(2.5f);
            Assert.AreEqual(1, _lines);

            _clock.Advance(0.5f);
            Assert.AreEqual(2, _lines);
        }

        [Test]
        public void Advance_OnAnExtraColorLevel_LinesComeAtItsShareOfTheInterval()
        {
            // Level 1's 3s interval at a 0.5 share: the next line 1.5s after the first.
            var current = Substitute.For<ILevelParameters>();
            current.SpawnIntervalScale.Returns(0.5f);
            var levelParams = Substitute.For<IActiveLevelParameters>();
            levelParams.Current.Returns(current);
            BuildClock(new SpawnClockSettings(3f, 0.5f, 1f, 4f), levelParams);

            _clock.Advance(4f);
            _clock.Advance(1.4f);
            Assert.AreEqual(1, _lines);

            _clock.Advance(0.2f);
            Assert.AreEqual(2, _lines);
        }

        [Test]
        public void Advance_WithoutALevelResolved_KeepsTheUsualInterval()
        {
            var levelParams = Substitute.For<IActiveLevelParameters>();
            levelParams.Current.Returns((ILevelParameters)null);
            BuildClock(new SpawnClockSettings(3f, 0.5f, 1f, 4f), levelParams);

            _clock.Advance(4f);
            _clock.Advance(2.9f);
            Assert.AreEqual(1, _lines);

            _clock.Advance(0.2f);
            Assert.AreEqual(2, _lines);
        }

        [Test]
        public void Advance_HugeDelta_PublishesAtMostOneLine()
        {
            _clock.Advance(1000f);

            Assert.AreEqual(1, _lines);
        }

        [Test]
        public void Advance_HugeDelta_ThenRestartsAFullInterval()
        {
            _clock.Advance(1000f);

            _clock.Advance(2.5f);

            Assert.AreEqual(1, _lines, "the overshoot is dropped, not banked");
        }

        [Test]
        public void Advance_AfterLevelUp_UsesTheNewLevelIntervalAfterGrace()
        {
            _clock.Advance(4f);
            _level.Value = 2;
            _clock.Advance(4f);
            Assert.AreEqual(2, _lines);

            _clock.Advance(1.5f);

            Assert.AreEqual(3, _lines, "level 2 runs at 1.5s");
        }

        [Test]
        public void Advance_WhilePaused_FreezesTheClock()
        {
            _pauseService.Pause(PauseSource.Cinematic);
            _clock.Advance(100f);
            _pauseService.Resume(PauseSource.Cinematic);

            _clock.Advance(3.5f);
            Assert.AreEqual(0, _lines, "paused time must not count");

            _clock.Advance(0.5f);
            Assert.AreEqual(1, _lines);
        }

        [Test]
        public void Advance_PhaseNotPlaying_FreezesTheClock()
        {
            _phase.Value = LevelUpPhase.Pending;

            _clock.Advance(100f);

            Assert.AreEqual(0, _lines);
        }

        [Test]
        public void Advance_PhaseBackToPlaying_Resumes()
        {
            _phase.Value = LevelUpPhase.Transitioning;
            _clock.Advance(100f);
            _phase.Value = LevelUpPhase.Playing;

            _clock.Advance(4f);

            Assert.AreEqual(1, _lines);
        }

        [Test]
        public void Advance_NavigationNotGame_FreezesTheClock()
        {
            Navigation.TransitionTo(NavigationState.GameOver);

            _clock.Advance(100f);

            Assert.AreEqual(0, _lines);
        }

        [Test]
        public void Advance_LoneDoomedBallInFlight_FreezesTheClock()
        {
            Fly().IsLastShieldApproach.Value = true;

            _clock.Advance(100f);

            Assert.AreEqual(0, _lines);
        }

        [Test]
        public void Advance_DoomedBallHasCompany_KeepsRunning()
        {
            Fly().IsLastShieldApproach.Value = true;
            Fly();

            _clock.Advance(4f);

            Assert.AreEqual(1, _lines);
        }

        [Test]
        public void Advance_FlyingBallNotDoomed_KeepsRunning()
        {
            Fly();

            _clock.Advance(4f);

            Assert.AreEqual(1, _lines);
        }

        [Test]
        public void ResetRun_MidInterval_RestartsTheGrace()
        {
            _clock.Advance(4f);
            _clock.Advance(2f);

            _clock.ResetRun(1);

            _clock.Advance(3.5f);
            Assert.AreEqual(1, _lines, "no new line before the full grace elapses");
            _clock.Advance(0.5f);
            Assert.AreEqual(2, _lines);
        }

        [Test]
        public void LevelChange_MidWindow_RestartsTheGrace()
        {
            _clock.Advance(3f);

            _level.Value = 2;

            _clock.Advance(3.5f);
            Assert.AreEqual(0, _lines);
            _clock.Advance(0.5f);
            Assert.AreEqual(1, _lines);
        }

        [Test]
        public void SecondsOnLevel_CountsPlayingTime_NotTheBoardPace()
        {
            _boardFill.StartingFill.Returns(0f);
            _boardFill.Occupancy.Returns(0f);

            _clock.Advance(2f);
            _clock.Advance(1.5f);

            Assert.AreEqual(3.5f, _clock.SecondsOnLevel, 1e-4f);
        }

        [Test]
        public void SecondsOnLevel_HoldsWhilePausedOrNotPlaying()
        {
            _clock.Advance(1f);
            _pauseService.Pause(PauseSource.Cinematic);
            _clock.Advance(10f);
            _pauseService.Resume(PauseSource.Cinematic);
            _phase.Value = LevelUpPhase.Pending;
            _clock.Advance(10f);

            Assert.AreEqual(1f, _clock.SecondsOnLevel, 1e-4f);
        }

        [Test]
        public void SecondsOnLevel_StartsOverOnLevelUpAndRunReset()
        {
            _clock.Advance(5f);
            _level.Value = 2;
            Assert.AreEqual(0f, _clock.SecondsOnLevel);

            _clock.Advance(2f);
            _clock.ResetRun(1);
            Assert.AreEqual(0f, _clock.SecondsOnLevel);
        }

        [Test]
        public void NextLineProgress_AcrossAWindow_RunsZeroToOne()
        {
            Assert.AreEqual(0f, _clock.NextLineProgress, 0.0001f);

            _clock.Advance(1f);
            Assert.AreEqual(0.25f, _clock.NextLineProgress, 0.0001f);

            _clock.Advance(2f);
            Assert.AreEqual(0.75f, _clock.NextLineProgress, 0.0001f);
        }

        [Test]
        public void NextLineProgress_AfterALine_RestartsAtZeroForTheInterval()
        {
            _clock.Advance(4f);

            Assert.AreEqual(0f, _clock.NextLineProgress, 0.0001f);

            _clock.Advance(1.5f);
            Assert.AreEqual(0.5f, _clock.NextLineProgress, 0.0001f);
        }

        [Test]
        public void Fire_WithNoOtherBallFlying_LeavesTheClockAlone()
        {
            UseEmptyBoardPull();
            Fire(Fly());

            _clock.Advance(3.5f);

            Assert.AreEqual(0, _lines, "a lone ball costs nothing");
        }

        [Test]
        public void Fire_WhileAnotherBallFlies_PullsTheNextLineCloser()
        {
            UseEmptyBoardPull();
            Fly();
            Fire(Fly());

            _clock.Advance(2.9f);
            Assert.AreEqual(0, _lines);

            _clock.Advance(0.1f);
            Assert.AreEqual(1, _lines, "the 4s grace lost a quarter, so the line lands at 3s");
        }

        [Test]
        public void Fire_EachOverlapShot_PullsAgain()
        {
            UseEmptyBoardPull();
            Fly();
            Fire(Fly());
            Fire(Fly());

            _clock.Advance(2f);

            Assert.AreEqual(1, _lines, "two pulls of 1s each off the 4s grace");
        }

        [Test]
        public void Fire_PullingPastZero_LandsOneLineOnTheNextTick()
        {
            UseEmptyBoardPull();
            Fly();
            for (var i = 0; i < 6; i++)
            {
                Fire(Fly());
            }

            Assert.AreEqual(0, _lines, "the pull only shortens the wait");

            _clock.Advance(0.01f);
            Assert.AreEqual(1, _lines, "however far past zero, one line per tick");
        }

        [Test]
        public void Fire_PullsAShareOfTheWindow_NotOfWhatIsLeft()
        {
            UseEmptyBoardPull();
            _clock.Advance(4f);
            _clock.Advance(2f);

            Fly();
            Fire(Fly());

            _clock.Advance(0.25f);
            Assert.AreEqual(2, _lines, "1s left of the 3s interval, minus a 0.75s pull");
        }

        [Test]
        public void Fire_OnABoardAtItsFill_PullsNothing()
        {
            Fly();
            Fire(Fly());

            _clock.Advance(3.9f);

            Assert.AreEqual(0, _lines, "a full board needs balls in the air, and they mustn't crowd it more");
        }

        [Test]
        public void Fire_OnAHalfFullBoard_PullsHalf()
        {
            UseEmptyBoardPull();
            _boardFill.StartingFill.Returns(0.5f);
            _boardFill.Occupancy.Returns(0.25f);

            Fly();
            Fire(Fly());
            _clock.Advance(3.4f);
            Assert.AreEqual(0, _lines);

            _clock.Advance(0.15f);
            Assert.AreEqual(1, _lines, "half the 1s pull off the 4s grace");
        }

        [Test]
        public void Advance_OnAnEmptyBoard_RunsAtTheFullSpeedup()
        {
            _boardFill.StartingFill.Returns(0f);
            _boardFill.Occupancy.Returns(0f);

            _clock.Advance(1.3f);
            Assert.AreEqual(0, _lines);

            _clock.Advance(0.05f);
            Assert.AreEqual(1, _lines, "the 4s grace at 3x is over in 1.33s");
        }

        [Test]
        public void Advance_OnAHalfFullBoard_RunsBetweenNormalAndFullSpeedup()
        {
            _boardFill.StartingFill.Returns(0.5f);
            _boardFill.Occupancy.Returns(0.25f);

            _clock.Advance(1.9f);
            Assert.AreEqual(0, _lines);

            _clock.Advance(0.15f);
            Assert.AreEqual(1, _lines, "half full runs at 2x, so the 4s grace takes 2s");
        }

        [Test]
        public void Advance_AtTheStartingFillButBelowTargetOccupancy_StillRunsFast()
        {
            _boardFill.Occupancy.Returns(0f);

            _clock.Advance(1.9f);
            Assert.AreEqual(0, _lines);

            _clock.Advance(0.15f);
            Assert.AreEqual(1, _lines, "occupancy weighs half, so a sparse board at its starting fill runs at 2x");
        }

        [Test]
        public void Advance_OccupancyAboveTarget_CountsAsFull()
        {
            _boardFill.StartingFill.Returns(0f);
            _boardFill.Occupancy.Returns(0.8f);

            _clock.Advance(1.9f);
            Assert.AreEqual(0, _lines);

            _clock.Advance(0.15f);
            Assert.AreEqual(1, _lines, "past the target, occupancy reads as 1, so the blend is 0.5 and the pace 2x");
        }

        [Test]
        public void Advance_OnAFullBoard_RunsAtNormalPace()
        {
            _clock.Advance(3.9f);

            Assert.AreEqual(0, _lines);
        }

        [Test]
        public void Flight_ABallInTheAir_SpeedsTheClockUpAsItFlies()
        {
            UseFlightPressure();
            Fly();

            _clock.Advance(1f);
            _clock.Advance(1f);
            Assert.AreEqual(0, _lines, "1.5x then 2x: 3.5s of the 4s grace gone");

            _clock.Advance(1f);
            Assert.AreEqual(1, _lines, "at 3s of flight the pace is 2.5x");
        }

        [Test]
        public void Flight_Pressure_CapsAtTheFlightSpeedup()
        {
            UseFlightPressure();
            for (var i = 0; i < 8; i++)
            {
                Fly();
            }

            _clock.Advance(1f);
            Assert.AreEqual(0, _lines, "8 flight seconds is past the 4s of full pressure, so the pace caps at 3x");

            _clock.Advance(1f);
            Assert.AreEqual(1, _lines);
        }

        [Test]
        public void Flight_OnABoardAtItsFill_AddsNothing()
        {
            UseFlightPressure();
            _boardFill.StartingFill.Returns(1f);
            _boardFill.Occupancy.Returns(1f);
            for (var i = 0; i < 8; i++)
            {
                Fly();
            }

            _clock.Advance(3.9f);

            Assert.AreEqual(0, _lines, "full flight pressure on a full board: still the plain 4s grace");
        }

        [Test]
        public void Flight_OnAHalfFullBoard_AddsHalf()
        {
            UseFlightPressure();
            _boardFill.StartingFill.Returns(0.5f);
            _boardFill.Occupancy.Returns(0.25f);
            for (var i = 0; i < 8; i++)
            {
                Fly();
            }

            _clock.Advance(1.95f);
            Assert.AreEqual(0, _lines, "1x plus half of the 2x extra: 2x, so the 4s grace takes 2s");

            _clock.Advance(0.1f);
            Assert.AreEqual(1, _lines);
        }

        [Test]
        public void Flight_AddsOnTopOfTheBoardPace()
        {
            BuildClock(new SpawnClockSettings(3f, 0.5f, 1f, 4f, sparseBoardSpeedup: 3f, targetOccupancy: 0.5f,
                occupancyWeight: 0.5f, flightSpeedup: 3f, flightPressureSeconds: 4f));
            _boardFill.StartingFill.Returns(0f);
            _boardFill.Occupancy.Returns(0f);
            for (var i = 0; i < 8; i++)
            {
                Fly();
            }

            _clock.Advance(0.75f);
            Assert.AreEqual(0, _lines, "3x from the empty board plus 2x from full flight pressure: 3.75s");

            _clock.Advance(0.1f);
            Assert.AreEqual(1, _lines);
        }

        [Test]
        public void Flight_ARetiredBallFliesAgain_StartsItsFlightTimeOver()
        {
            UseFlightPressure();
            var ball = Fly();
            _clock.Advance(1f);

            _registry.Retire(ball);
            _registry.Load(ball, null);
            _registry.Launch(ball);
            _clock.Advance(1f);
            _clock.Advance(0.5f);

            Assert.AreEqual(0, _lines, "1.5s + 1.5s + 0.875s, short of the 4s grace; a carried age would land it");
        }

        [Test]
        public void Flight_StoppedClock_DoesNotAgeTheBalls()
        {
            UseFlightPressure();
            Fly();
            _phase.Value = LevelUpPhase.Pending;
            _clock.Advance(10f);
            _phase.Value = LevelUpPhase.Playing;

            _clock.Advance(1.5f);

            Assert.AreEqual(0, _lines, "1.5 counted seconds run the clock at 1.75x, not a capped 3x");
        }

        [Test]
        public void Breather_ABigFastClear_SlowsTheClockRightDown()
        {
            UseBreather();
            Run(1f);

            _boardFill.Occupancy.Returns(0.4f);
            Run(3f);

            Assert.AreEqual(0, _lines, "without the breather the 4s grace is over by now");
        }

        [Test]
        public void Breather_EasesBackToFullPace_ByItsEnd()
        {
            UseBreather();
            Run(1f);
            _boardFill.Occupancy.Returns(0.4f);

            Run(4.85f);
            Assert.AreEqual(0, _lines, "the 4s breather averages half pace: 2s of clock, then 0.85s at full");

            Run(0.25f);
            Assert.AreEqual(1, _lines, "the clear that started the breather can't start another once it ends");
        }

        [Test]
        public void BreatherSecondsLeft_CountsDownThroughTheBreather_AndIsZeroOutsideIt()
        {
            UseBreather();
            Run(1f);
            Assert.AreEqual(0f, _clock.BreatherSecondsLeft);

            _boardFill.Occupancy.Returns(0.4f);
            Run(1f);
            Assert.AreEqual(3.05f, _clock.BreatherSecondsLeft, 0.01f, "it started at 4s on the clear's first tick");

            Run(3.5f);
            Assert.AreEqual(0f, _clock.BreatherSecondsLeft);
        }

        [Test]
        public void Breather_Cooldown_HoldsOffTheNextOne_ThenLetsABigClearEarnIt()
        {
            UseBreatherWithCooldown();
            Run(1f);
            _boardFill.Occupancy.Returns(0.4f);
            Run(4.2f);
            Assert.AreEqual(0f, _clock.BreatherSecondsLeft, "the 4s breather is over");

            _boardFill.Occupancy.Returns(1f);
            Run(0.5f);
            _boardFill.Occupancy.Returns(0.4f);
            Run(0.5f);
            Assert.AreEqual(0f, _clock.BreatherSecondsLeft, "a clear 1s into the 5s cooldown earns nothing");

            Run(5f);
            _boardFill.Occupancy.Returns(1f);
            Run(0.5f);
            _boardFill.Occupancy.Returns(0.4f);
            Run(0.1f);
            Assert.Greater(_clock.BreatherSecondsLeft, 0f, "past the cooldown the same clear earns one");
        }

        [Test]
        public void Breather_Cooldown_EndsWithTheLevel()
        {
            UseBreatherWithCooldown();
            Run(1f);
            _boardFill.Occupancy.Returns(0.4f);
            Run(4.2f);

            _level.Value = 2;
            _boardFill.Occupancy.Returns(1f);
            Run(0.5f);
            _boardFill.Occupancy.Returns(0.4f);
            Run(0.1f);

            Assert.Greater(_clock.BreatherSecondsLeft, 0f, "a new level starts with no cooldown");
        }

        [Test]
        public void Grant_InsideACooldown_StartsABreatherAnyway()
        {
            UseBreatherWithCooldown();
            Run(1f);
            _boardFill.Occupancy.Returns(0.4f);
            Run(4.2f);
            Assert.AreEqual(0f, _clock.BreatherSecondsLeft, "the 4s breather is over, the 5s cooldown running");

            _clock.Grant();

            Assert.AreEqual(4f, _clock.BreatherSecondsLeft, "a full breather, though a clear now earns none");
        }

        [Test]
        public void Grant_OnAnOrdinaryTick_StartsAFullBreather_ThatSlowsTheClock()
        {
            UseBreather();
            Run(1f);

            _clock.Grant();
            Run(3f);

            Assert.AreEqual(0, _lines, "without the breather the 4s grace is over by now");
            Assert.Greater(_clock.BreatherSecondsLeft, 0f);
        }

        [Test]
        public void Grant_WhileOneRuns_StartsItOver()
        {
            UseBreather();
            Run(1f);
            _clock.Grant();
            Run(2f);
            Assert.Less(_clock.BreatherSecondsLeft, 2.5f);

            _clock.Grant();

            Assert.AreEqual(4f, _clock.BreatherSecondsLeft);
        }

        [Test]
        public void Breather_ASlowClearOfTheSameSize_EarnsNone()
        {
            UseBreather();
            var occupancy = 1f;
            for (var t = 0f; t < 4.1f; t += 0.05f)
            {
                occupancy -= 0.0075f;
                _boardFill.Occupancy.Returns(occupancy);
                _clock.Advance(0.05f);
            }

            Assert.AreEqual(1, _lines, "0.6 of the board over 4s is far slower than 0.5 in 1s");
        }

        [Test]
        public void Breather_ZeroClear_NeverStartsOne()
        {
            BuildClock(new SpawnClockSettings(3f, 0.5f, 1f, 4f, breatherClear: 0f, breatherClearWindow: 1f,
                breatherSeconds: 4f, breatherPace: 0f));
            Run(1f);

            _boardFill.Occupancy.Returns(0f);
            Run(3.1f);

            Assert.AreEqual(1, _lines);
        }

        [Test]
        public void Breather_ScoreShapesCoveringHalfThePlayArea_StartOne()
        {
            UseShapeBreather();
            Run(1f);

            _shapeCover.CoveredArea.Returns(16f);
            Run(3f);

            Assert.Greater(_clock.BreatherSecondsLeft, 0f);
            Assert.AreEqual(0, _lines, "without the breather the 4s grace is over by now");
        }

        [Test]
        public void Pressure_BuildsWhileTheBoardStaysUnderTheHold()
        {
            UsePressure();

            Run(5f);

            Assert.AreEqual(0.5f, _clock.Pressure, 0.01f, "half of the 10s build");
        }

        [Test]
        public void Pressure_HoldsOnceTheBoardReachesTheHold()
        {
            UsePressure();
            Run(5f);

            _boardFill.Occupancy.Returns(0.85f);
            Run(5f);

            Assert.AreEqual(0.5f, _clock.Pressure, 0.01f);
        }

        [Test]
        public void Pressure_NeedSlowsTheBuild_AndTheLastHeartStopsIt()
        {
            UsePressure();
            _need.Need.Returns(0.5f);
            Run(5f);
            Assert.AreEqual(0.25f, _clock.Pressure, 0.01f, "half need, half the build");

            _need.Need.Returns(1f);
            Run(5f);
            Assert.AreEqual(0.25f, _clock.Pressure, 0.01f, "the last heart builds none");
        }

        [Test]
        public void Pressure_AtFull_RunsTheClockAtItsSpeedup()
        {
            UsePressure();
            Run(10f);
            Assert.AreEqual(1f, _clock.Pressure, 1e-4f);

            // The build spent 10 + 5 of the 100s grace; the 85 left go at 2x.
            Run(42.3f);
            Assert.AreEqual(0, _lines);

            Run(0.4f);
            Assert.AreEqual(1, _lines);
        }

        [Test]
        public void Pressure_ALineThatCostsAHeart_HalvesIt()
        {
            UsePressure();
            Run(10f);

            _damageHandler.Handle(new WaveDamageMessage(0, 3, 6));
            Assert.AreEqual(1f, _clock.Pressure, 1e-4f, "slots blocked short of a line cost no heart");

            _damageHandler.Handle(new WaveDamageMessage(1, 6, 6));
            Assert.AreEqual(0.5f, _clock.Pressure, 1e-4f);
        }

        [Test]
        public void Pressure_DoesNotBuildThroughABreather()
        {
            BuildClock(new SpawnClockSettings(3f, 0.5f, 1f, 100f, breatherSeconds: 4f, breatherDanger: 1f,
                pressureSpeedup: 2f, pressureBuildSeconds: 10f));
            _boardFill.Occupancy.Returns(0.5f);
            _danger.Value = 1f;

            Run(2f);

            Assert.Greater(_clock.BreatherSecondsLeft, 0f);
            Assert.AreEqual(0f, _clock.Pressure);
        }

        [Test]
        public void Pressure_LastsThroughALevel_AndGoesWithTheRun()
        {
            UsePressure();
            Run(5f);

            _level.Value = 2;
            Assert.AreEqual(0.5f, _clock.Pressure, 0.01f, "a strong build is still strong next level");

            _clock.ResetRun(2);
            Assert.AreEqual(0f, _clock.Pressure);
        }

        [Test]
        public void Pressure_Off_NeverMovesTheClock()
        {
            BuildClock(new SpawnClockSettings(3f, 0.5f, 1f, 4f));
            _boardFill.Occupancy.Returns(0.5f);

            Run(3.9f);
            Assert.AreEqual(0, _lines, "a speedup of 1 leaves the 4s grace as it is");

            Run(0.2f);
            Assert.AreEqual(1, _lines);
        }

        [Test]
        public void Breather_DangerAtItsLevel_StartsOne()
        {
            UseDangerBreather();
            Run(1f);

            _danger.Value = 0.5f;
            Run(3f);

            Assert.Greater(_clock.BreatherSecondsLeft, 0f);
            Assert.AreEqual(0, _lines, "without the breather the 4s grace is over by now");
        }

        [Test]
        public void Breather_DangerShortOfItsLevel_StartsNone()
        {
            UseDangerBreather();
            Run(1f);

            _danger.Value = 0.49f;
            Run(3.1f);

            Assert.AreEqual(0f, _clock.BreatherSecondsLeft);
            Assert.AreEqual(1, _lines);
        }

        [Test]
        public void Breather_ZeroDangerLevel_NeverStartsOne()
        {
            BuildClock(new SpawnClockSettings(3f, 0.5f, 1f, 4f, breatherSeconds: 4f, breatherPace: 0f));
            Run(1f);

            _danger.Value = 1f;
            Run(3.1f);

            Assert.AreEqual(0f, _clock.BreatherSecondsLeft);
            Assert.AreEqual(1, _lines);
        }

        [Test]
        public void Breather_Danger_WaitsOutTheCooldown()
        {
            BuildClock(new SpawnClockSettings(3f, 0.5f, 1f, 4f, breatherSeconds: 4f, breatherPace: 0f,
                breatherCooldownSeconds: 5f, breatherDanger: 0.5f));
            _danger.Value = 1f;
            Run(0.1f);
            Assert.Greater(_clock.BreatherSecondsLeft, 0f);

            Run(4f);
            Assert.AreEqual(0f, _clock.BreatherSecondsLeft, "the breather ran out");

            Run(4.8f);
            Assert.AreEqual(0f, _clock.BreatherSecondsLeft, "a board left in danger waits out the cooldown");

            Run(0.3f);
            Assert.Greater(_clock.BreatherSecondsLeft, 0f, "and earns the next once it is over");
        }

        [Test]
        public void Breather_ScoreShapesShortOfTheShare_StartNone()
        {
            UseShapeBreather();
            Run(1f);

            _shapeCover.CoveredArea.Returns(15f);
            Run(3.1f);

            Assert.AreEqual(0f, _clock.BreatherSecondsLeft);
            Assert.AreEqual(1, _lines);
        }

        [Test]
        public void Breather_ScoreShapes_WaitOutTheCooldownLikeAClear()
        {
            BuildClock(new SpawnClockSettings(3f, 0.5f, 1f, 4f, breatherClear: 0f, breatherClearWindow: 1f,
                breatherSeconds: 4f, breatherPace: 0f, breatherCooldownSeconds: 5f, breatherShapeCover: 0.5f));
            _shapeCover.CoveredArea.Returns(32f);
            Run(4.1f);
            Assert.AreEqual(0f, _clock.BreatherSecondsLeft, "the 4s breather is over");

            Run(4.8f);
            Assert.AreEqual(0f, _clock.BreatherSecondsLeft, "shapes still covering the board wait out the 5s cooldown");

            Run(0.3f);
            Assert.Greater(_clock.BreatherSecondsLeft, 0f, "past the cooldown they earn the next one");
        }

        [Test]
        public void Breather_ZeroShapeCover_NeverStartsOneFromShapes()
        {
            BuildClock(new SpawnClockSettings(3f, 0.5f, 1f, 4f, breatherClear: 0f, breatherSeconds: 4f,
                breatherPace: 0f, breatherShapeCover: 0f));
            _shapeCover.CoveredArea.Returns(32f);
            Run(1f);

            Assert.AreEqual(0f, _clock.BreatherSecondsLeft);
        }

        [Test]
        public void Breather_ScalesTheOverlapPullToo()
        {
            UseBreather(0.5f);
            _boardFill.StartingFill.Returns(0f);
            Run(1f);
            _boardFill.Occupancy.Returns(0.4f);
            Run(0.05f);

            Fly();
            Fire(Fly());
            Fire(Fly());
            Run(2.9f);

            Assert.AreEqual(0, _lines, "two full pulls would take 4s off the wait and land the line at once");
        }

        [Test]
        public void Breather_EndsWithTheLevel()
        {
            UseBreather();
            Run(1f);
            _boardFill.Occupancy.Returns(0.4f);
            Run(0.05f);

            _level.Value = 2;
            Run(4.1f);

            Assert.AreEqual(1, _lines, "the new level's 4s grace runs at full pace");
        }

        private void BuildClock(SpawnClockSettings settings, IActiveLevelParameters levelParams = null)
        {
            _clock?.Dispose();
            var pacing = Substitute.For<ILevelPacingConfiguration>();
            pacing.SpawnClock.Returns(settings);
            _clock = new SpawnClock(pacing, _progress, _pauseService, _registry, _boardFill, _publisher,
                _firedSubscriber, levelParams ?? UsualLevel(), _shapeCover, _flightConfig, _dangerLevel, _need,
                _damageSubscriber);
            _clock.Start();
        }

        // Full flight pressure (3x on an empty board, with no sparse-board speedup) at 4 summed flight seconds; no
        // overlap pull.
        private void UseFlightPressure()
        {
            BuildClock(new SpawnClockSettings(3f, 0.5f, 1f, 4f, targetOccupancy: 0.5f, occupancyWeight: 0.5f,
                flightSpeedup: 3f, flightPressureSeconds: 4f));
            _boardFill.StartingFill.Returns(0f);
            _boardFill.Occupancy.Returns(0f);
        }

        // An empty board, so a pull counts in full, with no sparse-board speedup: only the 25% pull moves the clock.
        private void UseEmptyBoardPull()
        {
            BuildClock(new SpawnClockSettings(3f, 0.5f, 1f, 4f, overlapShotPull: 0.25f, targetOccupancy: 0.5f,
                occupancyWeight: 0.5f));
            _boardFill.StartingFill.Returns(0f);
            _boardFill.Occupancy.Returns(0f);
        }

        // Board fill never moves the pace here; losing half the board within 1s starts a 4s breather that
        // opens at a standstill.
        private void UseBreather(float overlapShotPull = 0f)
        {
            BuildClock(new SpawnClockSettings(3f, 0.5f, 1f, 4f, overlapShotPull: overlapShotPull,
                breatherClear: 0.5f, breatherClearWindow: 1f, breatherSeconds: 4f, breatherPace: 0f));
        }

        // Only score shapes start a breather here: covering half the play area starts a 4s one at a standstill.
        private void UseShapeBreather()
        {
            BuildClock(new SpawnClockSettings(3f, 0.5f, 1f, 4f, breatherClear: 0f, breatherSeconds: 4f,
                breatherPace: 0f, breatherShapeCover: 0.5f));
        }

        // As UseBreather, with a 5s cooldown after each breather.
        // A 100s grace at plain pace on a board half full (under the 0.85 hold): pressure builds to full in 10s,
        // doubling the pace there.
        private void UsePressure()
        {
            BuildClock(new SpawnClockSettings(3f, 0.5f, 1f, 100f, pressureSpeedup: 2f, pressureBuildSeconds: 10f,
                pressureHoldOccupancy: 0.85f, pressureKeptOnHeartLoss: 0.5f));
            _boardFill.Occupancy.Returns(0.5f);
        }

        // Only danger starts a breather here: 0.5 or more starts a 4s one at a standstill.
        private void UseDangerBreather()
        {
            BuildClock(new SpawnClockSettings(3f, 0.5f, 1f, 4f, breatherSeconds: 4f, breatherPace: 0f,
                breatherDanger: 0.5f));
        }

        private void UseBreatherWithCooldown()
        {
            BuildClock(new SpawnClockSettings(3f, 0.5f, 1f, 4f, breatherClear: 0.5f, breatherClearWindow: 1f,
                breatherSeconds: 4f, breatherPace: 0f, breatherCooldownSeconds: 5f));
        }

        // Small steps, as frames would: the breather eases through its length.
        // The level's interval share is 1, as on every level but an extra-colour one.
        private static IActiveLevelParameters UsualLevel()
        {
            var current = Substitute.For<ILevelParameters>();
            current.SpawnIntervalScale.Returns(1f);
            var levelParams = Substitute.For<IActiveLevelParameters>();
            levelParams.Current.Returns(current);
            return levelParams;
        }

        private void Run(float seconds)
        {
            for (var t = 0f; t < seconds - 0.001f; t += 0.05f)
            {
                _clock.Advance(0.05f);
            }
        }

        private void Fire(ProjectileModel ball)
        {
            _firedHandler.Handle(new ProjectileFiredMessage(ball, Vector3.zero, Vector3.up));
        }

        private ProjectileModel Fly()
        {
            var ball = new ProjectileModel();
            _registry.Load(ball, null);
            _registry.Launch(ball);
            return ball;
        }
    }
}
