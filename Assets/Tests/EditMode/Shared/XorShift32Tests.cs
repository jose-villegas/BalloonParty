using BalloonParty.Shared;
using NUnit.Framework;

namespace BalloonParty.Tests.Shared
{
    /// <summary>
    ///     The seeded generator behind the mushroom layout (<see cref="XorShift32" />). Its sequences are pinned
    ///     here number by number: they were worked out apart from the game, in plain integer arithmetic, so a device
    ///     or runtime that disagrees fails this test instead of quietly laying out another board.
    /// </summary>
    [TestFixture]
    public class XorShift32Tests
    {
        [Test]
        public void NextUInt_Seed1_IsThePinnedSequence()
        {
            var random = new XorShift32(1);

            Assert.AreEqual(524866043u, random.NextUInt());
            Assert.AreEqual(2877414208u, random.NextUInt());
            Assert.AreEqual(2380002740u, random.NextUInt());
            Assert.AreEqual(2664205378u, random.NextUInt());
            Assert.AreEqual(3890067424u, random.NextUInt());
        }

        [Test]
        public void NextUInt_SeedZero_StillRuns()
        {
            var random = new XorShift32(0);

            Assert.AreEqual(1359758873u, random.NextUInt());
            Assert.AreEqual(3761132862u, random.NextUInt());
        }

        [Test]
        public void NextUInt_NegativeSeed_IsThePinnedSequence()
        {
            var random = new XorShift32(-1);

            Assert.AreEqual(785727346u, random.NextUInt());
            Assert.AreEqual(2925282831u, random.NextUInt());
        }

        [Test]
        public void Next_ScalesTheSequenceIntoTheRange()
        {
            var random = new XorShift32(20261119);
            var rolls = new int[8];
            for (var i = 0; i < rolls.Length; i++)
            {
                rolls[i] = random.Next(10);
            }

            CollectionAssert.AreEqual(new[] { 6, 2, 1, 4, 0, 0, 3, 3 }, rolls);
        }

        [Test]
        public void Next_NeverLeavesTheRange()
        {
            var random = new XorShift32(7);
            for (var i = 0; i < 10000; i++)
            {
                Assert.That(random.Next(7), Is.InRange(0, 6));
            }
        }

        [TestCase(1)]
        [TestCase(0)]
        [TestCase(-5)]
        public void Next_ARangeOfOneOrLess_IsZero(int exclusiveMax)
        {
            var random = new XorShift32(3);

            Assert.AreEqual(0, random.Next(exclusiveMax));
        }

        [Test]
        public void SameSeed_SameSequence_DifferentSeeds_Differ()
        {
            var first = new XorShift32(42);
            var second = new XorShift32(42);
            var other = new XorShift32(43);
            var differs = false;
            for (var i = 0; i < 100; i++)
            {
                var value = first.NextUInt();
                Assert.AreEqual(value, second.NextUInt());
                differs |= value != other.NextUInt();
            }

            Assert.IsTrue(differs);
        }
    }
}
