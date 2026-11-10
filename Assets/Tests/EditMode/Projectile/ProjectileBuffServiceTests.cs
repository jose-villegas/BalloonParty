using BalloonParty.Projectile;
using BalloonParty.Projectile.Buffs;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared.Messages;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UniRx;

namespace BalloonParty.Tests.Projectile
{
    [TestFixture]
    public class ProjectileBuffServiceTests
    {
        private ProjectileRegistry _registry;
        private ProjectileBuffService _service;
        private IPublisher<ProjectileBuffGainedMessage> _gainedPublisher;

        [SetUp]
        public void SetUp()
        {
            _registry = new ProjectileRegistry();

            _gainedPublisher = Substitute.For<IPublisher<ProjectileBuffGainedMessage>>();
            _service = new ProjectileBuffService(_registry, _gainedPublisher);
            _service.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _service.Dispose();
        }

        [Test]
        public void Apply_LiveBall_AddsTheBuff()
        {
            var ball = Fly();

            _service.Apply(ball, NewBuff(new ManualEndCondition()));

            Assert.IsTrue(ball.HasBuff(ProjectileBuffId.Speed));
        }

        [Test]
        public void Apply_DeadBall_DisposesTheBuffAndAddsNothing()
        {
            var ball = Fly();
            _registry.Land(ball);
            var condition = new ManualEndCondition();

            _service.Apply(ball, NewBuff(condition));

            Assert.IsFalse(ball.HasBuff(ProjectileBuffId.Speed));
            Assert.IsTrue(condition.IsDisposed, "a buff nobody owns must not leak its end-condition subscription");
        }

        [Test]
        public void Apply_NullBall_DisposesTheBuff()
        {
            var condition = new ManualEndCondition();

            _service.Apply(null, NewBuff(condition));

            Assert.IsTrue(condition.IsDisposed);
        }

        [Test]
        public void Apply_EndConditionAlreadyExpired_RemovesTheBuffCleanly()
        {
            var ball = Fly();
            var condition = new ManualEndCondition();
            condition.Expire();

            Assert.DoesNotThrow(() => _service.Apply(ball, NewBuff(condition)));

            Assert.IsFalse(ball.HasBuff(ProjectileBuffId.Speed));
            Assert.IsTrue(condition.IsDisposed);
        }

        [Test]
        public void Apply_RetiredBall_DisposesTheBuff()
        {
            var ball = Fly();
            _registry.Retire(ball);
            var condition = new ManualEndCondition();

            _service.Apply(ball, NewBuff(condition));

            Assert.IsFalse(ball.HasBuff(ProjectileBuffId.Speed));
            Assert.IsTrue(condition.IsDisposed);
        }

        [Test]
        public void EndConditionExpires_RemovesOnlyThatBuff()
        {
            var ball = Fly();
            var expiring = new ManualEndCondition();
            _service.Apply(ball, NewBuff(expiring, ProjectileBuffId.Speed));
            _service.Apply(ball, NewBuff(new ManualEndCondition(), ProjectileBuffId.RainbowShield));

            expiring.Expire();

            Assert.IsFalse(ball.HasBuff(ProjectileBuffId.Speed));
            Assert.IsTrue(ball.HasBuff(ProjectileBuffId.RainbowShield));
            Assert.IsTrue(expiring.IsDisposed);
        }

        [Test]
        public void Retire_DropsThatBallsBuffs_AndKeepsALiveBallsBuffs()
        {
            var dying = Fly();
            var surviving = Fly();
            var dyingCondition = new ManualEndCondition();
            var survivingCondition = new ManualEndCondition();
            _service.Apply(dying, NewBuff(dyingCondition));
            _service.Apply(surviving, NewBuff(survivingCondition));

            // Landed, as the view does before publishing Destroyed: the buff stays readable through that
            // dispatch and goes only when the ball retires after it.
            _registry.Land(dying);
            Assert.IsTrue(dying.HasBuff(ProjectileBuffId.Speed), "still readable through its own destroy dispatch");
            _registry.Retire(dying);

            Assert.IsFalse(dying.HasBuff(ProjectileBuffId.Speed));
            Assert.IsTrue(dyingCondition.IsDisposed);
            Assert.IsTrue(surviving.HasBuff(ProjectileBuffId.Speed));
            Assert.IsFalse(survivingCondition.IsDisposed);
        }

        [Test]
        public void Apply_AKindOfBuffTheBallLacked_AnnouncesIt()
        {
            var ball = Fly();

            _service.Apply(ball, NewBuff(new ManualEndCondition(), ProjectileBuffId.RainbowShield));

            _gainedPublisher.Received(1).Publish(Arg.Is<ProjectileBuffGainedMessage>(m =>
                m.Projectile == ball && m.BuffId == ProjectileBuffId.RainbowShield));
        }

        // A Snipe charge re-applies the rainbow to a ball that already has it: nothing new to announce.
        [Test]
        public void Apply_AKindTheBallAlreadyHolds_AnnouncesNothingMore()
        {
            var ball = Fly();
            _service.Apply(ball, NewBuff(new ManualEndCondition(), ProjectileBuffId.RainbowShield));

            _service.Apply(ball, NewBuff(new ManualEndCondition(), ProjectileBuffId.RainbowShield));

            _gainedPublisher.Received(1).Publish(Arg.Any<ProjectileBuffGainedMessage>());
        }

        [Test]
        public void Apply_TheSameKindAgainAfterTheFirstExpired_AnnouncesAgain()
        {
            var ball = Fly();
            var first = new ManualEndCondition();
            _service.Apply(ball, NewBuff(first, ProjectileBuffId.RainbowShield));
            first.Expire();

            _service.Apply(ball, NewBuff(new ManualEndCondition(), ProjectileBuffId.RainbowShield));

            _gainedPublisher.Received(2).Publish(Arg.Any<ProjectileBuffGainedMessage>());
        }

        [Test]
        public void Apply_ABuffAlreadyExpired_AnnouncesNothing()
        {
            var condition = new ManualEndCondition();
            condition.Expire();

            _service.Apply(Fly(), NewBuff(condition));

            _gainedPublisher.DidNotReceive().Publish(Arg.Any<ProjectileBuffGainedMessage>());
        }

        [Test]
        public void Apply_ToADeadBall_AnnouncesNothing()
        {
            var ball = Fly();
            _registry.Land(ball);

            _service.Apply(ball, NewBuff(new ManualEndCondition()));

            _gainedPublisher.DidNotReceive().Publish(Arg.Any<ProjectileBuffGainedMessage>());
        }

        private ProjectileModel Fly()
        {
            var ball = new ProjectileModel();
            _registry.Load(ball, null);
            _registry.Launch(ball);
            return ball;
        }

        private static ProjectileBuff NewBuff(
            IProjectileBuffEndCondition condition, ProjectileBuffId id = ProjectileBuffId.Speed)
        {
            return new ProjectileBuff(id, 2f, BuffModifierOp.Multiplicative, condition);
        }

        private sealed class ManualEndCondition : IProjectileBuffEndCondition
        {
            private readonly ReactiveProperty<bool> _expired = new(false);

            public IReadOnlyReactiveProperty<bool> Expired => _expired;
            public bool IsDisposed { get; private set; }

            public void Expire()
            {
                _expired.Value = true;
            }

            public void Dispose()
            {
                IsDisposed = true;
            }
        }
    }
}
