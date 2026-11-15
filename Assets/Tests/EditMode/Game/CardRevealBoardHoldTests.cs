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
        private ReactiveProperty<NavigationState> _navigation;
        private ReactiveProperty<RevealedCard> _current;
        private PauseService _pauseService;
        private CardRevealBoardHold _hold;

        [SetUp]
        public void SetUp()
        {
            _navigation = new ReactiveProperty<NavigationState>(NavigationState.Game);
            _current = new ReactiveProperty<RevealedCard>();
            var reveals = Substitute.For<ICardReveals>();
            reveals.Current.Returns(_current);
            var navigation = Substitute.For<INavigation>();
            navigation.Current.Returns(_navigation);
            _pauseService = new PauseService(
                Substitute.For<IPublisher<PausedMessage>>(), Substitute.For<IPublisher<ResumedMessage>>());
            _hold = new CardRevealBoardHold(reveals, navigation, _pauseService);
            _hold.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _hold.Dispose();
        }

        [Test]
        public void ARevealInPlay_HoldsTheBoard_UntilItEnds()
        {
            _current.Value = Reveal();

            Assert.IsTrue(_pauseService.IsPaused(PauseSource.CardReveal));

            _current.Value = null;

            Assert.IsFalse(_pauseService.IsAnyPaused.Value);
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
        public void ALevelUpsReveal_DuringItsAscent_HoldsNothing_ButRunningOnIntoPlay_Holds()
        {
            _navigation.Value = NavigationState.LevelUp;
            _current.Value = Reveal();

            Assert.IsFalse(_pauseService.IsPaused(PauseSource.CardReveal));

            _navigation.Value = NavigationState.Game;

            Assert.IsTrue(_pauseService.IsPaused(PauseSource.CardReveal));
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
