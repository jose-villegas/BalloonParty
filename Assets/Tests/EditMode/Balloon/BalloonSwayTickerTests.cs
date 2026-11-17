using System.Collections.Generic;
using BalloonParty.Balloon.Controller;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration.Balloons;
using NSubstitute;
using NUnit.Framework;

namespace BalloonParty.Tests.Balloon
{
    /// <summary>
    ///     The sway ticker's bookkeeping: each view keeps its own playback through other views coming and going,
    ///     a view that ends keeps its last pose, and one that begins again (a pooled balloon reused) starts over.
    ///     On <see cref="BalloonSwayPlaybackTests" />' straight-line loops: unstable reads 100 + 10 a second, stable
    ///     10 a second. A sway that begins is at its loop's start on its first pass, which is why most of these
    ///     start with a pass of no time.
    /// </summary>
    [TestFixture]
    public class BalloonSwayTickerTests
    {
        private const float Tight = 1e-4f;
        private const float Mixed = 1e-3f;

        private BalloonSwayTicker _ticker;

        [SetUp]
        public void SetUp()
        {
            var config = Substitute.For<IBalloonsConfiguration>();
            config.IdleSway.Returns(BalloonSwayPlaybackTests.LinearSettings());
            _ticker = new BalloonSwayTicker(config);
        }

        [Test]
        public void EachView_SwaysFromItsOwnStart()
        {
            var early = new FakeSwayView();
            var late = new FakeSwayView();

            _ticker.Begin(early);
            _ticker.Advance(0f);
            _ticker.Advance(1f);
            _ticker.Begin(late);
            _ticker.Advance(0f);
            _ticker.Advance(0.5f);

            Assert.AreEqual(115f, early.Angle, Tight, "1.5 s into the unstable loop");
            Assert.AreEqual(105f, late.Angle, Tight, "0.5 s into its own");
        }

        [Test]
        public void SetStable_SwitchesOnlyThatView()
        {
            var switched = new FakeSwayView();
            var other = new FakeSwayView();
            _ticker.Begin(switched);
            _ticker.Begin(other);
            _ticker.Advance(0f);
            _ticker.Advance(1f);

            _ticker.SetStable(switched, true);
            _ticker.Advance(0.25f);
            _ticker.Advance(0.25f);

            Assert.AreEqual(BalloonSwayPlayback.Mix(115f, 2.5f, 0.25f), switched.Angle, Mixed,
                "a quarter through the crossfade to stable");
            Assert.AreEqual(115f, other.Angle, Tight, "still on the unstable loop");
        }

        [Test]
        public void End_StopsWriting_AndLeavesTheLastPose()
        {
            var view = new FakeSwayView();
            _ticker.Begin(view);
            _ticker.Advance(0f);
            _ticker.Advance(1f);
            var writes = view.Writes;

            _ticker.End(view);
            _ticker.Advance(1f);

            Assert.AreEqual(110f, view.Angle, Tight);
            Assert.AreEqual(writes, view.Writes, "an ended view is the cinematic's to turn");
        }

        [Test]
        public void End_LeavesTheOthersOnTheirOwnPlayback()
        {
            var first = new FakeSwayView();
            var middle = new FakeSwayView();
            var last = new FakeSwayView();
            _ticker.Begin(first);
            _ticker.Begin(middle);
            _ticker.Advance(0f);
            _ticker.Advance(1f);
            _ticker.Begin(last);
            _ticker.SetStable(last, true);

            // Ending the first moves the last into its place; each must keep its own playback and flag.
            _ticker.End(first);
            _ticker.Advance(0f);
            _ticker.Advance(0.5f);

            Assert.AreEqual(115f, middle.Angle, Tight, "1.5 s into the unstable loop");
            Assert.AreEqual(5f, last.Angle, Tight, "0.5 s into the stable loop");

            _ticker.SetStable(middle, true);
            _ticker.End(last);
            _ticker.Advance(0.25f);
            _ticker.Advance(0.25f);

            Assert.AreEqual(BalloonSwayPlayback.Mix(120f, 2.5f, 0.25f), middle.Angle, Mixed,
                "a quarter through its crossfade, unstable at 2 s and stable at 0.25 s");
            Assert.AreEqual(5f, last.Angle, Tight);
        }

        [Test]
        public void BeginAgain_StartsOver_WithNoCrossfadeOrFlagLeftFromBefore()
        {
            var view = new FakeSwayView();
            _ticker.Begin(view);
            _ticker.Advance(0f);
            _ticker.Advance(1f);
            _ticker.SetStable(view, true);
            _ticker.Advance(0.25f);
            _ticker.Advance(0.25f);
            Assert.IsTrue(view.Angle < 100f, "mid-crossfade toward the stable loop when it despawns");

            // Reused from the pool: the unstable loop from its start, whatever it was doing before.
            _ticker.End(view);
            _ticker.Begin(view);
            _ticker.Advance(0.1f);
            Assert.AreEqual(100f, view.Angle, Tight);

            _ticker.Advance(0.1f);
            Assert.AreEqual(101f, view.Angle, Tight, "the unstable loop 0.1 s in: not stable, no crossfade");
        }

        [Test]
        public void BeginOnASwayingView_StartsItOver()
        {
            var view = new FakeSwayView();
            _ticker.Begin(view);
            _ticker.SetStable(view, true);
            _ticker.Advance(0f);
            _ticker.Advance(3f);

            _ticker.Begin(view);
            _ticker.Advance(0.1f);
            Assert.AreEqual(100f, view.Angle, Tight, "the unstable loop from its start");

            _ticker.Advance(0.1f);
            Assert.AreEqual(101f, view.Angle, Tight);
            Assert.AreEqual(4, view.Writes, "one entry, not two");
        }

        [Test]
        public void SetStable_IsIgnoredForAViewThatIsNotSwaying()
        {
            var view = new FakeSwayView();

            _ticker.SetStable(view, true);
            _ticker.Advance(1f);
            Assert.AreEqual(0, view.Writes);

            // The flag wasn't kept for later: a sway that begins starts unstable.
            _ticker.Begin(view);
            _ticker.Advance(0f);
            _ticker.Advance(1f);
            Assert.AreEqual(110f, view.Angle, Tight);
        }

        [Test]
        public void EndOnAViewThatIsNotSwaying_DoesNothing()
        {
            var swaying = new FakeSwayView();
            _ticker.Begin(swaying);

            _ticker.End(new FakeSwayView());
            _ticker.Advance(0f);
            _ticker.Advance(1f);

            Assert.AreEqual(110f, swaying.Angle, Tight);
        }

        [Test]
        public void MoreViewsThanTheStartingCapacity_AllSway()
        {
            var views = new List<FakeSwayView>();
            for (var i = 0; i < 200; i++)
            {
                var view = new FakeSwayView();
                views.Add(view);
                _ticker.Begin(view);
            }

            _ticker.Advance(0f);
            _ticker.Advance(1f);

            foreach (var view in views)
            {
                Assert.AreEqual(110f, view.Angle, Tight);
                Assert.AreEqual(2, view.Writes);
            }
        }

        private sealed class FakeSwayView : IBalloonSwayView
        {
            public float Angle { get; private set; }
            public int Writes { get; private set; }

            public void ApplySway(float degrees)
            {
                Angle = degrees;
                Writes++;
            }
        }
    }
}
