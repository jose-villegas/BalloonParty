using System;
using System.Collections.Generic;
using BalloonParty.Projectile;
using BalloonParty.Projectile.Controller;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared.Messages;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UniRx;

namespace BalloonParty.Tests.Projectile
{
    /// <summary>
    ///     <see cref="ActiveProjectilePierce.PierceArmed" /> fires once each time a ball starts piercing,
    ///     which the folded <see cref="ActiveProjectilePierce.IsPiercing" /> can't say: it stays true when
    ///     a second ball arms, and a Snipe re-arm keeps a ball piercing without arming it again.
    /// </summary>
    [TestFixture]
    public class ActiveProjectilePierceTests
    {
        private readonly List<IProjectileModel> _armed = new();

        private ProjectileRegistry _registry;
        private ActiveProjectilePierce _pierce;
        private IMessageHandler<ProjectileLoadedMessage> _loadedHandler;
        private IDisposable _armedSubscription;

        [SetUp]
        public void SetUp()
        {
            var loadedSubscriber = Substitute.For<ISubscriber<ProjectileLoadedMessage>>();
            loadedSubscriber
                .Subscribe(
                    Arg.Do<IMessageHandler<ProjectileLoadedMessage>>(h => _loadedHandler = h),
                    Arg.Any<MessageHandlerFilter<ProjectileLoadedMessage>[]>())
                .Returns(Substitute.For<IDisposable>());

            _registry = new ProjectileRegistry();
            _pierce = new ActiveProjectilePierce(_registry, loadedSubscriber);
            _pierce.Start();
            _armed.Clear();
            _armedSubscription = _pierce.PierceArmed.Subscribe(_armed.Add);
        }

        [TearDown]
        public void TearDown()
        {
            _armedSubscription.Dispose();
            _pierce.Dispose();
        }

        [Test]
        public void ABallArming_FiresOnceWithThatBall()
        {
            var ball = Load();

            ball.IsPiercing.Value = true;

            CollectionAssert.AreEqual(new[] { ball }, _armed);
        }

        [Test]
        public void ALoadedBallNotPiercing_FiresNothing()
        {
            Load();

            Assert.IsEmpty(_armed);
        }

        [Test]
        public void ASecondBallArmingWhileTheFirstPierces_FiresForIt_ThoughIsPiercingNeverChanges()
        {
            var first = Load();
            var second = Load();
            first.IsPiercing.Value = true;
            var piercingChanges = 0;
            using var watch = _pierce.IsPiercing.Skip(1).Subscribe(_ => piercingChanges++);

            second.IsPiercing.Value = true;

            CollectionAssert.AreEqual(new[] { first, second }, _armed);
            Assert.AreEqual(0, piercingChanges, "the fold was already true, so only PierceArmed can tell");
        }

        // A banked Snipe charge spends the pierce without IsPiercing dipping (SpendPierce): not an arming.
        [Test]
        public void ABallStayingArmed_FiresNoSecondTime()
        {
            var ball = Load();
            ball.IsPiercing.Value = true;

            ball.IsPiercing.Value = true;

            Assert.AreEqual(1, _armed.Count);
        }

        [Test]
        public void ABallArmingAgainAfterItsPierceEnded_FiresAgain()
        {
            var ball = Load();
            ball.IsPiercing.Value = true;
            ball.IsPiercing.Value = false;

            ball.IsPiercing.Value = true;

            CollectionAssert.AreEqual(new[] { ball, ball }, _armed);
        }

        [Test]
        public void ARetiredBall_IsNoLongerWatched()
        {
            var ball = Load();
            _registry.Retire(ball);

            ball.IsPiercing.Value = true;

            Assert.IsEmpty(_armed);
        }

        [Test]
        public void AnArming_ListenersAlreadyReadTheFoldAsPiercing()
        {
            var ball = Load();
            var piercingWhenArmed = false;
            using var watch = _pierce.PierceArmed.Subscribe(_ => piercingWhenArmed = _pierce.IsPiercing.Value);

            ball.IsPiercing.Value = true;

            Assert.IsTrue(piercingWhenArmed);
        }

        private ProjectileModel Load()
        {
            var ball = new ProjectileModel();
            _registry.Load(ball, null);
            _loadedHandler.Handle(new ProjectileLoadedMessage(ball));
            return ball;
        }
    }
}
