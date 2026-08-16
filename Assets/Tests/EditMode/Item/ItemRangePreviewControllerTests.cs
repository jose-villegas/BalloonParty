using BalloonParty.Item;
using BalloonParty.Item.Preview;
using NUnit.Framework;

namespace BalloonParty.Tests.Item
{
    // ResolveTurnCadence is internal static and carries no grid/ticker/DI dependency, so it is exercised
    // directly here rather than through the controller's LateTick machinery — mirroring
    // LaserItemRotationTests' approach to IsDwelling/DwellDuration. It consolidates what used to be three
    // separately-tested predicates (HoldLoopMayRebloom/TurnTimerMayAdvance/FullHoldMayAdvance); each case
    // below is the same scenario those three used to cover, translated to the outcome ResolveTurnCadence
    // would now return for the same inputs. Arguments are positional, in ResolveTurnCadence's own
    // declared order (holdLoopEnabled, activeHostElapsed, oneCycleSeconds, hostCount, spinning,
    // completedFullHold, earlyCycleEndRequested), since named arguments can't precede the positional
    // OneCycleSeconds constant reused across every call. holdLoopEnabled is true (RebloomHoldSeconds > 0)
    // throughout, matching every one of the old cases, which all implicitly assumed the hold loop was on.
    [TestFixture]
    public class ItemRangePreviewControllerTests
    {
        // BloomDuration 1 + RebloomHoldSeconds 0.65, the authored defaults that motivated this rule.
        private const float OneCycleSeconds = 1.65f;

        // Former HoldLoopMayRebloom cases: activeHostElapsed is 0f, below the turn-timer threshold, so
        // ResolveTurnCadence can never return Advance regardless of qualification — only the
        // HoldLoopMayRebloom-qualifies-or-not choice between Rebloom and Wait is exercised, same as the
        // old direct calls.

        [Test]
        public void ResolveTurnCadence_NonSpinningHost_Reblooms()
        {
            // No spin, no dwell to measure against — the hold loop stays the only cadence, exactly as
            // before this rule existed.
            var outcome = ItemRangePreviewController.ResolveTurnCadence(true, 0f, OneCycleSeconds, 1, null, false, false);
            Assert.AreEqual(ItemRangePreviewController.TurnCadenceOutcome.Rebloom, outcome);
        }

        [Test]
        public void ResolveTurnCadence_DwellExactlyTwoCycles_Reblooms()
        {
            // The boundary belongs to the "may re-bloom" side (dwell >= 2 * oneCycleSeconds), not the
            // suppressed side.
            var spinning = new FakeSpinningItemVisual(OneCycleSeconds * 2f);
            var outcome = ItemRangePreviewController.ResolveTurnCadence(true, 0f, OneCycleSeconds, 1, spinning, false, false);
            Assert.AreEqual(ItemRangePreviewController.TurnCadenceOutcome.Rebloom, outcome);
        }

        [Test]
        public void ResolveTurnCadence_DwellJustUnderTwoCycles_Waits()
        {
            var spinning = new FakeSpinningItemVisual((OneCycleSeconds * 2f) - 0.01f);
            var outcome = ItemRangePreviewController.ResolveTurnCadence(true, 0f, OneCycleSeconds, 1, spinning, false, false);
            Assert.AreEqual(ItemRangePreviewController.TurnCadenceOutcome.Wait, outcome);
        }

        // The Laser's own authored numbers: a 1.5s step dwells for 1.125s (stepSeconds * 0.75), well short
        // of even one 1.65s cycle, let alone two — this is the exact case the fix targets. Disqualified,
        // and with no early-cycle-end request either, this park is left dark and unconsumed.
        [Test]
        public void ResolveTurnCadence_LaserDefaultDwell_NoEarlyRequest_Waits()
        {
            var spinning = new FakeSpinningItemVisual(1.125f);
            var outcome = ItemRangePreviewController.ResolveTurnCadence(true, 0f, OneCycleSeconds, 1, spinning, false, false);
            Assert.AreEqual(ItemRangePreviewController.TurnCadenceOutcome.Wait, outcome);
        }

