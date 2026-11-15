using System;
using System.Collections.Generic;
using BalloonParty.Game.Health;
using BalloonParty.Game.Level;
using BalloonParty.Game.Run;
using BalloonParty.Game.Upgrades;
using BalloonParty.Shared;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Messages;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UniRx;

namespace BalloonParty.Tests.Game
{
    /// <summary>
    ///     Resuming an interrupted run across launches: the run is saved by <see cref="RunSaveRecorder" /> in one
    ///     launch and picked up by <see cref="RunResumeController" /> in the next, each launch with its own
    ///     retry tracker, seed and run controller and only the save carried over, as a kill leaves things.
    /// </summary>
    [TestFixture]
    public class RunResumeTests
    {
        private const int FreeRetries = 3;

        private MemoryRunSave _save;

        [SetUp]
        public void SetUp()
        {
            _save = new MemoryRunSave();
        }

        [Test]
        public void ALevelStart_IsSavedWithWhatTheLevelBeganWith()
        {
            var launch = new Launch(_save);
            launch.Play();

            launch.ReachLevel(5, levelStartScore: 1200, hearts: 2);

            Assert.IsTrue(_save.TryLoad(out var saved));
            Assert.AreEqual(5, saved.Level);
            Assert.AreEqual(1200, saved.LevelStartScore);
            Assert.AreEqual(2, saved.Hearts);
            Assert.AreEqual(launch.Seed.Value, saved.Seed);
            Assert.IsFalse(saved.OnGameOver);
        }

        [Test]
        public void Resume_PicksUpTheSavedLevelWithItsSeedAndRetries()
        {
            var first = new Launch(_save);
            first.Play();
            first.ReachLevel(5, levelStartScore: 1200, hearts: 2);
            first.Lose();
            first.Retry();

            var next = new Launch(_save);
            next.Continue();

            Assert.AreEqual(5, next.LevelSetUp, "the reset sets the saved level up, as its retry would");
            Assert.AreEqual(first.Seed.Value, next.Seed.Value, "and where it was");
            Assert.AreEqual(1, next.Tracker.RetriesUsed, "the retry spent before the kill stays spent");
            Assert.AreEqual(NavigationState.Launch, next.Navigation.Value, "the launch screen's ascent shows the game");
        }

        [Test]
        public void Resume_SpendsNoRetry()
        {
            var first = new Launch(_save);
            first.Play();
            first.ReachLevel(3, levelStartScore: 300, hearts: 3);
            var before = first.Offers.Current;

            var next = new Launch(_save);
            next.Continue();
            var after = next.Offers.Current;

            Assert.AreEqual(0, next.Tracker.RetriesUsed);
            Assert.AreEqual(before.FreeRetriesLeft, after.FreeRetriesLeft);
            Assert.IsFalse(after.RetryNeedsAd);
        }

        [Test]
        public void RetriesUsed_SurviveAResume_AndTheNextRetryCountsOnFromThem()
        {
            var first = new Launch(_save);
            first.Play();
            first.ReachLevel(4, levelStartScore: 500, hearts: 2);
            first.Lose();
            first.Retry();
            first.Lose();
            first.Retry();

            var next = new Launch(_save);
            next.Continue();
            Assert.AreEqual(2, next.Tracker.RetriesUsed);

            next.Show();
            next.Lose();
            next.Retry();

            Assert.AreEqual(3, next.Tracker.RetriesUsed);
            Assert.IsTrue(_save.TryLoad(out var saved));
            Assert.AreEqual(3, saved.RetriesUsed, "the retry spent after the resume is saved too");
        }

        [Test]
        public void KilledOnTheGameOverScreen_ComesBackToItWithTheSameOffer()
        {
            var first = new Launch(_save);
            first.Play();
            first.ReachLevel(6, levelStartScore: 900, hearts: 1);
            for (var i = 0; i < FreeRetries; i++)
            {
                first.Lose();
                first.Retry();
            }

            first.Lose(finalScore: 1100);
            var offered = first.Offers.Current;
            Assert.IsTrue(offered.RetryNeedsAd, "the free retries are used: the next one costs an ad");

            var next = new Launch(_save);
            next.Continue();
            next.Show();
            next.Resume.Tick();

            Assert.AreEqual(NavigationState.GameOver, next.Navigation.Value);
            Assert.AreEqual(1, next.GameOvers.Count);
            Assert.IsTrue(next.GameOvers[0].Restored);
            Assert.AreEqual(6, next.GameOvers[0].FinalLevel);
            Assert.AreEqual(1100, next.GameOvers[0].FinalScore);
            next.RunMeta.DidNotReceiveWithAnyArgs().RecordRun(default, default);

            var restored = next.Offers.Current;
            Assert.AreEqual(offered.RetryNeedsAd, restored.RetryNeedsAd, "relaunching never gets around the ad");
            Assert.AreEqual(offered.FreeRetriesLeft, restored.FreeRetriesLeft);
            Assert.AreEqual(offered.CanRetry, restored.CanRetry);
        }

