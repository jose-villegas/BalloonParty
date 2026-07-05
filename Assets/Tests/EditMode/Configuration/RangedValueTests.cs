using BalloonParty.Configuration;
using NUnit.Framework;

namespace BalloonParty.Tests.Configuration
{
    [TestFixture]
    public class RangedValueTests
    {
        [Test]
        public void RangedInt_Fixed_AlwaysReturnsMin()
        {
            var value = new RangedInt(3, 9, RangeMode.Fixed);

            Assert.AreEqual(3, value.Resolve(0f, new System.Random(1)));
            Assert.AreEqual(3, value.Resolve(1f, new System.Random(1)));
        }

        [Test]
        public void RangedInt_Linear_InterpolatesByPosition()
        {
            var value = new RangedInt(0, 10, RangeMode.Linear);

            Assert.AreEqual(0, value.Resolve(0f, new System.Random(1)));
            Assert.AreEqual(5, value.Resolve(0.5f, new System.Random(1)));
            Assert.AreEqual(10, value.Resolve(1f, new System.Random(1)));
        }

        [Test]
        public void RangedInt_Linear_ClampsPositionOutsideZeroOne()
        {
            var value = new RangedInt(0, 10, RangeMode.Linear);

            Assert.AreEqual(0, value.Resolve(-1f, new System.Random(1)));
            Assert.AreEqual(10, value.Resolve(2f, new System.Random(1)));
        }

        [Test]
        public void RangedInt_Random_StaysWithinBoundsAndIsSeededDeterministic()
        {
            var value = new RangedInt(1, 5, RangeMode.Random);

            var a = value.Resolve(0.5f, new System.Random(42));
            var b = value.Resolve(0.5f, new System.Random(42));

            Assert.AreEqual(a, b);
            Assert.GreaterOrEqual(a, 1);
            Assert.LessOrEqual(a, 5);
        }

        [Test]
        public void RangedFloat_Linear_InterpolatesByPosition()
        {
            var value = new RangedFloat(0f, 10f, RangeMode.Linear);

            Assert.AreEqual(2.5f, value.Resolve(0.25f, new System.Random(1)), 0.0001f);
        }

        [Test]
        public void RangedFloat_Random_StaysWithinBounds()
        {
            var value = new RangedFloat(2f, 4f, RangeMode.Random);

            var result = value.Resolve(0.5f, new System.Random(7));

            Assert.GreaterOrEqual(result, 2f);
            Assert.LessOrEqual(result, 4f);
        }
    }
}