        // Same disqualified dwell, but the rotation's own falling edge asked for this park — that route
        // still may re-bloom in place regardless of HoldLoopMayRebloom's qualification.
        [Test]
        public void ResolveTurnCadence_LaserDefaultDwell_EarlyRequest_Reblooms()
        {
            var spinning = new FakeSpinningItemVisual(1.125f);
            var outcome = ItemRangePreviewController.ResolveTurnCadence(true, 0f, OneCycleSeconds, 1, spinning, false, true);
            Assert.AreEqual(ItemRangePreviewController.TurnCadenceOutcome.Rebloom, outcome);
        }

        [Test]
        public void ResolveTurnCadence_DwellWellPastTwoCycles_Reblooms()
        {
            var spinning = new FakeSpinningItemVisual(OneCycleSeconds * 5f);
            var outcome = ItemRangePreviewController.ResolveTurnCadence(true, 0f, OneCycleSeconds, 1, spinning, false, false);
            Assert.AreEqual(ItemRangePreviewController.TurnCadenceOutcome.Rebloom, outcome);
        }

        // Former TurnTimerMayAdvance cases: activeHostElapsed >= oneCycleSeconds and completedFullHold true
        // (so the fullHoldMayAdvance half is trivially satisfied and never the reason a case does or
        // doesn't advance) — isolating exactly the turnTimerMayAdvance half ResolveTurnCadence now inlines,
        // same as the old direct TurnTimerMayAdvance calls.

        [Test]
        public void ResolveTurnCadence_MultiHost_IgnoresDisqualifiedSpin_Advances()
        {
            // A genuine advance to a DIFFERENT host has nothing to do with this host's own rotation, so it
            // is unaffected by HoldLoopMayRebloom regardless of dwell.
            var spinning = new FakeSpinningItemVisual(1.125f);
            var outcome =
                ItemRangePreviewController.ResolveTurnCadence(true, OneCycleSeconds, OneCycleSeconds, 2, spinning, true, false);
            Assert.AreEqual(ItemRangePreviewController.TurnCadenceOutcome.Advance, outcome);
        }

        [Test]
        public void ResolveTurnCadence_SingleHost_DisqualifiedSpin_DoesNotAdvance()
        {
            // The exact reported bug: a single sighted Laser whose dwell can't hold two full redraws must
            // not re-bloom on the turn timer — only the rotation's own falling edge may draw it again. With
            // no early request either, this park waits rather than advancing OR re-blooming.
            var spinning = new FakeSpinningItemVisual(1.125f);
            var outcome =
                ItemRangePreviewController.ResolveTurnCadence(true, OneCycleSeconds, OneCycleSeconds, 1, spinning, true, false);
            Assert.AreEqual(ItemRangePreviewController.TurnCadenceOutcome.Wait, outcome);
        }

        [Test]
        public void ResolveTurnCadence_SingleHost_QualifiedSpin_Advances()
        {
            var spinning = new FakeSpinningItemVisual(OneCycleSeconds * 5f);
            var outcome =
                ItemRangePreviewController.ResolveTurnCadence(true, OneCycleSeconds, OneCycleSeconds, 1, spinning, true, false);
            Assert.AreEqual(ItemRangePreviewController.TurnCadenceOutcome.Advance, outcome);
        }

        [Test]
        public void ResolveTurnCadence_SingleHost_NonSpinning_Advances()
        {
            // No spin, no dwell to measure — the hold loop stays the only cadence, exactly as a
            // non-spinning host's turn advance always has.
            var outcome =
                ItemRangePreviewController.ResolveTurnCadence(true, OneCycleSeconds, OneCycleSeconds, 1, null, true, false);
            Assert.AreEqual(ItemRangePreviewController.TurnCadenceOutcome.Advance, outcome);
        }

