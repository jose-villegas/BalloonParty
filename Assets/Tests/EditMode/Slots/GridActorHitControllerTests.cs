using BalloonParty.Balloon.Model;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Actor.Archetype;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Slots
{
    [TestFixture]
    public class GridActorHitControllerTests
    {
        private SlotGrid _grid;
        private GridActorHitController _controller;

        [SetUp]
        public void SetUp()
        {
            var config = Substitute.For<IGameConfiguration>();
            config.SlotsSize.Returns(new Vector2Int(4, 4));
            config.SlotSeparation.Returns(new Vector2(1f, 1f));
            config.SlotsOffset.Returns(Vector2.zero);

            _grid = new SlotGrid(config, new BalancePathHolder());
            _controller = new GridActorHitController(
                Substitute.For<MessagePipe.ISubscriber<ActorHitMessage>>(),
                _grid);
            _controller.Start();
        }

        [Test]
        public void GridActorHitController_OnActorHit_IBalloonModel_IsIgnored()
        {
            var slot = new Vector2Int(0, 0);
            var balloon = new BalloonModel();
            _grid.Place(balloon, null, slot);

            var msg = new ActorHitMessage(balloon, Vector3.zero, Vector3.zero, HitOutcome.Pop);
            _controller.OnActorHit(msg);

            Assert.IsFalse(_grid.IsEmpty(slot.x, slot.y));
        }

        [Test]
        public void GridActorHitController_OnActorHit_Gatekeeper_WhenHitsReachZero_RemovesFromGrid()
        {
            var slot = new Vector2Int(1, 1);
            var gatekeeper = new GatekeeperActorModel(hitsToPop: 1);
            gatekeeper.EvaluateHit(new DamageContext(1));
            _grid.Place(gatekeeper, null, slot);

            var msg = new ActorHitMessage(gatekeeper, Vector3.zero, Vector3.zero, HitOutcome.Pop);
            _controller.OnActorHit(msg);

            Assert.IsTrue(_grid.IsEmpty(slot.x, slot.y));
        }

        [Test]
        public void GridActorHitController_OnActorHit_Deflector_IsNotRemoved()
        {
            var slot = new Vector2Int(2, 2);
            var deflector = new DeflectorActorModel();
            _grid.Place(deflector, null, slot);

            var msg = new ActorHitMessage(deflector, Vector3.zero, Vector3.zero, HitOutcome.Deflect);
            _controller.OnActorHit(msg);

            Assert.IsFalse(_grid.IsEmpty(slot.x, slot.y));
        }
    }
}

