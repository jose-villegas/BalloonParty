using System.Collections.Generic;
using BalloonParty.Configuration.HeadsUp;
using BalloonParty.Game.HeadsUp;
using BalloonParty.Game.HeadsUp.Routing;
using BalloonParty.Projectile;
using BalloonParty.Projectile.Controller;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared.Messages;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using static BalloonParty.Tests.HeadsUp.HeadsUpRouterTestKit;

namespace BalloonParty.Tests.HeadsUp
{
    /// <summary>
    ///     What <see cref="CombatHeadsUpRouter" /> asks for: the streak keyed to its ball and colour, the tap and
    ///     the shatter where they happened, and the pierce and the rainbow at the ball while it flies.
    /// </summary>
    [TestFixture]
    public class CombatHeadsUpRouterTests
    {
        private static readonly Vector3 BallPosition = new(1f, 2f, 0f);

        private List<HeadsUpRequest> _requests;
        private IProjectileRegistry _projectiles;
        private Subject<IProjectileModel> _pierceArmed;
        private IMessageHandler<StreakGrewMessage> _onStreak;
        private IMessageHandler<SpeedTapMintedMessage> _onTap;
        private IMessageHandler<PierceDischargedMessage> _onDischarge;
        private IMessageHandler<ProjectileBuffGainedMessage> _onBuff;
        private IProjectileModel _ball;
        private CombatHeadsUpRouter _router;

        [SetUp]
        public void SetUp()
        {
            var headsUps = new RecordingHeadsUps();
            _requests = headsUps.Requests;
            _ball = Substitute.For<IProjectileModel>();
            _projectiles = Substitute.For<IProjectileRegistry>();
            _pierceArmed = new Subject<IProjectileModel>();
            var pierce = Substitute.For<IActiveProjectilePierce>();
            pierce.PierceArmed.Returns(_pierceArmed);
            _router = new CombatHeadsUpRouter(headsUps, _projectiles, pierce,
                CaptureSubscriber<StreakGrewMessage>(h => _onStreak = h),
                CaptureSubscriber<SpeedTapMintedMessage>(h => _onTap = h),
                CaptureSubscriber<PierceDischargedMessage>(h => _onDischarge = h),
                CaptureSubscriber<ProjectileBuffGainedMessage>(h => _onBuff = h));
            _router.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _router.Dispose();
            _pierceArmed.Dispose();
        }

        [Test]
        public void AStreakGrowing_IsWrittenAtThePop_KeyedToItsBallAndColour()
        {
            var pop = new Vector3(3f, 4f, 0f);

            _onStreak.Handle(new StreakGrewMessage(_ball, "Red", 4, pop));

            var request = _requests[0];
            Assert.AreEqual(HeadsUpStyleId.Streak, request.Style);
            Assert.AreEqual(4, request.Arg);
            Assert.AreEqual((Vector2)pop, request.Anchor.Position);
            Assert.AreEqual(new HeadsUpMergeKey(_ball, "Red"), request.MergeKey);
        }

        [Test]
        public void ASpeedTap_IsWrittenAtTheWall_WithItsTotal()
        {
            _onTap.Handle(new SpeedTapMintedMessage(_ball, new Vector3(5f, 0f, 0f), 2));

            Assert.AreEqual(HeadsUpStyleId.SpeedTap, _requests[0].Style);
            Assert.AreEqual(2, _requests[0].Arg);
            Assert.AreEqual(new Vector2(5f, 0f), _requests[0].Anchor.Position);
        }

        [Test]
        public void APierceDischarge_WritesHowManyToughOnesItShattered()
        {
            _onDischarge.Handle(new PierceDischargedMessage(_ball, Vector3.zero, 3, false, Vector3.up,
                new Vector3(0f, 6f, 0f)));

            Assert.AreEqual(HeadsUpStyleId.Shattered, _requests[0].Style);
            Assert.AreEqual(3, _requests[0].Arg);
            Assert.AreEqual(new Vector2(0f, 6f), _requests[0].Anchor.Position);
        }

        [Test]
        public void APierceArming_IsWrittenWhereTheBallIs()
        {
            Flying(_ball);

            _pierceArmed.OnNext(_ball);

            Assert.AreEqual(HeadsUpStyleId.Pierce, _requests[0].Style);
            Assert.AreEqual((Vector2)BallPosition, _requests[0].Anchor.Position);
        }

        [Test]
        public void APierceArmingOnAHeldBall_WritesNothing()
        {
            _pierceArmed.OnNext(_ball);

            Assert.IsEmpty(_requests);
        }

        [Test]
        public void TheRainbowPickedUp_IsWrittenWhereTheBallIs()
        {
            Flying(_ball);

            _onBuff.Handle(new ProjectileBuffGainedMessage(_ball, ProjectileBuffId.RainbowShield));

            Assert.AreEqual(HeadsUpStyleId.Rainbow, _requests[0].Style);
            Assert.AreEqual((Vector2)BallPosition, _requests[0].Anchor.Position);
        }

        [Test]
        public void AnotherBuff_WritesNothing()
        {
            Flying(_ball);

            _onBuff.Handle(new ProjectileBuffGainedMessage(_ball, ProjectileBuffId.Speed));

            Assert.IsEmpty(_requests);
        }

        private void Flying(IProjectileModel ball)
        {
            _projectiles.InFlightCount.Returns(1);
            _projectiles.InFlightAt(0).Returns(ball);
            _projectiles.InFlightPositionAt(0).Returns(BallPosition);
        }
    }
}
