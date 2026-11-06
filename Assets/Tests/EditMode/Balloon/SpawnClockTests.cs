using BalloonParty.Balloon.Spawner;
using BalloonParty.Configuration.Level;
using BalloonParty.Game.Level;
using BalloonParty.Projectile;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pause;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UniRx;

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

            var progress = Substitute.For<ILevelProgress>();
            progress.Level.Returns(_level);
            progress.Phase.Returns(_phase);

            // Grace 4s, then 3s at level 1 and 1.5s at level 2 (halving, floor 1s).
            var pacing = Substitute.For<ILevelPacingConfiguration>();
            pacing.SpawnClock.Returns(new SpawnClockSettings(3f, 0.5f, 1f, 4f));

            _pauseService = new PauseService(
                Substitute.For<IPublisher<PausedMessage>>(), Substitute.For<IPublisher<ResumedMessage>>());
            _registry = new ProjectileRegistry();

            _lines = 0;
            _publisher = Substitute.For<IPublisher<SpawnBalloonLineMessage>>();
            _publisher.When(p => p.Publish(Arg.Any<SpawnBalloonLineMessage>())).Do(_ => _lines++);

            _clock = new SpawnClock(pacing, progress, _pauseService, _registry, _publisher);
            _clock.Start();
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

        private ProjectileModel Fly()
        {
            var ball = new ProjectileModel();
            _registry.Load(ball, null);
            _registry.Launch(ball);
            return ball;
        }
    }
}
