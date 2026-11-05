using System;
using BalloonParty.Projectile.Buffs;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared.Messages;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Projectile
{
    [TestFixture]
    public class WallBounceEndConditionTests
    {
        private IMessageHandler<ShieldLostMessage> _shieldLost;
        private ProjectileModel _ownBall;
        private WallBounceEndCondition _condition;

        [SetUp]
        public void SetUp()
        {
            var subscriber = Substitute.For<ISubscriber<ShieldLostMessage>>();
            subscriber
                .Subscribe(
                    Arg.Do<IMessageHandler<ShieldLostMessage>>(h => _shieldLost = h),
                    Arg.Any<MessageHandlerFilter<ShieldLostMessage>[]>())
                .Returns(Substitute.For<IDisposable>());

            _ownBall = new ProjectileModel();
            _condition = new WallBounceEndCondition(subscriber, _ownBall);
        }

        [Test]
        public void ShieldLost_OfTheBuffedBall_ExpiresTheBuff()
        {
            _shieldLost.Handle(new ShieldLostMessage(_ownBall, Vector3.zero));

            Assert.IsTrue(_condition.Expired.Value);
        }

        [Test]
        public void ShieldLost_OfAnotherBall_DoesNotExpireTheBuff()
        {
            _shieldLost.Handle(new ShieldLostMessage(new ProjectileModel(), Vector3.zero));

            Assert.IsFalse(_condition.Expired.Value);
        }

        [Test]
        public void ShieldLost_WithNoBall_DoesNotExpireTheBuff()
        {
            _shieldLost.Handle(new ShieldLostMessage(null, Vector3.zero));

            Assert.IsFalse(_condition.Expired.Value);
        }
    }
}
