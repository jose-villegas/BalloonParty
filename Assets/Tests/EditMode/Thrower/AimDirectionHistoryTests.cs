using BalloonParty.Thrower;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Thrower
{
    [TestFixture]
    public class AimDirectionHistoryTests
    {
        [Test]
        public void TryResolve_EmptyHistory_ReturnsFalse()
        {
            var history = new AimDirectionHistory(4);

            var resolved = history.TryResolve(1f, out var direction);

            Assert.IsFalse(resolved);
            Assert.AreEqual(Vector3.zero, direction);
        }

        [Test]
        public void TryResolve_SampleExactlyAtBoundary_IsSelected()
        {
            var history = new AimDirectionHistory(4);
            history.Record(1f, Vector3.right);
            history.Record(2f, Vector3.up);

            // targetTime == the older sample's own timestamp: <= must include it, not just <.
            var resolved = history.TryResolve(1f, out var direction);

            Assert.IsTrue(resolved);
            Assert.AreEqual(Vector3.right, direction);
        }

        [Test]
        public void TryResolve_LatchOfZero_ReturnsNewestSample()
        {
            var history = new AimDirectionHistory(4);
            history.Record(1f, Vector3.right);
            history.Record(2f, Vector3.up);
            history.Record(3f, Vector3.left);

            // targetTime == now (no latch): the newest sample is the correct, live-equivalent pick.
            var resolved = history.TryResolve(3f, out var direction);

            Assert.IsTrue(resolved);
            Assert.AreEqual(Vector3.left, direction);
        }

        [Test]
        public void TryResolve_LatchLongerThanHistory_ReturnsOldestSample()
        {
            var history = new AimDirectionHistory(4);
            history.Record(5f, Vector3.right);
            history.Record(6f, Vector3.up);
            history.Record(7f, Vector3.left);

            // targetTime predates every recorded sample — fall back to the oldest one held rather
            // than failing.
            var resolved = history.TryResolve(0f, out var direction);

            Assert.IsTrue(resolved);
            Assert.AreEqual(Vector3.right, direction);
        }

        [Test]
        public void TryResolve_PicksNewestSampleAtOrBeforeTarget()
        {
            var history = new AimDirectionHistory(4);
            history.Record(1f, Vector3.right);
            history.Record(2f, Vector3.up);
            history.Record(3f, Vector3.left);

            var resolved = history.TryResolve(2.5f, out var direction);

            Assert.IsTrue(resolved);
            Assert.AreEqual(Vector3.up, direction);
        }

        [Test]
        public void Record_BeyondCapacity_OverwritesOldestAndKeepsResolvingCorrectly()
        {
            var history = new AimDirectionHistory(2);
            history.Record(1f, Vector3.right);
            history.Record(2f, Vector3.up);
            // Capacity is 2 — this overwrites the t=1 sample, which must no longer be resolvable.
            history.Record(3f, Vector3.left);

            var resolved = history.TryResolve(0f, out var direction);

            Assert.IsTrue(resolved);
            Assert.AreEqual(Vector3.up, direction);
        }

        [Test]
        public void Clear_EmptiesHistory()
        {
            var history = new AimDirectionHistory(4);
            history.Record(1f, Vector3.right);

            history.Clear();
            var resolved = history.TryResolve(1f, out _);

            Assert.IsFalse(resolved);
        }
    }
}
