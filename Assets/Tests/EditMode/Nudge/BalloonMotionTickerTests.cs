using BalloonParty.Balloon.Controller;
using BalloonParty.Balloon.View;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Nudge
{
    [TestFixture]
    public class BalloonMotionTickerTests
    {
        // Bitwise-exact: the ticker's completion branch writes `view.Position = state.BasePosition`
        // directly (no offset addition), so a base that was never externally moved comes back exact.
        private const float Exact = 0f;
        private const float Tight = 1e-4f;

        private BalloonMotionTicker _ticker;

        [SetUp]
        public void SetUp()
        {
            _ticker = new BalloonMotionTicker();
        }

        [Test]
        public void SingleImpulse_DisplacesMidFlightAndReturnsExactlyToBase()
        {
            var origin = new Vector3(1f, 2f, 3f);
            var fake = new FakeMotionView { Position = origin };
            var offset = new Vector3(1f, 0f, 0f);

            _ticker.AddImpulse(fake, offset, 1f);

            _ticker.Advance(0.25f);
            Assert.AreNotEqual(origin, fake.Position, "Impulse should displace the view mid-flight.");

            _ticker.Advance(0.25f);
            _ticker.Advance(0.25f);
            _ticker.Advance(0.25f); // elapsed hits duration exactly (4 * 0.25 == 1.0)

            AssertVector3(origin, fake.Position, Exact);

            // State was released on completion — further advances must not move the view.
            _ticker.Advance(1f);
            AssertVector3(origin, fake.Position, Exact);
        }

        [Test]
        public void SingleImpulse_FirstSmallAdvance_NoFirstFrameSnap()
        {
            var origin = Vector3.zero;
            var fake = new FakeMotionView { Position = origin };
            var offset = new Vector3(10f, 0f, 0f);
            const float duration = 1f;
            const float dt = 0.02f;

            _ticker.AddImpulse(fake, offset, duration);
            _ticker.Advance(dt);

            var expected = offset * Reach(dt / duration);
            AssertVector3(expected, fake.Position - origin, Tight);

            // The spirit of the guarantee: an early tick moves a little, never jumps far.
            Assert.Less((fake.Position - origin).magnitude, offset.magnitude * 0.5f);
        }

        [Test]
        public void StackedImpulses_DisplacementIsTheSumAndEachCompletesOnItsOwnTimeline()
        {
            var origin = Vector3.zero;
            var fake = new FakeMotionView { Position = origin };
            var offsetA = new Vector3(1f, 0f, 0f);
            var offsetB = new Vector3(0f, 1f, 0f);

            _ticker.AddImpulse(fake, offsetA, 1f);
            _ticker.Advance(0.3f); // impulse A: elapsed 0.3

            _ticker.AddImpulse(fake, offsetB, 0.4f);
            _ticker.Advance(0.1f); // A: elapsed 0.4, B: elapsed 0.1 — both active

            var expectedBoth = offsetA * Reach(0.4f) + offsetB * Reach(0.1f / 0.4f);
            AssertVector3(expectedBoth, fake.Position - origin, Tight);

            _ticker.Advance(0.3f); // B completes here (elapsed 0.4 == duration); A: elapsed 0.7

            var expectedAOnly = offsetA * Reach(0.7f);
            AssertVector3(expectedAOnly, fake.Position - origin, Tight);

            _ticker.Advance(0.3f); // A completes (elapsed 1.0)
            AssertVector3(origin, fake.Position, Exact);
        }

        [Test]
        public void StackedImpulses_AfterAllComplete_ReturnsExactlyToOriginalBase()
        {
            var origin = new Vector3(-2f, 5f, 0.5f);
            var fake = new FakeMotionView { Position = origin };

            _ticker.AddImpulse(fake, new Vector3(3f, 0f, 0f), 0.5f);
            _ticker.AddImpulse(fake, new Vector3(0f, -3f, 0f), 0.2f);

            _ticker.Advance(0.2f);
            _ticker.Advance(0.3f);
            _ticker.Advance(1f); // comfortably past both durations

            AssertVector3(origin, fake.Position, Exact);
        }

        [Test]
        public void ExternalBaseWrite_MidImpulse_IsAdoptedAndFinalPositionIsTheNewBase()
        {
            var origin = Vector3.zero;
            var fake = new FakeMotionView { Position = origin };
            var newBase = new Vector3(10f, 10f, 0f);

            _ticker.AddImpulse(fake, new Vector3(1f, 0f, 0f), 1f);
            _ticker.Advance(0.3f);

            // Simulate a DOPath tween writing the transform directly between late ticks.
            fake.Position = newBase;

            _ticker.Advance(0.3f);
            _ticker.Advance(0.4f); // completes (elapsed 1.0)

            AssertVector3(newBase, fake.Position, Exact);
        }

        [Test]
        public void ContinuousBaseWrites_BalanceSlide_FinalPositionIsTheLastWrittenBase()
        {
            var fake = new FakeMotionView { Position = Vector3.zero };

            // Duration deliberately doesn't land on a whole multiple of dt: completion (elapsed 0.6
            // after 6 ticks) lands on the SAME tick as the last external write, so this exercises the
            // interesting case — adoption and completion happening together — not just a trailing tick
            // where the ticker has nothing left to do.
            _ticker.AddImpulse(fake, new Vector3(0.5f, 0f, 0f), 0.55f);

            var lastBase = Vector3.zero;

            // Write a new base before every tick, as a balance DOPath would every frame.
            for (var i = 1; i <= 6; i++)
            {
                lastBase = new Vector3(i, i * 0.5f, 0f);
                fake.Position = lastBase;
                _ticker.Advance(0.1f);
            }

            AssertVector3(lastBase, fake.Position, Exact);
        }

        [Test]
        public void CancelAll_MidImpulse_StopsWritesAndViewAcceptsFreshImpulse()
        {
            var origin = Vector3.zero;
            var fake = new FakeMotionView { Position = origin };

            _ticker.AddImpulse(fake, new Vector3(1f, 0f, 0f), 1f);
            _ticker.Advance(0.3f);
            var stoppedAt = fake.Position;
            Assert.AreNotEqual(origin, stoppedAt);

            _ticker.CancelAll(fake);
            _ticker.Advance(0.3f);
            AssertVector3(stoppedAt, fake.Position, Exact);

            // A fresh impulse from wherever the view was left behaves like a brand-new one:
            // its base is seeded from the view's current position.
            _ticker.AddImpulse(fake, new Vector3(0f, 1f, 0f), 0.5f);
            _ticker.Advance(0.5f);
            AssertVector3(stoppedAt, fake.Position, Exact);
        }

        [Test]
        public void ImpulseCap_NinthImpulse_ReplacesTheOneClosestToCompletionAndStaysStable()
        {
            var origin = Vector3.zero;
            var fake = new FakeMotionView { Position = origin };

            // Impulse 0 (added first, so oldest/highest-progress once the loop finishes) carries a
            // huge offset so overwrite-vs-not is unmistakable in the resulting displacement. The
            // other seven carry zero offset so they can never contaminate the sum either way.
            var hugeOffset = new Vector3(100f, 0f, 0f);
            _ticker.AddImpulse(fake, hugeOffset, 1f);
            _ticker.Advance(0.01f);

            for (var i = 1; i < 8; i++)
            {
                _ticker.AddImpulse(fake, Vector3.zero, 1f);
                _ticker.Advance(0.01f);
            }

            // The cap (8) is already reached — this 9th impulse must overwrite the most-complete
            // one (impulse 0, the huge offset), not append a 9th slot.
            var replacementOffset = new Vector3(5f, 0f, 0f);
            _ticker.AddImpulse(fake, replacementOffset, 1f);
            _ticker.Advance(0.01f);

            // Every surviving old impulse carries Vector3.zero, so the only possible non-zero
            // contribution is the replacement — unless the huge offset survived the cap.
            var expected = replacementOffset * Reach(0.01f);
            AssertVector3(expected, fake.Position - origin, Tight);
            Assert.Less(fake.Position.magnitude, 3f, "The 100-magnitude offset must not have survived the cap.");

            _ticker.Advance(1.5f); // comfortably finishes every remaining impulse
            AssertVector3(origin, fake.Position, Exact);
        }

        [Test]
        public void DegenerateDuration_ZeroOrNegative_ClampsAndCompletesWithoutNaN()
        {
            var origin = Vector3.zero;
            var zeroFake = new FakeMotionView { Position = origin };
            var negativeFake = new FakeMotionView { Position = origin };

            _ticker.AddImpulse(zeroFake, new Vector3(1f, 0f, 0f), 0f);
            _ticker.AddImpulse(negativeFake, new Vector3(1f, 0f, 0f), -5f);

            _ticker.Advance(0.0002f); // exceeds the clamped 0.0001s duration — completes immediately

            AssertNoNaN(zeroFake.Position);
            AssertNoNaN(negativeFake.Position);
            AssertVector3(origin, zeroFake.Position, Exact);
            AssertVector3(origin, negativeFake.Position, Exact);
        }

        [Test]
        public void MultipleViews_ConcurrentImpulses_DoNotInterfere()
        {
            var originA = Vector3.zero;
            var originB = new Vector3(5f, 0f, 0f);
            var fakeA = new FakeMotionView { Position = originA };
            var fakeB = new FakeMotionView { Position = originB };

            _ticker.AddImpulse(fakeA, new Vector3(1f, 0f, 0f), 1f);
            _ticker.AddImpulse(fakeB, new Vector3(0f, 2f, 0f), 0.4f);

            _ticker.Advance(0.2f);

            var expectedA = new Vector3(1f, 0f, 0f) * Reach(0.2f);
            var expectedB = new Vector3(0f, 2f, 0f) * Reach(0.5f);
            AssertVector3(expectedA, fakeA.Position - originA, Tight);
            AssertVector3(expectedB, fakeB.Position - originB, Tight);

            _ticker.Advance(0.2f); // B completes (elapsed 0.4); A: elapsed 0.4
            AssertVector3(originB, fakeB.Position, Exact);
            Assert.AreNotEqual(originA, fakeA.Position, "A must still be mid-flight, unaffected by B completing.");

            _ticker.Advance(1f);
            AssertVector3(originA, fakeA.Position, Exact);
        }

        [Test]
        public void PoolReuse_RepeatedCompleteCycles_ContinueToBehaveCorrectly()
        {
            var origin = Vector3.zero;
            var fake = new FakeMotionView { Position = origin };

            // Drives many add/complete cycles through the same view so its NudgeState is released
            // back to the pool and popped again repeatedly (Task 2's pool-reuse guarantee). The pool
            // field itself is private, so this asserts the observable behavior stays correct across
            // reuse rather than the pool's internal size.
            for (var i = 0; i < 20; i++)
            {
                _ticker.AddImpulse(fake, new Vector3(1f, 1f, 0f), 0.2f);
                _ticker.Advance(0.1f);
                Assert.AreNotEqual(origin, fake.Position, $"cycle {i}: should be displaced mid-flight");

                _ticker.Advance(0.2f);
                AssertVector3(origin, fake.Position, Exact, $"cycle {i}: should return exactly to base");
            }
        }

        private static float Reach(float progress)
        {
            return progress < 0.5f
                ? EaseOutQuad(progress * 2f)
                : 1f - EaseOutQuad((progress - 0.5f) * 2f);
        }

        private static float EaseOutQuad(float t)
        {
            return 1f - (1f - t) * (1f - t);
        }

        private static void AssertVector3(Vector3 expected, Vector3 actual, float tolerance, string message = null)
        {
            Assert.AreEqual(expected.x, actual.x, tolerance, $"{message} (x)");
            Assert.AreEqual(expected.y, actual.y, tolerance, $"{message} (y)");
            Assert.AreEqual(expected.z, actual.z, tolerance, $"{message} (z)");
        }

        private static void AssertNoNaN(Vector3 v)
        {
            Assert.IsFalse(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z), $"NaN in {v}");
        }

        private sealed class FakeMotionView : IBalloonMotionView
        {
            public Vector3 Position { get; set; }
        }
    }
}
