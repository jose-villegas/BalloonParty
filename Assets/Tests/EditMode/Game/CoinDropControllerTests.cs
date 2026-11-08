using System;
using System.Collections.Generic;
using System.Linq;
using BalloonParty.Configuration.Level;
using BalloonParty.Game.Coins;
using BalloonParty.Game.Level;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Capabilities;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UniRx;
using UnityEngine;

namespace BalloonParty.Tests.Game
{
    [TestFixture]
    public class CoinDropControllerTests
    {
        private const int CoinsFromLevel = 15;

        private ICoinSettings _settings;
        private ReactiveProperty<int> _level;
        private IPublisher<CoinDroppedMessage> _droppedPublisher;
        private IMessageHandler<ActorHitMessage> _hitHandler;
        private CoinDropController _controller;

        [SetUp]
        public void SetUp()
        {
            _settings = Substitute.For<ICoinSettings>();
            _settings.CoinsFromLevel.Returns(CoinsFromLevel);
            _settings.DropChance.Returns(1f);

            var config = Substitute.For<IRunConfig>();
            config.Coins.Returns(_settings);

            _level = new ReactiveProperty<int>(CoinsFromLevel);
            var levelProgress = Substitute.For<ILevelProgress>();
            levelProgress.Level.Returns(_level);

            var pacing = Substitute.For<ILevelPacingConfiguration>();
            pacing.SeedForLevel(Arg.Any<int>()).Returns(call => (int)call[0] * 7919);

            _droppedPublisher = Substitute.For<IPublisher<CoinDroppedMessage>>();

            var hitSubscriber = Substitute.For<ISubscriber<ActorHitMessage>>();
            hitSubscriber
                .Subscribe(
                    Arg.Do<IMessageHandler<ActorHitMessage>>(h => _hitHandler = h),
                    Arg.Any<MessageHandlerFilter<ActorHitMessage>[]>())
                .Returns(Substitute.For<IDisposable>());

            _controller = new CoinDropController(
                config,
                levelProgress,
                pacing,
                hitSubscriber,
                _droppedPublisher);
            _controller.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _controller.Dispose();
            _level.Dispose();
        }

        [Test]
        public void Pop_AtTheFirstCoinLevel_DropsOneCoinWhereItPopped()
        {
            var position = new Vector3(1f, 2f, 0f);

            Hit(HitOutcome.Pop, position);

            _droppedPublisher.Received(1).Publish(Arg.Is<CoinDroppedMessage>(m =>
                m.Amount == 1 && m.WorldPosition == position));
        }

        [Test]
        public void Pop_BeforeTheFirstCoinLevel_DropsNothing()
        {
            _level.Value = CoinsFromLevel - 1;

            Hit(HitOutcome.Pop);

            _droppedPublisher.DidNotReceive().Publish(Arg.Any<CoinDroppedMessage>());
        }

        [TestCase(HitOutcome.None)]
        [TestCase(HitOutcome.Deflect)]
        [TestCase(HitOutcome.PassThrough)]
        [TestCase(HitOutcome.Absorb)]
        public void HitThatDoesNotPop_DropsNothing(HitOutcome outcome)
        {
            Hit(outcome);

            _droppedPublisher.DidNotReceive().Publish(Arg.Any<CoinDroppedMessage>());
        }

        [Test]
        public void PopCombinedWithAnotherOutcome_StillDrops()
        {
            Hit(HitOutcome.Pop | HitOutcome.PassThrough);

            _droppedPublisher.Received(1).Publish(Arg.Any<CoinDroppedMessage>());
        }

        [Test]
        public void Pop_AfterTheFirstCoinLevel_StillDrops()
        {
            _level.Value = CoinsFromLevel + 10;

            Hit(HitOutcome.Pop);

            _droppedPublisher.Received(1).Publish(Arg.Any<CoinDroppedMessage>());
        }

        [Test]
        public void ZeroDropChance_NeverDrops()
        {
            _settings.DropChance.Returns(0f);

            for (var i = 0; i < 200; i++)
            {
                Hit(HitOutcome.Pop);
            }

            _droppedPublisher.DidNotReceive().Publish(Arg.Any<CoinDroppedMessage>());
        }

        [Test]
        public void PartialDropChance_DropsSomeButNotAll()
        {
            _settings.DropChance.Returns(0.5f);

            var drops = CountDrops(Rolls(200));

            Assert.That(drops, Is.InRange(50, 150));
        }

        [Test]
        public void RetryOfTheSameLevel_DropsTheSameWay()
        {
            // A retry restarts the level the run died on, so Level never changes — only ResetRun reseeds.
            _settings.DropChance.Returns(0.3f);
            var firstAttempt = Rolls(60);

            _controller.ResetRun(2);
            var retry = Rolls(60);

            CollectionAssert.AreEqual(firstAttempt, retry);
        }

        [Test]
        public void ReturningToALevel_DropsTheSameWay()
        {
            _settings.DropChance.Returns(0.3f);
            var firstVisit = Rolls(60);

            _level.Value = CoinsFromLevel + 1;
            Rolls(10);
            _level.Value = CoinsFromLevel;
            var secondVisit = Rolls(60);

            CollectionAssert.AreEqual(firstVisit, secondVisit);
        }

        [Test]
        public void DifferentLevels_DropDifferently()
        {
            _settings.DropChance.Returns(0.3f);
            var levelA = Rolls(60);

            _level.Value = CoinsFromLevel + 1;
            var levelB = Rolls(60);

            CollectionAssert.AreNotEqual(levelA, levelB);
        }

        [Test]
        public void NonPops_DoNotConsumeRolls()
        {
            // A deflect between pops must not shift which pops drop, or retries with a different
            // number of bounces would drop differently.
            _settings.DropChance.Returns(0.3f);
            var popsOnly = Rolls(40);

            _controller.ResetRun(2);
            var interleaved = new List<bool>();
            for (var i = 0; i < 40; i++)
            {
                Hit(HitOutcome.Deflect);
                interleaved.Add(RollOnce());
            }

            CollectionAssert.AreEqual(popsOnly, interleaved);
        }

        private List<bool> Rolls(int count)
        {
            var results = new List<bool>(count);
            for (var i = 0; i < count; i++)
            {
                results.Add(RollOnce());
            }

            return results;
        }

        private bool RollOnce()
        {
            _droppedPublisher.ClearReceivedCalls();
            Hit(HitOutcome.Pop);
            return _droppedPublisher.ReceivedCalls().Any();
        }

        private static int CountDrops(IEnumerable<bool> rolls)
        {
            var count = 0;
            foreach (var dropped in rolls)
            {
                if (dropped)
                {
                    count++;
                }
            }

            return count;
        }

        private void Hit(HitOutcome outcome, Vector3 position = default)
        {
            _hitHandler.Handle(new ActorHitMessage(null, position, Vector3.zero, outcome));
        }
    }
}
