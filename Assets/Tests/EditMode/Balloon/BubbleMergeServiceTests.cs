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
    ///     absorb slides one away), and when the service looks again.
    /// </summary>
    [TestFixture]
    public class BubbleMergeServiceTests
    {
        private static readonly Vector2Int Left = new(1, 1);
        private static readonly Vector2Int Right = new(2, 1);

        private SlotGrid _grid;
        private IGamePalette _palette;
        private IMessageHandler<ActorHitMessage> _hitHandler;
        private BubbleMergeService _service;

        [SetUp]
        public void SetUp()
        {
            var gridConfig = Substitute.For<ISlotGridConfig>();
            gridConfig.SlotsSize.Returns(new Vector2Int(4, 4));
            _grid = new SlotGrid(gridConfig, new BalancePathHolder());

            _palette = Substitute.For<IGamePalette>();
            _palette.Colors.Returns(new List<PaletteEntry>());

            var registry = new BalloonControllerRegistry(null, Substitute.For<IPublisher<BoardDepletedMessage>>());
            _service = new BubbleMergeService(
                _grid, registry, Substitute.For<IBalloonsConfiguration>(), CaptureHits());
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
            var left = Place(Left, 3);
            var right = Place(Right, 3);

            _service.Tick();

            // Between equals the higher column gives.
            Assert.AreEqual(5, left.HitsRemaining.Value);
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

        private BubbleClusterModel Place(Vector2Int slot, int bubbles)
        {
            var model = new BubbleClusterModel(new BalloonModelConfig(hitsToPop: 5, scoreValue: 1), _palette);
            model.SetBubbles(bubbles);
            _grid.Place(model, null, slot);
            return model;
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
