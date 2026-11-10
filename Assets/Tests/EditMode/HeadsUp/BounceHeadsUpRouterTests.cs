using System.Collections.Generic;
using BalloonParty.Configuration.HeadsUp;
using BalloonParty.Game.HeadsUp;
using BalloonParty.Game.HeadsUp.Routing;
using BalloonParty.Projectile;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Grid;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using static BalloonParty.Tests.HeadsUp.HeadsUpRouterTestKit;

namespace BalloonParty.Tests.HeadsUp
{
    /// <summary>
    ///     What <see cref="BounceHeadsUpRouter" /> asks for: pickups counted per ball and flight, a ricochet's paid
    ///     shield written under the ricochet instead, the last shield's wall loud, and the ricochet's count.
    /// </summary>
    [TestFixture]
    public class BounceHeadsUpRouterTests
    {
        private static readonly Vector2Int Slot = new(1, 2);

        private List<HeadsUpRequest> _requests;
        private SlotGrid _grid;
        private Subject<IProjectileModel> _retired;
        private IMessageHandler<ShieldGainedMessage> _onShield;
        private IMessageHandler<WallHitMessage> _onWall;
        private IMessageHandler<DeflectChainMessage> _onChain;
        private IProjectileModel _ball;
        private BounceHeadsUpRouter _router;

        [SetUp]
        public void SetUp()
        {
            var headsUps = new RecordingHeadsUps();
            _requests = headsUps.Requests;
            _ball = Substitute.For<IProjectileModel>();
            var gridConfig = Substitute.For<ISlotGridConfig>();
            gridConfig.SlotsSize.Returns(new Vector2Int(4, 4));
            gridConfig.SlotSeparation.Returns(new Vector2(1f, 1f));
            _grid = new SlotGrid(gridConfig, new BalancePathHolder());
            _retired = new Subject<IProjectileModel>();
            var projectiles = Substitute.For<IProjectileRegistry>();
            projectiles.Retired.Returns(_retired);
            _router = new BounceHeadsUpRouter(headsUps, _grid, projectiles,
                CaptureSubscriber<ShieldGainedMessage>(h => _onShield = h),
                CaptureSubscriber<WallHitMessage>(h => _onWall = h),
                CaptureSubscriber<DeflectChainMessage>(h => _onChain = h));
            _router.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _router.Dispose();
            _retired.Dispose();
        }

        [Test]
        public void APickup_IsWrittenAtItsSlot()
        {
            _onShield.Handle(new ShieldGainedMessage(_ball, Slot));

            Assert.AreEqual(HeadsUpStyleId.BouncePickup, _requests[0].Style);
            Assert.AreEqual(1, _requests[0].Arg);
            Assert.AreEqual((Vector2)_grid.IndexToWorldPosition(Slot), _requests[0].Anchor.Position);
        }

        [Test]
        public void PickupsCount_PerBall()
        {
            var other = Substitute.For<IProjectileModel>();

            _onShield.Handle(new ShieldGainedMessage(_ball, Slot));
            _onShield.Handle(new ShieldGainedMessage(_ball, Slot, ShieldGainSource.Streak));
            _onShield.Handle(new ShieldGainedMessage(other, Slot));

            Assert.AreEqual(new[] { 1, 2, 1 }, ArgsOf(_requests));
        }

        [Test]
        public void ABallRetiring_StartsItsCountOver()
        {
            _onShield.Handle(new ShieldGainedMessage(_ball, Slot));
            _retired.OnNext(_ball);

            _onShield.Handle(new ShieldGainedMessage(_ball, Slot));

            Assert.AreEqual(1, _requests[1].Arg);
        }

        // The ricochet writes its own +1 under itself, so the shield it paid doesn't write a second.
        [Test]
        public void ARicochetsShield_WritesNothing_ButStillCounts()
        {
            _onShield.Handle(new ShieldGainedMessage(_ball, Slot, ShieldGainSource.Ricochet));

            _onShield.Handle(new ShieldGainedMessage(_ball, Slot));

            Assert.AreEqual(1, _requests.Count);
            Assert.AreEqual(2, _requests[0].Arg);
        }

        [Test]
        public void AShieldWithNoBall_WritesNothing()
        {
            _onShield.Handle(new ShieldGainedMessage(null, Slot));

            Assert.IsEmpty(_requests);
        }

        [TestCase(2, false)]
        [TestCase(1, false)]
        [TestCase(0, true)]
        public void AWall_SpendsOne_LoudWhenItLeavesTheLast(int shieldsRemaining, bool loud)
        {
            _onWall.Handle(new WallHitMessage(_ball, new Vector3(-3f, 1f, 0f), shieldsRemaining));

            Assert.AreEqual(loud ? HeadsUpStyleId.BounceWallLast : HeadsUpStyleId.BounceWall, _requests[0].Style);
            Assert.AreEqual(1, _requests[0].Arg);
            Assert.AreEqual(new Vector2(-3f, 1f), _requests[0].Anchor.Position);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ARicochet_WritesItsCount_WithThePaidShieldUnderIt(bool paidShield)
        {
            _onChain.Handle(new DeflectChainMessage(_ball, 3, new Vector3(2f, 2f, 0f), paidShield));

            Assert.AreEqual(paidShield ? HeadsUpStyleId.RicochetPaid : HeadsUpStyleId.Ricochet, _requests[0].Style);
            Assert.AreEqual(3, _requests[0].Arg);
            Assert.AreEqual(new Vector2(2f, 2f), _requests[0].Anchor.Position);
        }

        private static int[] ArgsOf(List<HeadsUpRequest> requests)
        {
            return requests.ConvertAll(request => request.Arg).ToArray();
        }
    }
}