        // Former FullHoldMayAdvance cases: non-spinning host and a multi-host sequence, so
        // turnTimerMayAdvance is trivially true and never the reason a case does or doesn't advance —
        // isolating exactly the fullHoldMayAdvance half, same as the old direct FullHoldMayAdvance calls.
        // FullHoldMayAdvance is the fix for the reported desync: a wall-clock elapsed test alone can't tell
        // a figure that was genuinely held for its full RebloomHoldSeconds apart from one whose every cycle
        // got cut short by RequestEarlyCycleEnd right as the elapsed budget happened to run out — so an
        // advance now additionally requires a latched "completed a full hold at least once" flag, falling
        // back to a generous starvation multiple of oneCycleSeconds so a host that can never legitimately
        // earn that flag (a badly mistuned config) doesn't hold the sequence forever.

        // The old direct call — FullHoldMayAdvance(true, 0f, OneCycleSeconds) — asserted true even at
        // elapsed 0f, because that predicate alone never looked at the turn-timer threshold. Inside
        // ResolveTurnCadence, activeHostElapsed now also gates the OUTER `>= oneCycleSeconds` check before
        // fullHoldMayAdvance is even consulted, so elapsed 0f can no longer reach Advance regardless of
        // completedFullHold — that outer gate was never part of what FullHoldMayAdvance itself answered.
        // The two cases below instead hold elapsed at (and safely above) the outer floor to isolate the
        // same "completedFullHold makes the starvation multiplier irrelevant" fact the old test targeted.
        [Test]
        public void ResolveTurnCadence_CompletedFullHold_AdvancesAtTurnTimerFloor()
        {
            var outcome =
                ItemRangePreviewController.ResolveTurnCadence(true, OneCycleSeconds, OneCycleSeconds, 2, null, true, false);
            Assert.AreEqual(ItemRangePreviewController.TurnCadenceOutcome.Advance, outcome);
        }

        [Test]
        public void ResolveTurnCadence_CompletedFullHold_AdvancesRegardlessOfElapsedAboveFloor()
        {
            // Far past even the starvation multiplier — completedFullHold alone still satisfies
            // fullHoldMayAdvance without ever needing the starvation fallback.
            var elapsed = OneCycleSeconds * 50f;
            var outcome = ItemRangePreviewController.ResolveTurnCadence(true, elapsed, OneCycleSeconds, 2, null, true, false);
            Assert.AreEqual(ItemRangePreviewController.TurnCadenceOutcome.Advance, outcome);
        }

        [Test]
        public void ResolveTurnCadence_NeverCompleted_BelowStarvationBound_DoesNotAdvance()
        {
            // Just under the 4x-oneCycleSeconds starvation bound, with no full hold ever recorded — the
            // exact desync this fix closes: the elapsed budget alone used to be enough to advance here.
            // Falls to Rebloom rather than Wait because a non-spinning host has no dwell of its own to
            // disqualify it from the hold loop's own cadence, which is exactly where this park came from.
            var elapsed = (OneCycleSeconds * 4f) - 0.01f;
            var outcome = ItemRangePreviewController.ResolveTurnCadence(true, elapsed, OneCycleSeconds, 2, null, false, false);
            Assert.AreEqual(ItemRangePreviewController.TurnCadenceOutcome.Rebloom, outcome);
        }

        [Test]
        public void ResolveTurnCadence_NeverCompleted_AtStarvationBound_Advances()
        {
            // The boundary belongs to the "may advance" side, mirroring HoldLoopMayRebloom's own >=
            // boundary convention.
            var elapsed = OneCycleSeconds * 4f;
            var outcome = ItemRangePreviewController.ResolveTurnCadence(true, elapsed, OneCycleSeconds, 2, null, false, false);
            Assert.AreEqual(ItemRangePreviewController.TurnCadenceOutcome.Advance, outcome);
        }

        [Test]
        public void ResolveTurnCadence_NeverCompleted_WellPastStarvationBound_Advances()
        {
            var elapsed = OneCycleSeconds * 20f;
            var outcome = ItemRangePreviewController.ResolveTurnCadence(true, elapsed, OneCycleSeconds, 2, null, false, false);
            Assert.AreEqual(ItemRangePreviewController.TurnCadenceOutcome.Advance, outcome);
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