        [Test]
        public void TheGameOverScreen_WaitsForTheGameToShow()
        {
            var first = new Launch(_save);
            first.Play();
            first.ReachLevel(2, levelStartScore: 100, hearts: 3);
            first.Lose(finalScore: 150);

            var next = new Launch(_save);
            next.Continue();
            next.Resume.Tick();

            Assert.IsEmpty(next.GameOvers, "the loss comes back once the launch screen has gone");
            Assert.AreEqual(NavigationState.Launch, next.Navigation.Value);
        }

        [Test]
        public void ARestoredGameOver_StaysTheSave_UntilTheScreenIsLeft()
        {
            var first = new Launch(_save);
            first.Play();
            first.ReachLevel(2, levelStartScore: 100, hearts: 3);
            first.Lose(finalScore: 150);

            var next = new Launch(_save);
            next.Continue();

            Assert.IsTrue(_save.TryLoad(out var saved));
            Assert.IsTrue(saved.OnGameOver, "a resume reset mustn't turn the game-over save into a playable level");
        }

        [Test]
        public void PlayInsteadOfContinue_DropsTheSave()
        {
            var first = new Launch(_save);
            first.Play();
            first.ReachLevel(4, levelStartScore: 500, hearts: 2);

            var next = new Launch(_save);
            next.Play();

            Assert.IsFalse(_save.TryLoad(out _));
            Assert.AreEqual(0, next.Tracker.RetriesUsed);
        }

        [Test]
        public void AFreshRestart_ClearsTheSave_AndARetryKeepsOne()
        {
            var launch = new Launch(_save);
            launch.Play();
            launch.ReachLevel(4, levelStartScore: 500, hearts: 2);
            launch.Lose();

            launch.Retry();
            Assert.IsTrue(_save.TryLoad(out _), "a retry saves the level it starts");

            launch.Lose();
            launch.Controller.RestartRun();
            Assert.IsFalse(_save.TryLoad(out _), "starting over throws the run away");
        }

        [Test]
        public void ALevelUpAfterTheLoss_DoesNotOverwriteTheGameOverSave()
        {
            var launch = new Launch(_save);
            launch.Play();
            launch.ReachLevel(4, levelStartScore: 500, hearts: 2);

            // A loss deferred past a level-up fires inside the transition's own handlers.
            launch.Lose(finalScore: 700);
            launch.ReachLevel(5, levelStartScore: 700, hearts: 1);

            Assert.IsTrue(_save.TryLoad(out var saved));
            Assert.IsTrue(saved.OnGameOver);
            Assert.AreEqual(4, saved.Level);
        }

        [Test]
        public void Continue_WithTheSaveGone_StartsANewRun()
        {
            var launch = new Launch(_save);
            launch.Continue();

            Assert.AreEqual(0, launch.Resets, "nothing to resume, so the run already set up stands");
        }

        private sealed class MemoryRunSave : IRunSave
        {
            private string _json;

            public bool TryLoad(out RunSnapshot snapshot)
            {
                return RunSnapshot.TryParse(_json, out snapshot);
            }

            public void Write(RunSnapshot snapshot)
            {
                _json = snapshot.ToJson();
            }

            public void Clear()
            {
                _json = null;
            }
        }

        /// <summary>One app launch: its own run services, the save shared with the launches before and after.</summary>
        private sealed class Launch
        {
            internal readonly ReactiveProperty<NavigationState> Navigation = new(NavigationState.Launch);
            internal readonly List<GameOverMessage> GameOvers = new();
            internal readonly IRunMeta RunMeta = Substitute.For<IRunMeta>();
            internal readonly RetryTracker Tracker;
            internal readonly RunSeed Seed;
            internal readonly FailureOffers Offers;
            internal readonly RunController Controller;
            internal readonly RunResumeController Resume;

            private readonly ReactiveProperty<int> _level = new(1);
            private readonly ReactiveProperty<int> _hearts = new(3);
            private readonly ReactiveProperty<int> _total = new(0);
            private readonly IRunResumeRequest _request = new RunResumeRequest();
            private readonly IRunScore _score = Substitute.For<IRunScore>();

            private IMessageHandler<RunResetMessage> _onReset;
            private IMessageHandler<LevelTransitionCompletedMessage> _onTransition;
            private IMessageHandler<GameOverMessage> _onGameOver;
            private int _levelStartScore;

