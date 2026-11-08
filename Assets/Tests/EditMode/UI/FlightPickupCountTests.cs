using BalloonParty.Projectile.Model;
using BalloonParty.UI.HeadsUp;
using NSubstitute;
using NUnit.Framework;

namespace BalloonParty.Tests.UI
{
    [TestFixture]
    public class FlightPickupCountTests
    {
        [Test]
        public void Record_EachPickup_CountsUp()
        {
            var count = new FlightPickupCount();
            var ball = Substitute.For<IProjectileModel>();

            Assert.AreEqual(1, count.Record(ball));
            Assert.AreEqual(2, count.Record(ball));
            Assert.AreEqual(3, count.Record(ball));
        }

        [Test]
        public void Record_TwoBalls_CountSeparately()
        {
            var count = new FlightPickupCount();
            var first = Substitute.For<IProjectileModel>();
            var second = Substitute.For<IProjectileModel>();

            count.Record(first);
            count.Record(first);

            Assert.AreEqual(1, count.Record(second));
        }

        [Test]
        public void Forget_AfterTheFlightEnds_TheNextFlightStartsAtOne()
        {
            var count = new FlightPickupCount();
            var ball = Substitute.For<IProjectileModel>();
            count.Record(ball);
            count.Record(ball);

            count.Forget(ball);

            Assert.AreEqual(1, count.Record(ball));
        }
    }
}
