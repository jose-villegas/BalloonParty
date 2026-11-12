using System;
using System.Collections.Generic;
using BalloonParty.Configuration.CardReveal;
using BalloonParty.Game.CardReveal;
using BalloonParty.Shared;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Messages;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UniRx;

namespace BalloonParty.Tests.Game
{
    [TestFixture]
    public class CardRevealControllerTests
    {
        private ReactiveProperty<NavigationState> _navigation;
        private ICardRevealSettings _settings;
        private IMessageHandler<UpgradeCardHeldMessage> _heldHandler;
        private IMessageHandler<BonusCardDealtMessage> _bonusHandler;
        private CardRevealController _controller;
        private IUpgradeCardSettings _shield;
        private IUpgradeCardSettings _reload;

        [SetUp]
        public void SetUp()
        {
            _navigation = new ReactiveProperty<NavigationState>(NavigationState.Game);
            _settings = Substitute.For<ICardRevealSettings>();
            _settings.PopCount.Returns(16);
            _settings.RecentPopsAvoided.Returns(2);
            _shield = Substitute.For<IUpgradeCardSettings>();
            _reload = Substitute.For<IUpgradeCardSettings>();
            Build(new Random(1));
        }

        [TearDown]
        public void TearDown()
        {
            _controller.Dispose();
        }

        [Test]
        public void WithNoStage_QueuesNothing_SoTheTooltipKeepsTheCards()
        {
            Held(_shield);
            _controller.AttachStage();

            Assert.IsNull(_controller.Current.Value, "a card won before the stage attached is the tooltip's");
        }

        [Test]
        public void HeldCard_StartsARevealOfIt()
        {
            _controller.AttachStage();

            Held(_shield, stacks: 2);

            var reveal = _controller.Current.Value;
            Assert.AreSame(_shield, reveal.Card);
            Assert.AreEqual(2, reveal.Stacks);
            Assert.IsFalse(reveal.IsBonus);
        }

        [Test]
        public void HeldCard_DuringTheLevelUp_StartsAtOnce_SoItPlaysWithTheAscent()
        {
            _controller.AttachStage();
            _navigation.Value = NavigationState.LevelUp;

            Held(_shield);

            Assert.AreSame(_shield, _controller.Current.Value.Card);
        }

        [Test]
        public void ASecondCard_WaitsItsTurn()
        {
            _controller.AttachStage();
            Held(_shield);
            Bonus(_reload);

            Assert.AreSame(_shield, _controller.Current.Value.Card);

            _controller.Finish(_controller.Current.Value);
            Assert.AreSame(_reload, _controller.Current.Value.Card);
            Assert.IsTrue(_controller.Current.Value.IsBonus);

            _controller.Finish(_controller.Current.Value);
            Assert.IsNull(_controller.Current.Value);
        }

        [Test]
        public void BonusDealtBehindTheGameOverScreen_WaitsForPlayToResume()
        {
            _controller.AttachStage();
            _navigation.Value = NavigationState.GameOver;

            Bonus(_reload, stacks: 1);
            Assert.IsNull(_controller.Current.Value);

            _navigation.Value = NavigationState.Game;
            Assert.AreSame(_reload, _controller.Current.Value.Card);
        }

        [Test]
        public void ResetRun_DropsTheOldRunsReveals()
        {
            _controller.AttachStage();
            Held(_shield);
            Held(_reload);

            _controller.ResetRun(1);
            Assert.IsNull(_controller.Current.Value);

            _navigation.Value = NavigationState.GameOver;
            _navigation.Value = NavigationState.Game;
            Assert.IsNull(_controller.Current.Value, "the queued second card went with the reset");
        }

        [Test]
        public void ResetRun_KeepsTheRetrysBonus_DealtAfterIt()
        {
            _controller.AttachStage();
            Held(_shield);

            _controller.ResetRun(1);
            Bonus(_reload);

            Assert.AreSame(_reload, _controller.Current.Value.Card);
        }

        [Test]
        public void Finish_OfAReveal_TheResetAlreadyDropped_LeavesTheNewOneShowing()
        {
            _controller.AttachStage();
            Held(_shield);
            var old = _controller.Current.Value;

            _controller.ResetRun(1);
            Bonus(_reload);
            _controller.Finish(old);

            Assert.AreSame(_reload, _controller.Current.Value.Card, "the stage finishing the old reveal late");
        }

        [Test]
        public void AttachingAStage_OffTheGameScreen_WaitsForPlay()
        {
            _navigation.Value = NavigationState.GameOver;
            _controller.AttachStage();
            Bonus(_reload);
            _controller.DetachStage();

            _controller.AttachStage();
            Assert.IsNull(_controller.Current.Value, "the detach dropped the queue");

            Bonus(_shield);
            _controller.AttachStage();
            Assert.IsNull(_controller.Current.Value, "still behind the game-over screen");

            _navigation.Value = NavigationState.Game;
            Assert.AreSame(_shield, _controller.Current.Value.Card);
        }

        [Test]
        public void ANullCard_QueuesNothing()
        {
            _controller.AttachStage();

            Held(null);

            Assert.IsNull(_controller.Current.Value);
        }

