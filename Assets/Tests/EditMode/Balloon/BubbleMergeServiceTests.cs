using System;
using System.Collections.Generic;
using BalloonParty.Balloon.Controller;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Palette;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Balloon
{
    /// <summary>
    ///     The service over a real grid: merges where the giver keeps bubbles, which need no controller (only an
    ///     absorb slides one away), and when the service looks again. Clusters spawn with the shipped HitsToPop, below
    ///     the merge cap, as they do in play. Each merge's <see cref="BubbleMergedMessage" /> is what the view plays.
    /// </summary>
    [TestFixture]
    public class BubbleMergeServiceTests
    {
        private const int SpawnHits = 3;
        private const int MergeMax = 5;

        private static readonly Vector2Int Left = new(1, 1);
        private static readonly Vector2Int Right = new(2, 1);

        private readonly List<BubbleMergedMessage> _merged = new();
        // Each cluster's count as the message went out, (left, right).
        private readonly List<(int, int)> _countsAtPublish = new();

        private SlotGrid _grid;
        private IGamePalette _palette;
        private IMessageHandler<ActorHitMessage> _hitHandler;
        private BubbleClusterModel _left;
        private BubbleClusterModel _right;
        private BubbleMergeService _service;

        [SetUp]
        public void SetUp()
        {
            var gridConfig = Substitute.For<ISlotGridConfig>();
            gridConfig.SlotsSize.Returns(new Vector2Int(4, 4));
            _grid = new SlotGrid(gridConfig, new BalancePathHolder());

            _palette = Substitute.For<IGamePalette>();
            _palette.Colors.Returns(new List<PaletteEntry>());

            var balloonsConfig = Substitute.For<IBalloonsConfiguration>();
            balloonsConfig.BubbleMergeMax.Returns(MergeMax);
            var registry = new BalloonControllerRegistry(null, Substitute.For<IPublisher<BoardDepletedMessage>>());
            _merged.Clear();
            _countsAtPublish.Clear();
            _left = null;
            _right = null;
            _service = new BubbleMergeService(_grid, registry, balloonsConfig, CaptureHits(), CaptureMerges());
            _service.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _service.Dispose();
        }

        [Test]
        public void TwoClustersThatDoNotFit_FillTheReceiverAndTheGiverKeepsTheRest()
        {
            // Both at their spawn hits: with the receiver's own HitsToPop as the limit, it counted as full and two
            // 3s sat side by side unmerged.
            var left = Place(Left, SpawnHits);
            var right = Place(Right, SpawnHits);

            _service.Tick();

            // Between equals the higher column gives.
            Assert.AreEqual(MergeMax, left.HitsRemaining.Value);
            Assert.AreEqual(1, right.HitsRemaining.Value);
            Assert.AreSame(right, _grid.At(Right));
        }

        [Test]
        public void AFullReceiver_TakesNothing()
        {
            var full = Place(Left, 5);
            var giver = Place(Right, 3);

            _service.Tick();

            Assert.AreEqual(5, full.HitsRemaining.Value);
            Assert.AreEqual(3, giver.HitsRemaining.Value);
        }

        [Test]
        public void AHitThatMakesRoom_MergesOnTheNextFrame()
        {
            var full = Place(Left, 5);
            var giver = Place(Right, 3);
            _service.Tick();

            full.SetBubbles(4);
            _hitHandler.Handle(new ActorHitMessage(full, Vector3.zero, Vector3.up, HitOutcome.PassThrough));
            _service.Tick();

            Assert.AreEqual(5, full.HitsRemaining.Value);
            Assert.AreEqual(2, giver.HitsRemaining.Value);
        }

        [Test]
        public void WithoutAGridChangeOrAHit_ItDoesNotLookAgain()
        {
            var full = Place(Left, 5);
            var giver = Place(Right, 3);
            _service.Tick();

            // A count changed by something the service doesn't hear about waits for the next change it does.
            full.SetBubbles(4);
            _service.Tick();

            Assert.AreEqual(4, full.HitsRemaining.Value);
            Assert.AreEqual(3, giver.HitsRemaining.Value);
        }

        [Test]
        public void APartialMerge_PublishesWhoGaveHowManyAndWhere()
        {
            _left = Place(Left, SpawnHits);
            _right = Place(Right, SpawnHits);

            _service.Tick();

            Assert.AreEqual(1, _merged.Count);
            var merge = _merged[0];
            Assert.AreEqual(Right, merge.GiverSlot);
            Assert.AreEqual(Left, merge.ReceiverSlot);
            Assert.AreEqual(_grid.IndexToWorldPosition(Right), merge.From);
            Assert.AreEqual(_grid.IndexToWorldPosition(Left), merge.To);
            Assert.AreEqual(MergeMax - SpawnHits, merge.Given);
            Assert.IsFalse(merge.Absorbed);
        }

        [Test]
        public void TheMessage_GoesOutBeforeTheCountsChange()
        {
            // The view holds each cluster at the count it showed and steps it as bubbles hop: it must see the old ones.
            _left = Place(Left, SpawnHits);
            _right = Place(Right, SpawnHits);

            _service.Tick();

            Assert.AreEqual((SpawnHits, SpawnHits), _countsAtPublish[0]);
            Assert.AreEqual(MergeMax, _left.HitsRemaining.Value);
        }

        [Test]
        public void NoMerge_PublishesNothing()
        {
            Place(Left, 5);
            Place(Right, 3);

            _service.Tick();

            Assert.IsEmpty(_merged);
        }

        [Test]
        public void AGiverWithNoControllerToAbsorbIt_PublishesNothing()
        {
            // It would give all it has, but nothing can slide it away: no merge happens, so nothing is shown.
            var receiver = Place(Left, SpawnHits);
            Place(Right, 1);

            _service.Tick();

            Assert.IsEmpty(_merged);
            Assert.AreEqual(SpawnHits, receiver.HitsRemaining.Value);
        }

        [Test]
        public void EachMergeOfAPass_PublishesOnce()
        {
            // Two separate pairs merge in the same pass.
            Place(Left, SpawnHits);
            Place(Right, SpawnHits);
            Place(new Vector2Int(1, 3), SpawnHits);
            Place(new Vector2Int(2, 3), SpawnHits);

            _service.Tick();

            Assert.AreEqual(2, _merged.Count);
            Assert.AreNotEqual(_merged[0].ReceiverSlot, _merged[1].ReceiverSlot);
        }

        private BubbleClusterModel Place(Vector2Int slot, int bubbles)
        {
            var model = new BubbleClusterModel(new BalloonModelConfig(hitsToPop: SpawnHits, scoreValue: 1), _palette);
            model.SetBubbles(bubbles);
            _grid.Place(model, null, slot);
            return model;
        }

        private IPublisher<BubbleMergedMessage> CaptureMerges()
        {
            var publisher = Substitute.For<IPublisher<BubbleMergedMessage>>();
            publisher
                .When(p => p.Publish(Arg.Any<BubbleMergedMessage>()))
                .Do(call =>
                {
                    _merged.Add(call.Arg<BubbleMergedMessage>());
                    _countsAtPublish.Add((_left?.HitsRemaining.Value ?? 0, _right?.HitsRemaining.Value ?? 0));
                });
            return publisher;
        }

        private ISubscriber<ActorHitMessage> CaptureHits()
        {
            var subscriber = Substitute.For<ISubscriber<ActorHitMessage>>();
            subscriber
                .Subscribe(
                    Arg.Do<IMessageHandler<ActorHitMessage>>(h => _hitHandler = h),
                    Arg.Any<MessageHandlerFilter<ActorHitMessage>[]>())
                .Returns(Substitute.For<IDisposable>());
            return subscriber;
        }
    }
}