            internal int LevelSetUp { get; private set; }
            internal int Resets { get; private set; }

            internal Launch(IRunSave save)
            {
                var config = Substitute.For<IRunConfig>();
                config.MaxRetries.Returns(0);
                config.RetriesBeforeAd.Returns(FreeRetries);
                Tracker = new RetryTracker(config);
                Seed = new RunSeed(Tracker, new Random(Environment.TickCount));
                var bonusCards = Substitute.For<IBonusCardSource>();
                Offers = new FailureOffers(Tracker, Tracker, config, Substitute.For<IAdService>(), bonusCards);

                _score.LevelStartScore.Returns(_ => _levelStartScore);
                _score.TotalScore.Returns(_total);
                var levelProgress = Substitute.For<ILevelProgress>();
                levelProgress.Level.Returns(_level);
                var health = Substitute.For<IPlayerHealth>();
                health.Current.Returns(_hearts);

                var navigation = Substitute.For<INavigation>();
                navigation.Current.Returns(Navigation);
                navigation.When(n => n.TransitionTo(Arg.Any<NavigationState>()))
                    .Do(call => Navigation.Value = call.Arg<NavigationState>());

                var recorder = new RunSaveRecorder(save, Tracker, levelProgress, _score, health, Seed,
                    Substitute.For<IHeldCards>(), Subscriber<RunResetMessage>(h => _onReset = h),
                    Subscriber<LevelTransitionCompletedMessage>(h => _onTransition = h),
                    Subscriber<GameOverMessage>(h => _onGameOver = h));
                recorder.Start();

                var gameOverPublisher = Substitute.For<IPublisher<GameOverMessage>>();
                gameOverPublisher.When(p => p.Publish(Arg.Any<GameOverMessage>())).Do(call => OnGameOver(call.Arg<GameOverMessage>()));
                var resetPublisher = Substitute.For<IPublisher<RunResetMessage>>();
                resetPublisher.When(p => p.Publish(Arg.Any<RunResetMessage>()))
                    .Do(call => _onReset.Handle(call.Arg<RunResetMessage>()));

                // Stands in for LevelController (order 100), which reads the staged level during the cascade.
                var levelReader = new ResetReader(RunResetOrder.Score, () =>
                {
                    Resets++;
                    LevelSetUp = Tracker.RetryLevel > 0 ? Tracker.RetryLevel : 1;
                    _level.Value = LevelSetUp;
                });

                var cinematic = Substitute.For<ICinematicState>();
                Controller = new RunController(navigation, cinematic, RunMeta, _score, levelProgress,
                    gameOverPublisher, resetPublisher, Substitute.For<ISubscriber<EndRunRequestedMessage>>(),
                    new IRunResettable[] { Tracker, Seed, recorder, levelReader });
                Controller.Start();

                Resume = new RunResumeController(_request, Tracker, save, Controller, navigation);
                Resume.Start();
            }

            internal void Play()
            {
                Navigation.Value = NavigationState.Game;
            }

            internal void Continue()
            {
                _request.Request();
            }

            // The launch screen's ascent ends.
            internal void Show()
            {
                Navigation.Value = NavigationState.Game;
            }

            internal void ReachLevel(int level, int levelStartScore, int hearts)
            {
                _level.Value = level;
                _levelStartScore = levelStartScore;
                _hearts.Value = hearts;
                _onTransition.Handle(default);
            }

            // Scores a little on the level, unless told the final score, then loses.
            internal void Lose(int finalScore = -1)
            {
                _total.Value = finalScore >= 0 ? finalScore : _levelStartScore + 50;
                _hearts.Value = 0;
                Controller.EndRun();
            }

            // What the game-over screen's Retry does once the offer is settled, then the restart.
            internal void Retry()
            {
                Tracker.PrepareRetry(_level.Value, withBonusCard: false);
                _hearts.Value = 3;
                Controller.RestartRun();
            }

            private void OnGameOver(GameOverMessage message)
            {
                GameOvers.Add(message);
                _onGameOver.Handle(message);
            }

            private static ISubscriber<T> Subscriber<T>(Action<IMessageHandler<T>> capture)
            {
                var subscriber = Substitute.For<ISubscriber<T>>();
                subscriber
                    .Subscribe(Arg.Do(capture), Arg.Any<MessageHandlerFilter<T>[]>())
                    .Returns(Substitute.For<IDisposable>());
                return subscriber;
            }
        }

        private sealed class ResetReader : IRunResettable
        {
            private readonly Action _onReset;

            internal ResetReader(int order, Action onReset)
            {
                ResetOrder = order;
                _onReset = onReset;
            }

            public int ResetOrder { get; }

            public void ResetRun(int generation)
            {
                _onReset();
            }
        }
    }
}
