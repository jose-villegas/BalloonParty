using BalloonParty.Projectile;
using BalloonParty.Projectile.Model;
using NUnit.Framework;

namespace BalloonParty.Tests.Projectile
{
    /// <summary>
    ///     The lone-ball rule the doomed slow-mo, the board freeze and the held spawn share: one ball's last
    ///     breath may only warp the world while nothing else is flying under it.
    /// </summary>
    [TestFixture]
    public class ProjectileRegistryExtensionsTests
    {
        private ProjectileRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            _registry = new ProjectileRegistry();
        }

        [TearDown]
        public void TearDown()
        {
            _registry.Dispose();
        }

        [Test]
        public void FliesAlone_OnlyBallInFlight_True()
        {
            var ball = Fly();

            Assert.IsTrue(_registry.FliesAlone(ball));
        }

        [Test]
        public void FliesAlone_AnotherBallAlsoInFlight_FalseForBoth()
        {
            var first = Fly();
            var second = Fly();

            Assert.IsFalse(_registry.FliesAlone(first));
            Assert.IsFalse(_registry.FliesAlone(second));
        }

        [Test]
        public void FliesAlone_OtherBallLands_TrueAgainForTheSurvivor()
        {
            var first = Fly();
            var second = Fly();

            _registry.Land(first);

            Assert.IsTrue(_registry.FliesAlone(second));
            Assert.IsFalse(_registry.FliesAlone(first), "a landed ball is no longer the one flying");
        }

        [Test]
        public void FliesAlone_NullBall_False()
        {
            Fly();

            Assert.IsFalse(_registry.FliesAlone(null));
        }

        [Test]
        public void FliesAlone_BallHeldInThrowerWhileAnotherFlies_False()
        {
            Fly();
            var held = new ProjectileModel();
            _registry.Load(held, null);

            Assert.IsFalse(_registry.FliesAlone(held), "a held ball is not in flight");
        }

        [Test]
        public void DoomedShotFliesAlone_LoneDoomedBall_True()
        {
            Fly().IsLastShieldApproach.Value = true;

            Assert.IsTrue(_registry.DoomedShotFliesAlone());
        }

        [Test]
        public void DoomedShotFliesAlone_LoneBallNotDoomed_False()
        {
            Fly();

            Assert.IsFalse(_registry.DoomedShotFliesAlone());
        }

        [Test]
        public void DoomedShotFliesAlone_DoomedBallWithCompany_False()
        {
            Fly().IsLastShieldApproach.Value = true;
            Fly();

            Assert.IsFalse(_registry.DoomedShotFliesAlone());
        }

        [Test]
        public void DoomedShotFliesAlone_NothingInFlight_False()
        {
            Assert.IsFalse(_registry.DoomedShotFliesAlone());
        }

        private ProjectileModel Fly()
        {
            var ball = new ProjectileModel();
            _registry.Load(ball, null);
            _registry.Launch(ball);
            return ball;
        }
    }
}
