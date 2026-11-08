using BalloonParty.Projectile;
using BalloonParty.Projectile.Model;
using NSubstitute;
using NUnit.Framework;

namespace BalloonParty.Tests.Projectile
{
    [TestFixture]
    public class ProjectileTallyTests
    {
        [Test]
        public void Record_EachTime_CountsUp()
        {
            var tally = new ProjectileTally();
            var ball = Substitute.For<IProjectileModel>();

            Assert.AreEqual(1, tally.Record(ball));
            Assert.AreEqual(2, tally.Record(ball));
            Assert.AreEqual(3, tally.Record(ball));
        }

        [Test]
        public void Record_TwoBalls_CountSeparately()
        {
            var tally = new ProjectileTally();
            var first = Substitute.For<IProjectileModel>();
            var second = Substitute.For<IProjectileModel>();

            tally.Record(first);
            tally.Record(first);

            Assert.AreEqual(1, tally.Record(second));
        }

        [Test]
        public void Forget_ThenRecord_StartsAtOne()
        {
            var tally = new ProjectileTally();
            var ball = Substitute.For<IProjectileModel>();
            tally.Record(ball);
            tally.Record(ball);

            tally.Forget(ball);

            Assert.AreEqual(1, tally.Record(ball));
        }
    }
}
