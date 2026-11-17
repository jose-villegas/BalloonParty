using BalloonParty.Shared.Math;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Shared
{
    /// <summary>
    ///     Tests <see cref="SpringChain" />, the tough snake's springs: its ends stay pinned where they're put, it
    ///     sags with slack, and it stretches past its rest length when its ends part.
    /// </summary>
    [TestFixture]
    public class SpringChainTests
    {
        private static readonly SpringChainTuning Tuning = new(0.6f, 8f, 0.03f, 1f / 120f, 4);

        [Test]
        public void ItsEnds_SitOnTheEndsItWasGiven()
        {
            var chain = new SpringChain(8);
            chain.Place(Vector3.zero, Vector3.right);

            Settle(chain, new Vector3(0f, 1f, 0f), new Vector3(1.5f, 0.5f, 0f), 1f);

            Assert.AreEqual(9, chain.Points);
            Assert.That(Vector3.Distance(chain.Point(0), new Vector3(0f, 1f, 0f)), Is.LessThan(1e-4f));
            Assert.That(Vector3.Distance(chain.Point(8), new Vector3(1.5f, 0.5f, 0f)), Is.LessThan(1e-4f));
        }

        [Test]
        public void WithSlack_ItSags_BelowItsEnds()
        {
            var chain = new SpringChain(8);
            chain.Place(Vector3.zero, Vector3.right);

            Settle(chain, Vector3.zero, Vector3.right, 1.3f);

            Assert.That(chain.Point(4).y, Is.LessThan(-0.1f));
        }

        [Test]
        public void ItsEndsParting_StretchItPastItsRestLength_AndItHangsStraighter()
        {
            var chain = new SpringChain(8);
            chain.Place(Vector3.zero, Vector3.right);
            Settle(chain, Vector3.zero, Vector3.right, 1.3f);
            var sag = chain.Point(4).y;

            Settle(chain, Vector3.zero, new Vector3(2f, 0f, 0f), 1.3f);

            Assert.That(Length(chain), Is.GreaterThan(1.3f));
            Assert.That(chain.Point(4).y, Is.GreaterThan(sag));
        }

        [Test]
        public void LessThanAStep_MovesNothing()
        {
            var chain = new SpringChain(4);
            chain.Place(Vector3.zero, Vector3.right);

            chain.Step(Vector3.up, Vector3.right + Vector3.up, 1f, 0.001f, Tuning);

            Assert.AreEqual(Vector3.zero, chain.Point(0));
        }

        private static void Settle(SpringChain chain, Vector3 from, Vector3 to, float rest)
        {
            for (var i = 0; i < 300; i++)
            {
                chain.Step(from, to, rest, 1f / 60f, Tuning);
            }
        }

        private static float Length(SpringChain chain)
        {
            var length = 0f;
            for (var i = 1; i < chain.Points; i++)
            {
                length += Vector3.Distance(chain.Point(i - 1), chain.Point(i));
            }

            return length;
        }
    }
}
