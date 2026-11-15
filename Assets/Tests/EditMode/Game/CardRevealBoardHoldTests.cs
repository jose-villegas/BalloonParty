using BalloonParty.Configuration.CardReveal;
using BalloonParty.Game.CardReveal;
using BalloonParty.Shared;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Pause;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UniRx;

namespace BalloonParty.Tests.Game
{
    [TestFixture]
    public class CardRevealBoardHoldTests
    {
        private const float ReturnSeconds = 1.5f;

        private ReactiveProperty<NavigationState> _navigation;
        private ReactiveProperty<RevealedCard> _current;
        private ReactiveProperty<bool> _shown;
        private PauseService _pauseService;
        private ITimeScaleClaims _timeScale;
        private CardRevealBoardHold _hold;

        [SetUp]
        public void SetUp()
        {
            _navigation = new ReactiveProperty<NavigationState>(NavigationState.Game);
            _current = new ReactiveProperty<RevealedCard>();
            _shown = new ReactiveProperty<bool>();
            var reveals = Substitute.For<ICardReveals>();
            reveals.Current.Returns(_current);
            reveals.Shown.Returns(_shown);
            var navigation = Substitute.For<INavigation>();
            navigation.Current.Returns(_navigation);
            _pauseService = new PauseService(
                Substitute.For<IPublisher<PausedMessage>>(), Substitute.For<IPublisher<ResumedMessage>>());
            _timeScale = Substitute.For<ITimeScaleClaims>();
            var settings = Substitute.For<ICardRevealSettings>();
            settings.BoardReturnSeconds.Returns(ReturnSeconds);
            _hold = new CardRevealBoardHold(reveals, navigation, _pauseService, _timeScale, settings);
            _hold.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _hold.Dispose();
        }

        [Test]
        public void ARevealInPlay_HoldsTheBoard_UntilItsCardIsOut()
        {
            _current.Value = Reveal();

            Assert.IsTrue(_pauseService.IsPaused(PauseSource.CardReveal));

            _shown.Value = true;

            Assert.IsFalse(_pauseService.IsAnyPaused.Value, "the player gets the board back as the card comes out");
        }

        [Test]
        public void TheCardOut_EasesTimeUpFromAStandstill_ToFullSpeed()
        {
            _current.Value = Reveal();
            _timeScale.ClearReceivedCalls();

            _shown.Value = true;
            _timeScale.Received(1).Claim(TimeScaleSource.CardReveal, 0f);

            _hold.Advance(ReturnSeconds / 2f);
            _timeScale.Received(1).Claim(TimeScaleSource.CardReveal, 0.5f);

            _hold.Advance(ReturnSeconds / 2f);
            _timeScale.Received().Release(TimeScaleSource.CardReveal);

            _timeScale.ClearReceivedCalls();
            _hold.Advance(1f);
            _timeScale.DidNotReceiveWithAnyArgs().Claim(default, default);
        }

        [Test]
        public void TheReturn_KeepsEasing_AfterTheRevealEnds()
        {
            _current.Value = Reveal();
            _shown.Value = true;
            _current.Value = null;
            _timeScale.ClearReceivedCalls();

            _hold.Advance(ReturnSeconds / 2f);

            _timeScale.Received(1).Claim(TimeScaleSource.CardReveal, 0.5f);
        }

        [Test]
        public void ARevealEndingBeforeItsCardIsOut_FreesTheBoard_AtFullSpeed()
        {
            _current.Value = Reveal();

            _current.Value = null;

            Assert.IsFalse(_pauseService.IsAnyPaused.Value);
            _timeScale.DidNotReceive().Claim(TimeScaleSource.CardReveal, Arg.Any<float>());
        }

        [Test]
        public void ABonusCardWaitingBehindGameOver_HoldsFromWhenPlayResumes()
        {
            _navigation.Value = NavigationState.GameOver;
            _current.Value = Reveal();

            Assert.IsFalse(_pauseService.IsAnyPaused.Value, "the game-over screen is already in front of the board");

            _navigation.Value = NavigationState.Game;

            Assert.IsTrue(_pauseService.IsPaused(PauseSource.CardReveal));
        }

        [Test]
        public void ALevelUpsReveal_DuringItsAscent_HoldsNothing_ButRunningOnIntoPlay_HoldsUntilItsCardIsOut()
        {
            _navigation.Value = NavigationState.LevelUp;
            _current.Value = Reveal();

            Assert.IsFalse(_pauseService.IsPaused(PauseSource.CardReveal));

            _navigation.Value = NavigationState.Game;

            Assert.IsTrue(_pauseService.IsPaused(PauseSource.CardReveal));
        }

        [Test]
        public void ALevelUpsReveal_WithItsCardAlreadyOut_RunsOnIntoPlay_HoldingNothing()
        {
            _navigation.Value = NavigationState.LevelUp;
            _current.Value = Reveal();
            _shown.Value = true;

            _navigation.Value = NavigationState.Game;

            Assert.IsFalse(_pauseService.IsAnyPaused.Value);
            _timeScale.DidNotReceive().Claim(TimeScaleSource.CardReveal, Arg.Any<float>());
        }

        [Test]
        public void LeavingPlay_MidReturn_StopsIt()
        {
            _current.Value = Reveal();
            _shown.Value = true;

            _navigation.Value = NavigationState.GameOver;
            _timeScale.Received().Release(TimeScaleSource.CardReveal);
            _timeScale.ClearReceivedCalls();

            _hold.Advance(ReturnSeconds / 2f);
            _timeScale.DidNotReceiveWithAnyArgs().Claim(default, default);
        }

        [Test]
        public void ANextReveal_MidReturn_HoldsTheBoardAgain()
        {
            _current.Value = Reveal();
            _shown.Value = true;

            _shown.Value = false;
            _current.Value = Reveal();

            Assert.IsTrue(_pauseService.IsPaused(PauseSource.CardReveal));
            _timeScale.Received().Release(TimeScaleSource.CardReveal);
        }

        [Test]
        public void ARunReset_MidReturn_StopsIt()
        {
            _current.Value = Reveal();
            _shown.Value = true;
            _timeScale.ClearReceivedCalls();

            _hold.ResetRun(1);
            _hold.Advance(ReturnSeconds / 2f);

            _timeScale.DidNotReceiveWithAnyArgs().Claim(default, default);
        }

        [Test]
        public void ARevealFollowedByAnother_HoldsOnce()
        {
            _current.Value = Reveal();
            _current.Value = Reveal();
            _current.Value = null;

            Assert.IsFalse(_pauseService.IsAnyPaused.Value, "one release undoes the one hold");
        }

        [Test]
        public void ARunResetDroppingTheReveal_LeavesTheBoardFree_AndANextRevealHoldsAgain()
        {
            _current.Value = Reveal();
            _pauseService.ResetRun(1);
            _current.Value = null;

            Assert.IsFalse(_pauseService.IsAnyPaused.Value);

            _current.Value = Reveal();

            Assert.IsTrue(_pauseService.IsPaused(PauseSource.CardReveal));
        }

        private static RevealedCard Reveal()
        {
            return new RevealedCard(Substitute.For<IUpgradeCardSettings>(), 1, isBonus: true, pop: 0);
        }
    }
}