        [Test]
        public void DetachingTheLastStage_DropsTheQueue()
        {
            _controller.AttachStage();
            _controller.AttachStage();
            Held(_shield);
            Held(_reload);

            _controller.DetachStage();
            Assert.AreSame(_shield, _controller.Current.Value.Card, "one stage is still attached");

            _controller.DetachStage();
            Assert.IsFalse(_controller.HasStage);
            Assert.IsNull(_controller.Current.Value);
        }

        [Test]
        public void Pop_NeverOneOfTheLastTwo_AndEveryPopComesUp()
        {
            _controller.AttachStage();
            var shown = new List<int>();
            for (var i = 0; i < 400; i++)
            {
                Held(_shield);
                shown.Add(_controller.Current.Value.Pop);
                _controller.Finish(_controller.Current.Value);
            }

            for (var i = 2; i < shown.Count; i++)
            {
                Assert.AreNotEqual(shown[i - 1], shown[i], $"reveal {i} repeated the last pop");
                Assert.AreNotEqual(shown[i - 2], shown[i], $"reveal {i} repeated the pop before last");
            }

            for (var pop = 0; pop < 16; pop++)
            {
                Assert.Contains(pop, shown);
            }
        }

        [Test]
        public void NextPop_BeforeAStageAttaches_IsNone()
        {
            Assert.AreEqual(-1, _controller.NextPop);
        }

        [Test]
        public void NextPop_IsThePopTheNextRevealPlays()
        {
            _controller.AttachStage();
            for (var i = 0; i < 20; i++)
            {
                var next = _controller.NextPop;
                Assert.That(next, Is.InRange(0, 15));

                Held(_shield);

                Assert.AreEqual(next, _controller.Current.Value.Pop, $"reveal {i}");
                _controller.Finish(_controller.Current.Value);
            }
        }

        [Test]
        public void NextPop_IsChosenAgainBeforeARevealPublishes()
        {
            _controller.AttachStage();
            var nextAtPublish = new List<int>();
            using var watching = _controller.Current.Where(reveal => reveal != null)
                .Subscribe(reveal => nextAtPublish.Add(_controller.NextPop));

            var chosenBefore = _controller.NextPop;
            Held(_shield);

            // Different only since the setup avoids recent pops, the one playing among them.
            Assert.AreEqual(1, nextAtPublish.Count);
            Assert.AreNotEqual(chosenBefore, nextAtPublish[0], "a stage reading it as the reveal starts loads the " +
                                                             "one after, not the one playing");
        }

        [Test]
        public void NextPop_AStageAttachingAgain_KeepsTheOneChosen()
        {
            _controller.AttachStage();
            var next = _controller.NextPop;

            _controller.DetachStage();
            _controller.AttachStage();

            Assert.AreEqual(next, _controller.NextPop, "its sheet may already be loading");
        }

        [Test]
        public void Pop_WithFewerPopsThanAvoided_StillPicksOne()
        {
            _settings.PopCount.Returns(2);
            _controller.AttachStage();

            var shown = new List<int>();
            for (var i = 0; i < 6; i++)
            {
                Held(_shield);
                shown.Add(_controller.Current.Value.Pop);
                _controller.Finish(_controller.Current.Value);
            }

            for (var i = 1; i < shown.Count; i++)
            {
                Assert.AreNotEqual(shown[i - 1], shown[i], "two pops alternate");
            }
        }

        [Test]
        public void Pop_WithOnePop_AlwaysPicksIt()
        {
            _settings.PopCount.Returns(1);
            _controller.AttachStage();

            for (var i = 0; i < 3; i++)
            {
                Held(_shield);
                Assert.AreEqual(0, _controller.Current.Value.Pop);
                _controller.Finish(_controller.Current.Value);
            }
        }

        private void Build(Random random)
        {
            var navigation = Substitute.For<INavigation>();
            navigation.Current.Returns(_navigation);

            var held = Substitute.For<ISubscriber<UpgradeCardHeldMessage>>();
            held.Subscribe(Arg.Do<IMessageHandler<UpgradeCardHeldMessage>>(h => _heldHandler = h),
                    Arg.Any<MessageHandlerFilter<UpgradeCardHeldMessage>[]>())
                .Returns(Substitute.For<IDisposable>());

            var bonus = Substitute.For<ISubscriber<BonusCardDealtMessage>>();
            bonus.Subscribe(Arg.Do<IMessageHandler<BonusCardDealtMessage>>(h => _bonusHandler = h),
                    Arg.Any<MessageHandlerFilter<BonusCardDealtMessage>[]>())
                .Returns(Substitute.For<IDisposable>());

            _controller = new CardRevealController(_settings, navigation, held, bonus, random);
            _controller.Start();
        }

        private void Held(IUpgradeCardSettings card, int stacks = 1)
        {
            _heldHandler.Handle(new UpgradeCardHeldMessage(card, stacks));
        }

        private void Bonus(IUpgradeCardSettings card, int stacks = 1)
        {
            _bonusHandler.Handle(new BonusCardDealtMessage(card, stacks));
        }
    }
}
