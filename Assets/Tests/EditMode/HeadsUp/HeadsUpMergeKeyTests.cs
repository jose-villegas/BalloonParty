using BalloonParty.Game.HeadsUp;
using NUnit.Framework;

namespace BalloonParty.Tests.HeadsUp
{
    /// <summary>
    ///     When two heads-ups are the same line: the same owner (the very same ball, not an equal one) and the same
    ///     channel, spelled exactly. A request with no key is never the same as any line.
    /// </summary>
    [TestFixture]
    public class HeadsUpMergeKeyTests
    {
        private static readonly object Ball = new();

        [Test]
        public void NoKey_MatchesNothing_NotEvenNoKey()
        {
            Assert.IsFalse(default(HeadsUpMergeKey).Equals(default(HeadsUpMergeKey)));
            Assert.IsFalse(default(HeadsUpMergeKey).Equals(new HeadsUpMergeKey(null, null)));
        }

        [Test]
        public void TheSameOwnerAndChannel_Match_WithEqualHashes()
        {
            var a = new HeadsUpMergeKey(Ball, "Red");
            var b = new HeadsUpMergeKey(Ball, "Red");

            Assert.IsTrue(a.Equals(b));
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Test]
        public void AnEqualButDistinctOwner_DoesNotMatch()
        {
            var owner = new string('a', 3);
            var twin = new string('a', 3);

            Assert.IsFalse(new HeadsUpMergeKey(owner, "Red").Equals(new HeadsUpMergeKey(twin, "Red")));
        }

        [Test]
        public void ChannelsCompareExactly()
        {
            Assert.IsFalse(new HeadsUpMergeKey(Ball, "Red").Equals(new HeadsUpMergeKey(Ball, "red")));
            Assert.IsFalse(new HeadsUpMergeKey(Ball, null).Equals(new HeadsUpMergeKey(Ball, string.Empty)));
            Assert.IsTrue(new HeadsUpMergeKey(null, null).Equals(new HeadsUpMergeKey(null, null)));
        }

        [Test]
        public void Equals_AnythingElse_IsFalse()
        {
            Assert.IsFalse(new HeadsUpMergeKey(Ball, "Red").Equals((object)"Red"));
        }
    }
}
