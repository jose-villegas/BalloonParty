using BalloonParty.Configuration.HeadsUp;
using BalloonParty.UI.HeadsUp;
using NUnit.Framework;

namespace BalloonParty.Tests.UI
{
    [TestFixture]
    public class HeadsUpStyleTests
    {
        private const float Size = 1.1f;
        private const float Growth = 0.25f;
        private const float MaxSize = 1.6f;

        [Test]
        public void SizeAt_ACountOfOne_IsTheBaseSize()
        {
            Assert.AreEqual(Size, HeadsUpSizing.SizeAt(Size, Growth, MaxSize, 1), 1e-5f);
        }

        [Test]
        public void SizeAt_EachDoubling_AddsTheSameStep()
        {
            // A cap out of reach, so only the growth shows.
            const float noCap = 10f;
            var two = HeadsUpSizing.SizeAt(Size, Growth, noCap, 2) - HeadsUpSizing.SizeAt(Size, Growth, noCap, 1);
            var four = HeadsUpSizing.SizeAt(Size, Growth, noCap, 4) - HeadsUpSizing.SizeAt(Size, Growth, noCap, 2);

            Assert.AreEqual(Size * Growth, two, 1e-5f);
            Assert.AreEqual(two, four, 1e-5f);
        }

        [Test]
        public void SizeAt_ALongChain_StopsAtTheMax()
        {
            Assert.AreEqual(MaxSize, HeadsUpSizing.SizeAt(Size, Growth, MaxSize, 64), 1e-5f);
        }

        [Test]
        public void SizeAt_NoGrowth_StaysAtTheBase()
        {
            Assert.AreEqual(0.9f, HeadsUpSizing.SizeAt(0.9f, 0f, 0.9f, 5), 1e-5f);
        }

        [Test]
        public void SizeAt_ACountBelowOne_IsTreatedAsOne()
        {
            Assert.AreEqual(Size, HeadsUpSizing.SizeAt(Size, Growth, MaxSize, 0), 1e-5f);
        }
    }
}
