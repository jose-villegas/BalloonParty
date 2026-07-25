using System.Collections.Generic;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Palette;
using BalloonParty.Item.Effects;
using BalloonParty.Solver;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.ShotSolver
{
    /// <summary>Phase C1 (@ref plan_shot_solver_accuracy Phase C): the Shield item, the first item to
    /// wire a real effect through <see cref="ShotSimulator" />'s pop-site hook
    /// (<c>RunItemEffects</c>/<c>ApplyItemOutcome</c>) and <see cref="ShotItemLayer.Resolve" />'s
    /// Shield case. Bomb/Laser/Lightning/Paint/Snipe stay plumbing-only until their own sub-phase
    /// (C2 .. C6) — nothing here exercises <c>ApplyEffectHits</c> beyond it staying a no-op, since
    /// Shield has no effect core and never emits an <see cref="EffectHit" />.
    /// <para/>
    /// Rubric exclusion: the per-flight activation budget (<c>ShotItemLayer.MaxActivationsPerFlight</c>)
    /// isn't exercised here — Shield never chains into another activation, so there is no way to drive
    /// the queue deep enough with this item alone; it needs a chaining item (Bomb-into-Bomb, Phase C2+)
    /// to matter.</para></summary>
    [TestFixture]
    public class ShotItemEffectTests
    {
        // Mirrors ShotSimulatorTests'/ShotBuffScoringTests' convention: a box far larger than
        // anything fired in these tests, so only the geometry each test cares about ever produces
        // an event.
        private static readonly Vector4 WideOpenWalls = new(1000f, 1000f, -1000f, -1000f);

        // Shield reads neither _effectParams nor the effect board (no core, no EffectHits), so an
        // empty params map and a default (never-consulted) lattice are enough to build a real layer.
        private static ShotItemLayer CreateItemLayer()
        {
            var lattice = default(ShotSlotLattice);
            return new ShotItemLayer(new Dictionary<ItemType, ItemEffectParams>(), in lattice);
        }

        [Test]
        public void ResolveBalloonContact_ShieldItem_GrantsAShieldASubsequentWallBounceSpends()
        {
            // Right wall close behind a Shield carrier; a return-path balloon clears the board right
            // after the bounce so the flight never risks a second, unrelated bounce. With 0 starting
            // shields, the granted +1 is exactly enough to survive the one bounce it funds.
            var walls = new Vector4(1000f, 2f, -1000f, -1000f);
            var grantingBoard = new[]
            {
                ShotBoardBuilder.Green(new Vector2(1f, 0f), 0.1f, "Red", 1, 1, item: ItemType.Shield),
                ShotBoardBuilder.Green(new Vector2(-0.5f, 0f), 0.1f, "Blue", 1, 1),
            };
            var grantingWorkingSet = new ShotBalloonState[grantingBoard.Length];

            var granted = ShotSimulator.Simulate(
                grantingBoard, walls, Vector2.zero, Vector2.right, startingShields: 0, projectileContactRadius: 0f,
                workingSet: grantingWorkingSet, items: CreateItemLayer());

            Assert.IsFalse(granted.Died, "the item's granted shield covers the wall bounce");
            Assert.IsTrue(granted.BoardCleared);
            Assert.AreEqual(2, granted.Pops);

            // Same board, no item layer: the grant never happens, so the same bounce drops shields
            // below zero — the control this test is actually pinning against.
            var controlWorkingSet = new ShotBalloonState[grantingBoard.Length];
            var control = ShotSimulator.Simulate(
                grantingBoard, walls, Vector2.zero, Vector2.right, startingShields: 0, projectileContactRadius: 0f,
                workingSet: controlWorkingSet);

            Assert.IsTrue(control.Died, "without the item layer, no shield ever covers the bounce");
            Assert.AreEqual(1, control.Pops, "the flight dies at the wall, never reaching the return-path balloon");
        }

        [Test]
        public void ResolveBalloonContact_RainbowShieldHost_GrantsColorAgnosticBuffThatEndsAtTheSpendingWall()
        {
            // Red (anchors a real streak, not a defer) -> Rainbow+Shield host (continues the "Red"
            // streak to 2, THEN grants the until-wall buff) -> Blue (buff active: streak keeps
            // climbing to 3 despite the colour change) -> wall bounce (spends the item's granted
            // shield, clearing the buff unconditionally) -> Green (buff gone: an ordinary colour
            // mismatch against the still-"Red" StreakColor resets the streak to 1).
            var walls = new Vector4(1000f, 3.5f, -1000f, -1000f);
            var board = new[]
            {
                ShotBoardBuilder.Green(new Vector2(0.5f, 0f), 0.1f, "Red", 5, 1),
                ShotBoardBuilder.Rainbow(new Vector2(1.5f, 0f), 0.1f, GamePalette.RainbowColorId, 1, 1, item: ItemType.Shield),
                ShotBoardBuilder.Green(new Vector2(2.5f, 0f), 0.1f, "Blue", 10, 1),
                ShotBoardBuilder.Green(new Vector2(-0.5f, 0f), 0.1f, "Green", 100, 1),
            };
            var workingSet = new ShotBalloonState[board.Length];

            var result = ShotSimulator.Simulate(
                board, walls, Vector2.zero, Vector2.right, startingShields: 0, projectileContactRadius: 0f,
                workingSet: workingSet, items: CreateItemLayer(), allowedColors: new[] { "Red" });

            // Red(5*streak1) + rainbow anchors "Red"(1*streak2) + Blue buffed(10*streak3, NOT reset
            // to 1) + Green post-buff(100*streak1, reset — a still-active buff would instead climb to
            // streak4 and score 400).
            Assert.AreEqual((5 * 1) + (1 * 2) + (10 * 3) + (100 * 1), result.RawScore);
            Assert.AreEqual(4, result.Pops);
            Assert.IsFalse(result.Died, "the host's granted shield (plus the streak-of-2 refund) covers the bounce");
            Assert.IsTrue(result.BoardCleared);
        }

        [Test]
        public void ResolveBalloonContact_ItemsNull_ItemCarryingBoardMatchesPlainBoardByteForByte()
        {
            // The fast-path lock: carrying an ItemProfile must not perturb a flight that never
            // activates it — with items:null, the pop-site hook is a single `if` that never fires.
            var itemBoard = new[] { ShotBoardBuilder.Green(new Vector2(0f, 1f), 0.1f, "Red", 3, 1, item: ItemType.Shield) };
            var plainBoard = new[] { ShotBoardBuilder.Green(new Vector2(0f, 1f), 0.1f, "Red", 3, 1) };
            var itemWorkingSet = new ShotBalloonState[itemBoard.Length];
            var plainWorkingSet = new ShotBalloonState[plainBoard.Length];

            var itemResult = ShotSimulator.Simulate(
                itemBoard, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: itemWorkingSet);
            var plainResult = ShotSimulator.Simulate(
                plainBoard, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: plainWorkingSet);

            Assert.AreEqual(plainResult.RawScore, itemResult.RawScore);
            Assert.AreEqual(plainResult.Pops, itemResult.Pops);
            Assert.AreEqual(plainResult.Died, itemResult.Died);
            Assert.AreEqual(plainResult.BoardCleared, itemResult.BoardCleared);
            // The four fields above were the only ones this lock checked originally; a byte-for-byte
            // claim needs the rest of the tuple too, since a stray item-plumbing side effect could
            // easily land in one of these instead (e.g. an extra Events tick from a spurious activation).
            Assert.AreEqual(plainResult.ToughsCleared, itemResult.ToughsCleared);
            Assert.AreEqual(plainResult.Events, itemResult.Events);
            Assert.AreEqual(plainResult.Capped, itemResult.Capped);
            Assert.AreEqual(plainResult.Absorbed, itemResult.Absorbed);
        }

        [Test]
        public void ResolveBalloonContact_NoCarrierBoard_NonNullItemsLayerIsANoOp()
        {
            // The layer-side lock: a board with no ItemProfile at all must behave identically whether
            // or not a real ShotItemLayer is supplied — the pop-site hook's `host.Item != ItemType.None`
            // guard is the only thing standing between "items given" and "items used".
            var board = new[] { ShotBoardBuilder.Green(new Vector2(0f, 1f), 0.1f, "Red", 3, 1) };
            var withoutLayerWorkingSet = new ShotBalloonState[board.Length];
            var withLayerWorkingSet = new ShotBalloonState[board.Length];

            var withoutLayer = ShotSimulator.Simulate(
                board, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: withoutLayerWorkingSet);
            var withLayer = ShotSimulator.Simulate(
                board, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: withLayerWorkingSet, items: CreateItemLayer());

            Assert.AreEqual(withoutLayer.RawScore, withLayer.RawScore);
            Assert.AreEqual(withoutLayer.Pops, withLayer.Pops);
            Assert.AreEqual(withoutLayer.Died, withLayer.Died);
            Assert.AreEqual(withoutLayer.BoardCleared, withLayer.BoardCleared);
            // See the sibling fast-path lock above for why the tuple needs to be complete here too.
            Assert.AreEqual(withoutLayer.ToughsCleared, withLayer.ToughsCleared);
            Assert.AreEqual(withoutLayer.Events, withLayer.Events);
            Assert.AreEqual(withoutLayer.Capped, withLayer.Capped);
            Assert.AreEqual(withoutLayer.Absorbed, withLayer.Absorbed);
        }

        [Test]
        public void ResolveBalloonContact_ShieldHostPopAlsoRefunds_BothShieldSourcesStack()
        {
            // Two consecutive Red pops (streak 1, then streak 2) put the THIRD pop's refund gate
            // (StreakCount >= 2, same color) and its OWN Shield-item grant on the identical pop — the
            // stacking case @ref plan_shot_solver_accuracy Phase C §7 calls out ("+1 refund +1 item
            // shield = +2"). A filler balloon far off the flight's fixed x-axis keeps the working set
            // non-empty (the sim loop exits the instant activeCount hits 0) without ever being hit,
            // so the flight is free to keep bouncing between two close walls afterward — the only way
            // to observe the exact shield COUNT this pop produced.
            var walls = new Vector4(1000f, 2f, -1000f, -2f);
            var board = new[]
            {
                ShotBoardBuilder.Green(new Vector2(0.5f, 0f), 0.1f, "Red", 1, 1),
                ShotBoardBuilder.Green(new Vector2(1.0f, 0f), 0.1f, "Red", 1, 1, item: ItemType.Shield),
                ShotBoardBuilder.Green(new Vector2(0f, 500f), 0.01f, "Blue", 1, 1),
            };
            var workingSet = new ShotBalloonState[board.Length];

            var result = ShotSimulator.Simulate(
                board, walls, Vector2.zero, Vector2.right, startingShields: 0, projectileContactRadius: 0f,
                workingSet: workingSet, items: CreateItemLayer());

            // Shields 0 -> +1 (refund, streak reaches 2 on "Red") -> +1 (the item's own grant) = 2.
            // Bounce 1 (right wall, event 3) spends one and survives; bounce 2 (left wall, event 4)
            // spends the second and survives; bounce 3 (right wall again, event 5) has none left and
            // dies. Events == 4 (not 5) is exactly what a single stacked source (refund-only or
            // grant-only, i.e. 1 shield instead of 2) would produce instead — dying on bounce 2.
            Assert.AreEqual(2, result.Pops);
            Assert.IsTrue(result.Died, "with only 2 shields, the third close-corridor bounce is fatal");
            Assert.AreEqual(5, result.Events, "the stack must fund exactly two survived bounces, not one");
        }

        [Test]
        public void ResolveBalloonContact_RainbowShieldHost_GrantAppliesAfterTheHostsOwnPopIsScored()
        {
            // A 3-pop "Blue" streak, then a Rainbow+Shield host whose chosen primary ("Green", the
            // only allowed colour, since the projectile's own "Blue" isn't in the filter) DIFFERS from
            // the running streak colour — so the host's own pop's multiplier depends entirely on
            // whether its OWN grant was already active when it scored:
            // - grant AFTER (live-faithful — the one-frame ItemActivator delay means a pop can never
            //   see the buff its own item grants): RecordColor("Green") resets streak 3 -> 1, this pop
            //   scores 10*1=10, and the trailing "Purple" pop (now buffed) climbs to streak 2 -> 200.
            //   Total = (1+2+3) + 10 + 200 = 216.
            // - grant BEFORE (the bug this test guards against): WildcardStreak keeps "Blue"'s streak
            //   climbing straight through — 3->4 (this pop scores 10*4=40) then 4->5 (Purple scores
            //   100*5=500). Total = 6 + 40 + 500 = 546.
            // The two totals diverge by more than 2x, so a reordering regression cannot slip through.
            var board = new[]
            {
                ShotBoardBuilder.Green(new Vector2(0.5f, 0f), 0.1f, "Blue", 1, 1),
                ShotBoardBuilder.Green(new Vector2(1.0f, 0f), 0.1f, "Blue", 1, 1),
                ShotBoardBuilder.Green(new Vector2(1.5f, 0f), 0.1f, "Blue", 1, 1),
                ShotBoardBuilder.Rainbow(
                    new Vector2(2.0f, 0f), 0.1f, GamePalette.RainbowColorId, 10, 1, item: ItemType.Shield),
                ShotBoardBuilder.Green(new Vector2(2.5f, 0f), 0.1f, "Purple", 100, 1),
            };
            var workingSet = new ShotBalloonState[board.Length];

            var result = ShotSimulator.Simulate(
                board, WideOpenWalls, Vector2.zero, Vector2.right, startingShields: 0, projectileContactRadius: 0f,
                workingSet: workingSet, items: CreateItemLayer(), allowedColors: new[] { "Green" });

            Assert.AreEqual((1 + 2 + 3) + 10 + 200, result.RawScore);
            Assert.AreEqual(5, result.Pops);
            Assert.IsFalse(result.Died);
            Assert.IsTrue(result.BoardCleared);
        }
    }
}
