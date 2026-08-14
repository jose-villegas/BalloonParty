using BalloonParty.Item;
using BalloonParty.Item.Preview;
using NUnit.Framework;

namespace BalloonParty.Tests.Item
{
    // HoldLoopMayRebloom is internal static and carries no grid/ticker/DI dependency, so it is exercised
    // directly here rather than through the controller's LateTick machinery — mirroring
    // LaserItemRotationTests' approach to IsDwelling/DwellDuration. It is the two-full-redraws comparison
    // described in the Item README ("A hold-driven re-bloom only takes over...") that decides whether a
    // spinning host's hold loop or its own rotation is what re-blooms it.
    [TestFixture]
    public class ItemRangePreviewControllerTests
    {
        // BloomDuration 1 + RebloomHoldSeconds 0.65, the authored defaults that motivated this rule.
        private const float OneCycleSeconds = 1.65f;

        [Test]
        public void HoldLoopMayRebloom_NonSpinningHost_ReturnsTrue()
        {
            // No spin, no dwell to measure against — the hold loop stays the only cadence, exactly as
            // before this rule existed.
            Assert.IsTrue(ItemRangePreviewController.HoldLoopMayRebloom(null, OneCycleSeconds));
        }

        [Test]
        public void HoldLoopMayRebloom_DwellExactlyTwoCycles_ReturnsTrue()
        {
            // The boundary belongs to the "may re-bloom" side (dwell >= 2 * oneCycleSeconds), not the
            // suppressed side.
            var spinning = new FakeSpinningItemVisual(OneCycleSeconds * 2f);
            Assert.IsTrue(ItemRangePreviewController.HoldLoopMayRebloom(spinning, OneCycleSeconds));
        }

        [Test]
        public void HoldLoopMayRebloom_DwellJustUnderTwoCycles_ReturnsFalse()
        {
            var spinning = new FakeSpinningItemVisual((OneCycleSeconds * 2f) - 0.01f);
            Assert.IsFalse(ItemRangePreviewController.HoldLoopMayRebloom(spinning, OneCycleSeconds));
        }

        // The Laser's own authored numbers: a 1.5s step dwells for 1.125s (stepSeconds * 0.75), well short
        // of even one 1.65s cycle, let alone two — this is the exact case the fix targets.
        [Test]
        public void HoldLoopMayRebloom_LaserDefaultDwell_ReturnsFalse()
        {
            var spinning = new FakeSpinningItemVisual(1.125f);
            Assert.IsFalse(ItemRangePreviewController.HoldLoopMayRebloom(spinning, OneCycleSeconds));
        }

        [Test]
        public void HoldLoopMayRebloom_DwellWellPastTwoCycles_ReturnsTrue()
        {
            var spinning = new FakeSpinningItemVisual(OneCycleSeconds * 5f);
            Assert.IsTrue(ItemRangePreviewController.HoldLoopMayRebloom(spinning, OneCycleSeconds));
        }

        // TurnTimerMayAdvance is the same pure-static shape as HoldLoopMayRebloom and carries the fix for
        // the bug this covers: a one-host sequence's turn-timer "advance" wraps straight back to itself
        // (AdvanceSequence's own mod arithmetic), which is a re-bloom in place in every way that matters —
        // it must answer to the very same dwell qualification the rotation's own re-bloom-in-place branch
        // does, not bypass it just because it arrived via the turn timer instead.
        [Test]
        public void TurnTimerMayAdvance_MultiHost_IgnoresDisqualifiedSpin()
        {
            // A genuine advance to a DIFFERENT host has nothing to do with this host's own rotation, so it
            // is unaffected by HoldLoopMayRebloom regardless of dwell.
            var spinning = new FakeSpinningItemVisual(1.125f);
            Assert.IsTrue(ItemRangePreviewController.TurnTimerMayAdvance(2, spinning, OneCycleSeconds));
        }

        [Test]
        public void TurnTimerMayAdvance_SingleHost_DisqualifiedSpin_ReturnsFalse()
        {
            // The exact reported bug: a single sighted Laser whose dwell can't hold two full redraws must
            // not re-bloom on the turn timer — only the rotation's own falling edge may draw it again.
            var spinning = new FakeSpinningItemVisual(1.125f);
            Assert.IsFalse(ItemRangePreviewController.TurnTimerMayAdvance(1, spinning, OneCycleSeconds));
        }

        [Test]
        public void TurnTimerMayAdvance_SingleHost_QualifiedSpin_ReturnsTrue()
        {
            var spinning = new FakeSpinningItemVisual(OneCycleSeconds * 5f);
            Assert.IsTrue(ItemRangePreviewController.TurnTimerMayAdvance(1, spinning, OneCycleSeconds));
        }

        [Test]
        public void TurnTimerMayAdvance_SingleHost_NonSpinning_ReturnsTrue()
        {
            // No spin, no dwell to measure — the hold loop stays the only cadence, exactly as a
            // non-spinning host's turn advance always has.
            Assert.IsTrue(ItemRangePreviewController.TurnTimerMayAdvance(1, null, OneCycleSeconds));
        }

        // FullHoldMayAdvance is the fix for the reported desync: a wall-clock elapsed test alone can't
        // tell a figure that was genuinely held for its full RebloomHoldSeconds apart from one whose every
        // cycle got cut short by RequestEarlyCycleEnd right as the elapsed budget happened to run out — so
        // an advance now additionally requires a latched "completed a full hold at least once" flag,
        // falling back to a generous starvation multiple of oneCycleSeconds so a host that can never
        // legitimately earn that flag (a badly mistuned config) doesn't hold the sequence forever.
        [Test]
        public void FullHoldMayAdvance_CompletedFullHold_ReturnsTrueRegardlessOfElapsed()
        {
            Assert.IsTrue(ItemRangePreviewController.FullHoldMayAdvance(true, 0f, OneCycleSeconds));
        }

        [Test]
        public void FullHoldMayAdvance_NeverCompleted_BelowStarvationBound_ReturnsFalse()
        {
            // Just under the 4x-oneCycleSeconds starvation bound, with no full hold ever recorded — the
            // exact desync this fix closes: the elapsed budget alone used to be enough to advance here.
            var elapsed = (OneCycleSeconds * 4f) - 0.01f;
            Assert.IsFalse(ItemRangePreviewController.FullHoldMayAdvance(false, elapsed, OneCycleSeconds));
        }

        [Test]
        public void FullHoldMayAdvance_NeverCompleted_AtStarvationBound_ReturnsTrue()
        {
            // The boundary belongs to the "may advance" side, mirroring HoldLoopMayRebloom's own >=
            // boundary convention.
            var elapsed = OneCycleSeconds * 4f;
            Assert.IsTrue(ItemRangePreviewController.FullHoldMayAdvance(false, elapsed, OneCycleSeconds));
        }

        [Test]
        public void FullHoldMayAdvance_NeverCompleted_WellPastStarvationBound_ReturnsTrue()
        {
            var elapsed = OneCycleSeconds * 20f;
            Assert.IsTrue(ItemRangePreviewController.FullHoldMayAdvance(false, elapsed, OneCycleSeconds));
        }

        private sealed class FakeSpinningItemVisual : ISpinningItemVisual
        {
            public float AngleDegrees => 0f;

            public float SpinDegreesPerSecond => 0f;

            public bool IsSettled => true;

            public float DwellSeconds { get; }

            public FakeSpinningItemVisual(float dwellSeconds)
            {
                DwellSeconds = dwellSeconds;
            }
        }
    }
}
