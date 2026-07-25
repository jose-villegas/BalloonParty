using System.Collections.Generic;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Palette;
using BalloonParty.Item.Effects;
using BalloonParty.Shared;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Solver;
using NSubstitute;
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

        // Phase C2 (Bomb): a real Bomb config entry, rather than the empty dict above — every test
        // below reads exactly one item type's worth of settings, so damage/flags/radius are the only
        // knobs a test needs to vary.
        private static ShotItemLayer CreateBombItemLayer(
            float radius, int damage, float rainbowConversionRange = 0f, string rainbowColorId = null,
            DamageFlags flags = DamageFlags.Normal)
        {
            var effectParams = new Dictionary<ItemType, ItemEffectParams>
            {
                {
                    ItemType.Bomb,
                    new ItemEffectParams(
                        new BombEffectParams(radius, rainbowEffectScale: 1f, rainbowConversionRange), default, default,
                        default, default, damage, flags)
                },
            };
            var lattice = default(ShotSlotLattice);
            return new ShotItemLayer(effectParams, in lattice, rainbowColorId);
        }

        // Phase C3 (Laser): a real Laser config entry — same one-item-type-worth-of-settings
        // convention as CreateBombItemLayer above (castRadius/castDistance map onto
        // LaserEffectParams's CircleCastRadius/RaycastDistance).
        private static ShotItemLayer CreateLaserItemLayer(
            float castRadius, float castDistance, int damage, DamageFlags flags = DamageFlags.Normal,
            string rainbowColorId = null)
        {
            var effectParams = new Dictionary<ItemType, ItemEffectParams>
            {
                {
                    ItemType.Laser,
                    new ItemEffectParams(
                        default, new LaserEffectParams(castDistance, castRadius, colorCycles: 0f), default, default,
                        default, damage, flags)
                },
            };
            var lattice = default(ShotSlotLattice);
            return new ShotItemLayer(effectParams, in lattice, rainbowColorId);
        }

        // Shared by the fast-path lock test below — a full-tuple comparison, not just the four fields
        // the original Shield-era locks checked (see their own comment for why the complete tuple
        // matters: a stray item-plumbing side effect could easily land in a field neither of them read).
        private static void AssertResultsMatch(ShotSimulationResult expected, ShotSimulationResult actual)
        {
            Assert.AreEqual(expected.RawScore, actual.RawScore);
            Assert.AreEqual(expected.Pops, actual.Pops);
            Assert.AreEqual(expected.Died, actual.Died);
            Assert.AreEqual(expected.BoardCleared, actual.BoardCleared);
            Assert.AreEqual(expected.ToughsCleared, actual.ToughsCleared);
            Assert.AreEqual(expected.Events, actual.Events);
            Assert.AreEqual(expected.Capped, actual.Capped);
            Assert.AreEqual(expected.Absorbed, actual.Absorbed);
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

        [Test]
        public void ApplyEffectHits_BombBlast_RadiusBoundary_JustInsideHitsJustOutsideSurvives()
        {
            // Bomb radius 1.0 + the target's own 0.2 contact radius = 1.2 combined kill radius (mirrors
            // BombBlast.ResolveNormal's Physics2D.OverlapCircle-equivalent circle-vs-circle test) — off
            // the shot's straight-up flight line (x offset) so only the host is ever hit directly. Two
            // separate single-occupant scenarios isolate the boundary cleanly.
            var effectParams = CreateBombItemLayer(radius: 1.0f, damage: 1);

            var insideBoard = new[]
            {
                ShotBoardBuilder.Green(
                    new Vector2(0f, 1f), 0.05f, "Red", 1, 1, new Vector2Int(0, 0), 0, 0, 0f, false, null,
                    item: ItemType.Bomb),
                ShotBoardBuilder.Green(
                    new Vector2(1.19f, 1f), 0.2f, "Blue", 1, 1, new Vector2Int(1, 0), 0, 0, 0f, false, null),
            };
            var insideWorkingSet = new ShotBalloonState[insideBoard.Length];
            var insideResult = ShotSimulator.Simulate(
                insideBoard, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: insideWorkingSet, items: CreateBombItemLayer(radius: 1.0f, damage: 1));

            Assert.AreEqual(2, insideResult.Pops, "distance 1.19 is inside the 1.2 combined radius — the target dies");
            Assert.IsTrue(insideResult.BoardCleared);

            var outsideBoard = new[]
            {
                ShotBoardBuilder.Green(
                    new Vector2(0f, 1f), 0.05f, "Red", 1, 1, new Vector2Int(0, 0), 0, 0, 0f, false, null,
                    item: ItemType.Bomb),
                ShotBoardBuilder.Green(
                    new Vector2(1.21f, 1f), 0.2f, "Blue", 1, 1, new Vector2Int(1, 0), 0, 0, 0f, false, null),
            };
            var outsideWorkingSet = new ShotBalloonState[outsideBoard.Length];
            var outsideResult = ShotSimulator.Simulate(
                outsideBoard, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: outsideWorkingSet, items: CreateBombItemLayer(radius: 1.0f, damage: 1));

            Assert.AreEqual(1, outsideResult.Pops, "distance 1.21 is outside the 1.2 combined radius — no hit at all");
            Assert.IsFalse(outsideResult.BoardCleared);
        }

        [Test]
        public void ApplyEffectHits_HexNeighbourWithinRadius_GuaranteedKillVsPlainDamageDecrement()
        {
            // Slot identity (hex-neighbour classification) and world position (radius selection) are
            // independent snapshot fields — this test isolates each axis: a hex-neighbour SLOT of the
            // host placed CLOSE dies outright regardless of hitsRemaining (PiercingDamage — a guaranteed
            // kill); the SAME hex-neighbour SLOT placed FAR away takes no hit at all; a non-neighbour
            // SLOT placed close (Damage-kind) merely decrements — surviving the identical hitsRemaining
            // (5) the close neighbour dies to.
            var board = new[]
            {
                ShotBoardBuilder.Green(
                    new Vector2(0f, 1f), 0.05f, "Red", 1, 1, new Vector2Int(3, 3), 0, 0, 0f, false, null,
                    item: ItemType.Bomb),
                ShotBoardBuilder.Green(
                    new Vector2(0.5f, 1f), 0.05f, "Blue", 1, 5, new Vector2Int(2, 3), 0, 0, 0f, false, null),
                ShotBoardBuilder.Green(
                    new Vector2(100f, 100f), 0.05f, "Green", 1, 5, new Vector2Int(4, 3), 0, 0, 0f, false, null),
                ShotBoardBuilder.Green(
                    new Vector2(0.3f, 1.3f), 0.05f, "Purple", 1, 5, new Vector2Int(10, 10), 0, 0, 0f, false, null),
            };
            var workingSet = new ShotBalloonState[board.Length];

            var result = ShotSimulator.Simulate(
                board, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: workingSet, items: CreateBombItemLayer(radius: 1.0f, damage: 1));

            Assert.AreEqual(2, result.Pops, "host + the near hex-neighbour only — the far neighbour and the near non-neighbour both survive");
            Assert.IsFalse(result.BoardCleared);
        }

        [Test]
        public void ApplyEffectHits_RainbowBombRing_ConvertsWithoutKilling()
        {
            // Rainbow-host classification drops the occupant-radius term (centre distance alone): kill
            // radius 1.0, conversion ring out to 2.0 (radius 1.0 + range 1.0). The ring target sits at
            // 1.5 — inside the ring, outside the kill zone — so it must survive and simply recolor.
            // Host at the LAST board index so its own swap-remove is a self-copy no-op (RemoveActive
            // copies the last active element into the removed slot — removing the last element is
            // always a no-op), leaving the ring target's index (0) untouched to inspect after the flight.
            var board = new[]
            {
                ShotBoardBuilder.Green(new Vector2(1.5f, 1f), 0.05f, "Blue", 1, 1),
                ShotBoardBuilder.Rainbow(
                    new Vector2(0f, 1f), 0.05f, GamePalette.RainbowColorId, 1, 1, new Vector2Int(5, 5), 0, 0, 0f,
                    false, null, item: ItemType.Bomb),
            };
            var workingSet = new ShotBalloonState[board.Length];

            var result = ShotSimulator.Simulate(
                board, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: workingSet,
                items: CreateBombItemLayer(
                    radius: 1.0f, damage: 1, rainbowConversionRange: 1.0f, rainbowColorId: GamePalette.RainbowColorId),
                rainbowColorId: GamePalette.RainbowColorId, allowedColors: new[] { "Blue" });

            Assert.AreEqual(1, result.Pops, "only the rainbow host itself pops — the ring target survives");
            Assert.IsFalse(result.BoardCleared);
            Assert.IsTrue(workingSet[0].IsRainbow, "the ring converts the surviving target to rainbow");
            Assert.AreEqual(GamePalette.RainbowColorId, workingSet[0].ColorId);
        }

        [Test]
        public void ApplyEffectHits_RainbowBombRing_OuterBoundary_JustInsideRecolorsJustOutsideUntouched()
        {
            // Companion boundary test to the normal-radius one above, but for the rainbow ring's OUTER
            // edge — kill radius 1.0, conversion range 1.0, outer radius exactly 2.0, centre-distance
            // ONLY (no added occupant radius — BombBlast.ResolveRainbow's own doc). Two isolated
            // single-occupant scenarios with the same ±0.01 margin convention as the normal-radius
            // boundary test above; host at the LAST board index (self-copy no-op) so the ring target's
            // index (0) is stable to inspect afterward.
            var insideBoard = new[]
            {
                ShotBoardBuilder.Green(new Vector2(1.99f, 1f), 0.2f, "Blue", 1, 1),
                ShotBoardBuilder.Rainbow(
                    new Vector2(0f, 1f), 0.05f, GamePalette.RainbowColorId, 1, 1, new Vector2Int(5, 5), 0, 0, 0f,
                    false, null, item: ItemType.Bomb),
            };
            var insideWorkingSet = new ShotBalloonState[insideBoard.Length];
            var insideResult = ShotSimulator.Simulate(
                insideBoard, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: insideWorkingSet,
                items: CreateBombItemLayer(
                    radius: 1.0f, damage: 1, rainbowConversionRange: 1.0f, rainbowColorId: GamePalette.RainbowColorId),
                rainbowColorId: GamePalette.RainbowColorId, allowedColors: new[] { "Blue" });

            Assert.AreEqual(1, insideResult.Pops, "only the rainbow host pops — 1.99 sits inside the 2.0 outer radius, it survives");
            Assert.IsTrue(insideWorkingSet[0].IsRainbow, "1.99 is inside the ring — it converts");

            var outsideBoard = new[]
            {
                ShotBoardBuilder.Green(new Vector2(2.01f, 1f), 0.2f, "Blue", 1, 1),
                ShotBoardBuilder.Rainbow(
                    new Vector2(0f, 1f), 0.05f, GamePalette.RainbowColorId, 1, 1, new Vector2Int(5, 5), 0, 0, 0f,
                    false, null, item: ItemType.Bomb),
            };
            var outsideWorkingSet = new ShotBalloonState[outsideBoard.Length];
            var outsideResult = ShotSimulator.Simulate(
                outsideBoard, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: outsideWorkingSet,
                items: CreateBombItemLayer(
                    radius: 1.0f, damage: 1, rainbowConversionRange: 1.0f, rainbowColorId: GamePalette.RainbowColorId),
                rainbowColorId: GamePalette.RainbowColorId, allowedColors: new[] { "Blue" });

            Assert.AreEqual(1, outsideResult.Pops, "only the rainbow host pops — 2.01 sits outside the ring entirely");
            Assert.IsFalse(outsideWorkingSet[0].IsRainbow, "2.01 is outside the 2.0 outer radius — no hit at all, no conversion");
        }

        [Test]
        public void ApplyEffectHits_Damage3_PopsATwoHitTough_FlatScoreNoStreak()
        {
            // A non-neighbour Tough within the blast, damage:3 against hitsRemaining:2 — pops (3 >= 2),
            // via the flat/streak-breaking ResolveToughPop rule (an ordinary colourless Tough, not
            // PaysSourceColor): host(2*streak1=2) + tough's own flat ScoreValue(5, no multiplier) = 7.
            var board = new[]
            {
                ShotBoardBuilder.Green(
                    new Vector2(0f, 1f), 0.05f, "Red", 2, 1, new Vector2Int(3, 3), 0, 0, 0f, false, null,
                    item: ItemType.Bomb),
                ShotBoardBuilder.Tough(
                    new Vector2(0.5f, 1f), 0.05f, 5, 2, new Vector2Int(9, 9), 0, 0, 0f, false, null),
            };
            var workingSet = new ShotBalloonState[board.Length];

            var result = ShotSimulator.Simulate(
                board, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: workingSet, items: CreateBombItemLayer(radius: 1.0f, damage: 3));

            Assert.AreEqual(2, result.Pops);
            Assert.AreEqual(1, result.ToughsCleared, "the item pop still routes an ordinary Tough through the flat rule");
            Assert.AreEqual(2 + 5, result.RawScore);
            Assert.IsTrue(result.BoardCleared);
        }

        [Test]
        public void ApplyEffectHits_SurvivingDamageHit_StillNudgesItsHexNeighbour()
        {
            // Mirrors BalanceBiasFidelityTests.RunFlight's dynamics rig: a real ShotBoardDynamics over a
            // headless SlotGrid, no rebalance pulses (interval 0) so nothing else moves the board
            // mid-flight. The Tough survives (hitsRemaining 5 - damage 3 = 2) yet ApplyEffectHits nudges
            // its hex-neighbour regardless — NudgeService has no outcome filter for item hits, mirroring
            // a pop's own unconditional nudge.
            var gridConfig = Substitute.For<ISlotGridConfig>();
            gridConfig.SlotsSize.Returns(new Vector2Int(5, 3));
            gridConfig.SlotSeparation.Returns(new Vector2(1f, 1f));
            gridConfig.SlotsOffset.Returns(Vector2.zero);

            var balloonsConfig = Substitute.For<IBalloonsConfiguration>();
            balloonsConfig.FlightRebalanceInterval.Returns(0f);

            var board = new[]
            {
                ShotBoardBuilder.Green(
                    new Vector2(0f, 1f), 0.05f, "Red", 2, 1, new Vector2Int(0, 0), 0, 0, 0f, false, null,
                    item: ItemType.Bomb),
                ShotBoardBuilder.Tough(
                    new Vector2(0.5f, 1f), 0.05f, 9, 5, new Vector2Int(3, 1), 0, 0, 0f, false, null),
                ShotBoardBuilder.Green(
                    new Vector2(100f, 100f), 0.05f, "Blue", 1, 1, new Vector2Int(2, 1), 0, 0, 0f, false, null),
            };
            var dynamics = new ShotBoardDynamics(
                gridConfig, balloonsConfig, board, new ShotDynamicActorSnapshot[0], new ShotStaticActorSnapshot[0]);
            var workingSet = new ShotBalloonState[board.Length];

            var result = ShotSimulator.Simulate(
                board, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: workingSet, dynamics: dynamics, items: CreateBombItemLayer(radius: 1.0f, damage: 3));

            Assert.AreEqual(1, result.Pops, "only the host itself pops — the tough survives the damage-3 hit");
            Assert.IsFalse(result.BoardCleared);
            Assert.AreEqual(2, workingSet[1].HitsRemaining, "5 - damage(3) = 2 — survived, but was still hit");
            Assert.Greater(
                dynamics.TargetActors[2].NudgeImpulses.Count, 0,
                "the surviving Damage hit still nudges its hex-neighbour, same as a pop would");
        }

        [Test]
        public void ApplyEffectHits_Damage3AgainstFourHitTough_LeavesExactlyOneHitRemaining()
        {
            // Boundary companion to the two tests above: damage(3) against hitsRemaining(4) leaves
            // EXACTLY 1 (4-3=1, still > 0) rather than the comfortable margins those exercise (2-3
            // overshoots to a pop; 5-3=2 survives with room to spare) — pins the `<= 0` pop threshold
            // against an off-by-one that would treat a small positive remainder as a kill.
            var board = new[]
            {
                ShotBoardBuilder.Green(
                    new Vector2(0f, 1f), 0.05f, "Red", 1, 1, new Vector2Int(0, 0), 0, 0, 0f, false, null,
                    item: ItemType.Bomb),
                ShotBoardBuilder.Tough(
                    new Vector2(0.5f, 1f), 0.05f, 5, 4, new Vector2Int(9, 9), 0, 0, 0f, false, null),
                ShotBoardBuilder.Green(
                    new Vector2(100f, 100f), 0.05f, "Blue", 1, 1, new Vector2Int(2, 1), 0, 0, 0f, false, null),
            };
            var workingSet = new ShotBalloonState[board.Length];

            var result = ShotSimulator.Simulate(
                board, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: workingSet, items: CreateBombItemLayer(radius: 1.0f, damage: 3));

            Assert.AreEqual(1, result.Pops, "only the host pops — the tough survives with exactly 1 hit remaining");
            Assert.IsFalse(result.BoardCleared);
            Assert.AreEqual(1, workingSet[1].HitsRemaining, "4 - damage(3) = 1 — the exact boundary, must not pop");
        }

        [Test]
        public void ApplyEffectHits_BombPopsAShieldCarryingNeighbour_ChainsTheShieldGrant()
        {
            // Cross-item chaining (@ref plan_shot_solver_accuracy Phase C2 memo §7): PopItemHit's own
            // chained-activation construction never discriminates by ItemType — a Bomb-popped Shield
            // carrier must still grant its shield exactly like a direct-contact Shield pop does
            // (ResolveBalloonContact_ShieldItem_GrantsAShieldASubsequentWallBounceSpends, above, is the
            // direct-contact baseline this test extends into a chain). A far-off filler keeps the
            // working set non-empty through the one wall bounce that proves the grant: without it, the
            // shot dies on that SAME bounce (0 shields); with it, 1 shield survives the bounce.
            var walls = new Vector4(1.5f, 1000f, -1000f, -1000f);
            var board = new[]
            {
                ShotBoardBuilder.Green(
                    new Vector2(0f, 1f), 0.05f, "Red", 1, 1, new Vector2Int(0, 0), 0, 0, 0f, false, null,
                    item: ItemType.Bomb),
                ShotBoardBuilder.Green(
                    new Vector2(0.5f, 1f), 0.05f, "Blue", 1, 1, new Vector2Int(1, 0), 0, 0, 0f, false, null,
                    item: ItemType.Shield),
                ShotBoardBuilder.Green(
                    new Vector2(500f, 500f), 0.05f, "Green", 1, 1, new Vector2Int(20, 20), 0, 0, 0f, false, null),
            };
            var workingSet = new ShotBalloonState[board.Length];

            var result = ShotSimulator.Simulate(
                board, walls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: workingSet, items: CreateBombItemLayer(radius: 1.0f, damage: 1));

            Assert.AreEqual(2, result.Pops, "host (direct) and the Shield carrier (bomb-triggered) both pop");
            Assert.IsFalse(result.BoardCleared, "the far-off filler survives untouched");
            Assert.AreEqual(2, result.Events, "host contact + the one wall bounce the grant survives");
            Assert.IsFalse(
                result.Died, "the chained Shield grant survives the wall bounce that would otherwise kill it (0 shields)");
        }

        [Test]
        public void ApplyEffectHits_BombHexNeighbourUnbreakable_GuaranteedKillPaysSourceColorNoRefund()
        {
            // The C2a payout path through an ITEM-triggered pop (companion to ShotBuffScoringTests'
            // pure-projectile-contact C2a guard): a hex-neighbour Unbreakable (hitsRemaining ==
            // int.MaxValue) within the blast radius is a guaranteed kill regardless of its durability
            // (PiercingDamage always pops), and its pop still routes through the PaysSourceColor branch
            // even though the cause is an item effect — paying the host's own colour (cause.SourceColorId)
            // and EXTENDING the established streak (not resetting it, unlike an ordinary Tough), yet
            // never refunding (IsProjectileContact is false for an item pop). A far-off filler keeps the
            // flight alive to the one wall bounce that proves the missing refund.
            var walls = new Vector4(1.5f, 1000f, -1000f, -1000f);
            var board = new[]
            {
                ShotBoardBuilder.Green(
                    new Vector2(0f, 1f), 0.05f, "Red", 2, 1, new Vector2Int(0, 0), 0, 0, 0f, false, null,
                    item: ItemType.Bomb),
                ShotBoardBuilder.Tough(
                    new Vector2(0.5f, 1f), 0.05f, 9, int.MaxValue, new Vector2Int(1, 0), 0, 0, 0f, false, null,
                    paysSourceColor: true),
                ShotBoardBuilder.Green(
                    new Vector2(500f, 500f), 0.05f, "Green", 1, 1, new Vector2Int(20, 20), 0, 0, 0f, false, null),
            };
            var workingSet = new ShotBalloonState[board.Length];

            var result = ShotSimulator.Simulate(
                board, walls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: workingSet, items: CreateBombItemLayer(radius: 1.0f, damage: 1));

            // Host(2*streak1=2) + the Unbreakable's item pop, paying "Red" and EXTENDING the same streak
            // to 2 (RecordColor, not ResolveToughPop's flat/reset rule): 9*2=18. Total 2+18=20.
            Assert.AreEqual(2 + 18, result.RawScore);
            Assert.AreEqual(2, result.Pops, "the guaranteed-kill hex-neighbour pops despite int.MaxValue durability");
            Assert.AreEqual(1, result.ToughsCleared, "PaysSourceColor still tallies as a tough popped");
            Assert.IsFalse(result.BoardCleared, "the far-off filler survives untouched");

            // No refund fired for the Unbreakable's own pop (IsProjectileContact is false for an item
            // pop) — 0 shields dies on the very next bounce; a wrongly-fired refund would survive it.
            Assert.AreEqual(2, result.Events, "host contact + the one wall bounce that exposes the missing refund");
            Assert.IsTrue(result.Died, "0 shields — the item pop never refunded");
        }

        [Test]
        public void ApplyEffectHits_ChainedBombIntoBomb_StopsAtTheActivationBudget()
        {
            // 40 Bomb-carrying dominoes spread along +x at y=1 (distance 1.0 apart) — only domino[0]
            // sits on the vertical flight ray (x=0); every other domino is far enough off-ray (x>=1) to
            // never be hit directly, so every pop past domino[0] can ONLY happen via the bomb chain.
            // Bomb radius 1.0 + the uniform 0.05 occupant radius = 1.05 combined — covers each domino's
            // IMMEDIATE next neighbour (distance 1.0) but not the one after that (distance 2.0 > 1.05),
            // so the chain can only ever cascade forward one hop at a time.
            //
            // Activation-budget hand-trace (MaxActivationsPerFlight = 32): domino[0]'s direct-hit pop
            // enqueues Activation #1 (its OWN bomb — the host's own direct-hit activation counts toward
            // the budget). Draining Activation #k (domino[k-1]'s bomb, k = 1..32) pops domino[k] and
            // chains Activation #(k+1) (domino[k]'s own bomb). TryBeginActivation accepts while
            // _activationCount < 32, so Activations #1..#32 all succeed (the 32nd bringing the count to
            // exactly 32); domino[32]'s pop then tries to chain Activation #33, which TryBeginActivation
            // rejects (count already at 32) — domino[32]'s own bomb never fires, so domino[33] onward are
            // never touched. Total popped: domino[0] (direct) plus domino[1..32] (32 chained pops) = 33;
            // domino[33..39] (7 dominoes) survive.
            const int dominoCount = 40;
            var board = new ShotBalloonSnapshot[dominoCount];
            board[0] = ShotBoardBuilder.Green(
                new Vector2(0f, 1f), 0.05f, "Red", 1, 1, new Vector2Int(0, 0), 0, 0, 0f, false, null,
                item: ItemType.Bomb);
            for (var k = 1; k < dominoCount; k++)
            {
                board[k] = ShotBoardBuilder.Green(
                    new Vector2(k, 1f), 0.05f, "Red", 1, 1, new Vector2Int(k, 0), 0, 0, 0f, false, null,
                    item: ItemType.Bomb);
            }

            var workingSet = new ShotBalloonState[board.Length];
            var result = ShotSimulator.Simulate(
                board, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: workingSet, items: CreateBombItemLayer(radius: 1.0f, damage: 1));

            Assert.AreEqual(33, result.Pops, "domino[0] direct + 32 chained pops (the budget's own cap), then the chain starves");
            Assert.IsFalse(result.BoardCleared, "the 7 dominoes past the budget's reach never get blasted");
        }

        [Test]
        public void ApplyEffectHits_BombPopContinuesAnEstablishedStreak_NoRefundNoNeighbourConversion()
        {
            // A pre-established "Red" streak-of-1 (mirrors ShotBuffScoringTests' refund-gate seeding
            // technique — the WildcardStreak branch never itself writes StreakColor, so the seed pins it
            // directly) plus a rainbow buff kept active throughout (so a hypothetical PopItemHit
            // regression that copies the direct-contact path's ConvertNeighborsToRainbow call — gated on
            // the buff or not — would still be exercised): B's own direct hit extends the streak to 2 (an
            // ordinary direct-hit refund fires here — the BASELINE this test is not questioning), then
            // B's Bomb blast pops C (same colour, off the flight's ray) via an ITEM effect — continuing
            // the SAME streak to 3 for its own scoring (not resetting), but never refunding
            // (IsProjectileContact is false for an item pop) and never touching D, C's real
            // hex-neighbour, which must stay un-rainbow.
            var walls = new Vector4(1000f, 1.5f, -1000f, -1.5f);
            var board = new[]
            {
                ShotBoardBuilder.Green(
                    new Vector2(1.0f, 0f), 0.05f, "Red", 1, 1, new Vector2Int(6, 6), 0, 0, 0f, false, null,
                    item: ItemType.Bomb),
                ShotBoardBuilder.Green(
                    new Vector2(1.0f, 0.3f), 0.05f, "Red", 100, 1, new Vector2Int(0, 0), 0, 0, 0f, false, null),
                ShotBoardBuilder.Green(
                    new Vector2(50f, 50f), 0.05f, "Red", 1, 1, new Vector2Int(1, 0), 0, 0, 0f, false, null),
            };
            var workingSet = new ShotBalloonState[board.Length];

            var result = ShotSimulator.Simulate(
                board, walls, Vector2.zero, Vector2.right, startingShields: 0, projectileContactRadius: 0f,
                workingSet: workingSet,
                items: CreateBombItemLayer(radius: 1.0f, damage: 1, rainbowColorId: GamePalette.RainbowColorId),
                rainbowColorId: GamePalette.RainbowColorId,
                seed: ShotFlightSeed.WithRainbowBuff(
                    untilWall: true, projectileColor: "Red", streakColor: "Red", streakCount: 1));

            // B(1*streak2) + C's item pop(100*streak3, continuing — NOT reset to 1) = 2 + 300 = 302.
            Assert.AreEqual(2 + 300, result.RawScore);
            Assert.AreEqual(2, result.Pops, "B (direct) and C (bomb-triggered) pop; D survives untouched");
            Assert.IsFalse(result.BoardCleared);

            // Shields: 0 -> +1 (B's own direct-hit refund) only; C's item pop must NOT refund a second
            // time. 1 shield survives exactly one bounce (right wall) then dies on the second (left
            // wall) — Events == 3 (contact + 2 walls). A wrongly-doubled refund (2 shields) would
            // survive a THIRD bounce too, dying only on a 4th event instead.
            Assert.AreEqual(3, result.Events);
            Assert.IsTrue(result.Died);

            // D is C's real hex-neighbour (slot (1,0) neighbours slot (0,0)) and shares its colour — an
            // item pop must never call ConvertNeighborsToRainbow (wired only into the direct-contact
            // path), even though the buff is still active when C pops.
            Assert.IsFalse(workingSet[0].IsRainbow, "an item-triggered pop must never convert its neighbours to rainbow");
            Assert.AreEqual("Red", workingSet[0].ColorId);
        }

        [Test]
        public void ResolveBalloonContact_BombCarryingBoard_ItemsNullOrConfigMissing_MatchesPlainBoard()
        {
            // The Bomb-specific fast-path lock (Shield's own lock lives above) — a Bomb-carrying host
            // that never gets to activate (items:null) or whose item layer has no Bomb entry in its
            // config dict must behave byte-identically to a board with no item at all; a nearby off-ray
            // balloon within what WOULD be blast range proves nothing gets blasted either way.
            var plainBoard = new[]
            {
                ShotBoardBuilder.Green(new Vector2(0f, 1f), 0.05f, "Red", 3, 1),
                ShotBoardBuilder.Green(new Vector2(0.5f, 1f), 0.05f, "Blue", 1, 1),
            };
            var bombBoard = new[]
            {
                ShotBoardBuilder.Green(new Vector2(0f, 1f), 0.05f, "Red", 3, 1, item: ItemType.Bomb),
                ShotBoardBuilder.Green(new Vector2(0.5f, 1f), 0.05f, "Blue", 1, 1),
            };

            var plainWorkingSet = new ShotBalloonState[plainBoard.Length];
            var plainResult = ShotSimulator.Simulate(
                plainBoard, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: plainWorkingSet);

            var itemsNullWorkingSet = new ShotBalloonState[bombBoard.Length];
            var itemsNullResult = ShotSimulator.Simulate(
                bombBoard, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: itemsNullWorkingSet);

            var missingConfigWorkingSet = new ShotBalloonState[bombBoard.Length];
            var missingConfigResult = ShotSimulator.Simulate(
                bombBoard, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: missingConfigWorkingSet, items: CreateItemLayer());

            AssertResultsMatch(plainResult, itemsNullResult);
            AssertResultsMatch(plainResult, missingConfigResult);
        }

        [Test]
        public void ApplyEffectHits_LaserFlagsPiercing_PopsAnUnbreakableAndFollowsOrdinaryPerColorStreak()
        {
            // Design ruling 2026-07-25: the laser's ItemConfiguration.asset _damageFlags was a CONFIG
            // ERROR (previously -1), now fixed to 1 (DamageFlags.Piercing only) — no WildcardStreak, no
            // DirectHit, no DeferredStreak. Piercing alone still pops EVERYTHING in the corridor
            // (unbreakables included, HitsRemaining == int.MaxValue), but every item pop otherwise
            // follows the ORDINARY per-hit streak rule (adoption/refund are suppressed for any item
            // cause regardless of flags — see ShotPopCause.IsProjectileContact's own doc): each pop
            // records its OWN colour, so two DIFFERENT colours in a row reset the streak rather than
            // climbing color-agnostically.
            //
            // Host (LAST board index, so its own pop is a swap-remove self-copy — RemoveActive copies
            // the last active element into the removed slot, a no-op when that slot IS the last one —
            // leaving the two right-arm targets at their original indices/order) pops via direct
            // contact (Red, streak 1, score 2*1=2). The right arm then hits, in board order: Blue
            // (ordinary Green, streak resets "Red"->"Blue"=1, score 3*1=3), then the Unbreakable
            // (PaysSourceColor pays the host's OWN colour "Red", streak resets "Blue"->"Red"=1, ToughsCleared++,
            // score 5*1=5). Total = 2+3+5 = 10.
            var board = new[]
            {
                ShotBoardBuilder.Green(
                    new Vector2(1f, 1f), 0.05f, "Blue", 3, 1, new Vector2Int(1, 0), 0, 0, 0f, false, null),
                ShotBoardBuilder.Tough(
                    new Vector2(2f, 1f), 0.05f, 5, int.MaxValue, new Vector2Int(2, 0), 0, 0, 0f, false, null,
                    paysSourceColor: true),
                ShotBoardBuilder.Green(
                    new Vector2(0f, 1f), 0.05f, "Red", 2, 1, new Vector2Int(9, 9), 0, 0, 0f, false, null,
                    item: ItemType.Laser),
            };
            var workingSet = new ShotBalloonState[board.Length];

            var result = ShotSimulator.Simulate(
                board, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: workingSet,
                items: CreateLaserItemLayer(castRadius: 0.1f, castDistance: 3f, damage: 1, flags: DamageFlags.Piercing));

            Assert.AreEqual(3, result.Pops, "host + both right-arm targets, including the int.MaxValue unbreakable");
            Assert.AreEqual(1, result.ToughsCleared, "the Unbreakable's PaysSourceColor pop still tallies as a tough");
            Assert.AreEqual(2 + 3 + 5, result.RawScore);
            Assert.IsTrue(result.BoardCleared);
        }

        [Test]
        public void ApplyEffectHits_LaserFlagsFlow_NonPiercingDamageMerelyDecrementsATough()
        {
            // Proves the activation's OWN configured Flags/Damage actually reach ApplyEffectHits (not
            // just a hard-coded Piercing path) — a contrived non-Piercing flags value against
            // damage:3 leaves a 5-HP tough at 2 (survives), rather than popping it outright.
            var board = new[]
            {
                ShotBoardBuilder.Tough(
                    new Vector2(1f, 1f), 0.05f, 9, 5, new Vector2Int(1, 0), 0, 0, 0f, false, null),
                ShotBoardBuilder.Green(
                    new Vector2(0f, 1f), 0.05f, "Red", 2, 1, new Vector2Int(9, 9), 0, 0, 0f, false, null,
                    item: ItemType.Laser),
            };
            var workingSet = new ShotBalloonState[board.Length];

            var result = ShotSimulator.Simulate(
                board, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: workingSet,
                items: CreateLaserItemLayer(castRadius: 0.1f, castDistance: 3f, damage: 3, flags: DamageFlags.Normal));

            Assert.AreEqual(1, result.Pops, "only the host pops — the tough merely takes damage");
            Assert.IsFalse(result.BoardCleared);
            Assert.AreEqual(2, workingSet[0].HitsRemaining, "5 - damage(3) = 2 — hit, but survives (self-copy keeps its index stable)");
        }

        [Test]
        public void ApplyEffectHits_LaserOriginOverlap_HitsAnOccupantAlreadyTouchingTheHost()
        {
            // Unity's own CircleCast reports a start-overlap as an immediate hit — mirrored by
            // SegmentHitsCircle's own overlap branch. An occupant placed a negligible distance from
            // the host's own centre is also, incidentally, the "straddles two arms" case the mirror
            // test calls out: since all four arms share the same origin, a point essentially AT that
            // origin is within reach of every arm simultaneously — the per-arm dedup (HitScratch) is
            // what keeps it a SINGLE hit rather than four.
            var board = new[]
            {
                ShotBoardBuilder.Green(
                    new Vector2(0f, 1.0001f), 0.05f, "Blue", 4, 1, new Vector2Int(1, 0), 0, 0, 0f, false, null),
                ShotBoardBuilder.Green(
                    new Vector2(0f, 1f), 0.05f, "Red", 2, 1, new Vector2Int(9, 9), 0, 0, 0f, false, null,
                    item: ItemType.Laser),
            };
            var workingSet = new ShotBalloonState[board.Length];

            var result = ShotSimulator.Simulate(
                board, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: workingSet,
                items: CreateLaserItemLayer(castRadius: 0.3f, castDistance: 1f, damage: 1, flags: DamageFlags.Normal));

            Assert.AreEqual(2, result.Pops, "the overlap-at-origin occupant pops exactly once, not once per arm");
            Assert.IsTrue(result.BoardCleared);
        }

        [Test]
        public void ApplyEffectHits_LaserSpinExtrapolation_DifferentSpinRatesProduceDifferentHitSets()
        {
            // Host radius 0 (a point target) with the default projectileSpeed(1)/no cruise makes tHit
            // exactly 1.0 (distance 1 / speed 1) — so activation.SpinDegrees (host.ItemSpinDegrees(0) +
            // host.ItemSpinRate * tHit) equals the configured spin rate EXACTLY. spin:0 keeps the cross
            // axis-aligned, hitting the target sitting on the +x arm (distance 1, combined radius 0.1:
            // entry 1-0.1=0.9 <= castDistance 1.0). spin:45 rotates every arm 45° off that axis — the
            // target's perpendicular distance from each rotated ray becomes sqrt(0.5)>0.1 (hand-derived:
            // toCenter=(-1,0), |along|=cos45=0.7071 on every arm by symmetry, discriminant =
            // along^2 - |toCenter|^2 + combinedRadius^2 = 0.5 - 1 + 0.01 < 0 on all four — a clean miss),
            // so the SAME board geometry produces a different hit set purely from the spin rate.
            var target = ShotBoardBuilder.Green(
                new Vector2(1f, 1f), 0.05f, "Blue", 3, 1, new Vector2Int(1, 0), 0, 0, 0f, false, null);
            var itemLayer = CreateLaserItemLayer(castRadius: 0.05f, castDistance: 1.0f, damage: 1, flags: DamageFlags.Normal);

            var axisAlignedBoard = new[]
            {
                target,
                ShotBoardBuilder.Green(
                    new Vector2(0f, 1f), 0f, "Red", 2, 1, new Vector2Int(9, 9), 0, 0, 0f, false, null,
                    item: ItemType.Laser, spin: 0f),
            };
            var axisAlignedWorkingSet = new ShotBalloonState[axisAlignedBoard.Length];
            var axisAlignedResult = ShotSimulator.Simulate(
                axisAlignedBoard, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 0,
                projectileContactRadius: 0f, workingSet: axisAlignedWorkingSet, items: itemLayer);

            Assert.AreEqual(2, axisAlignedResult.Pops, "spin 0 keeps the arm on-axis — the target is hit");
            Assert.IsTrue(axisAlignedResult.BoardCleared);

            var rotatedBoard = new[]
            {
                target,
                ShotBoardBuilder.Green(
                    new Vector2(0f, 1f), 0f, "Red", 2, 1, new Vector2Int(9, 9), 0, 0, 0f, false, null,
                    item: ItemType.Laser, spin: 45f),
            };
            var rotatedWorkingSet = new ShotBalloonState[rotatedBoard.Length];
            var rotatedResult = ShotSimulator.Simulate(
                rotatedBoard, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: rotatedWorkingSet, items: CreateLaserItemLayer(castRadius: 0.05f, castDistance: 1.0f, damage: 1, flags: DamageFlags.Normal));

            Assert.AreEqual(1, rotatedResult.Pops, "spin 45 rotates every arm off-axis — the identical target now misses");
            Assert.IsFalse(rotatedResult.BoardCleared);
        }

        [Test]
        public void ApplyEffectHits_LaserRainbowHost_BorderingConversionSkipsToughConvertsGreen()
        {
            // Mirrors LaserItemHandler.ConvertBorderingNeighbors at the loop level (the CORE-level
            // already-hit-exclusion/dedup is pinned directly against both IEffectBoard adapters in
            // EffectBoardMirrorTests.LaserResolve_RainbowHostBorderingConversion_GridAndSimAgreeOnHitSet).
            // The hit target sits at slot (2,3); its hex neighbours (1,3) [paintable Green, parked far
            // off any cast arm at (50,50) so it only ever converts, never gets a direct hit] and (3,3)
            // [non-paintable Tough, parked at (60,60)] are addressed purely by SLOT, independent of
            // their (deliberately off-axis) positions.
            var board = new[]
            {
                ShotBoardBuilder.Green(
                    new Vector2(1f, 1f), 0.05f, "Blue", 1, 1, new Vector2Int(2, 3), 0, 0, 0f, false, null),
                ShotBoardBuilder.Green(
                    new Vector2(50f, 50f), 0.05f, "Green", 1, 1, new Vector2Int(1, 3), 0, 0, 0f, false, null),
                ShotBoardBuilder.Tough(
                    new Vector2(60f, 60f), 0.05f, 1, 5, new Vector2Int(3, 3), 0, 0, 0f, false, null),
                ShotBoardBuilder.Rainbow(
                    new Vector2(0f, 1f), 0.05f, GamePalette.RainbowColorId, 1, 1, new Vector2Int(5, 5), 0, 0, 0f,
                    false, null, item: ItemType.Laser),
            };
            var workingSet = new ShotBalloonState[board.Length];

            var result = ShotSimulator.Simulate(
                board, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: workingSet,
                items: CreateLaserItemLayer(
                    castRadius: 0.1f, castDistance: 2f, damage: 1, flags: DamageFlags.Normal,
                    rainbowColorId: GamePalette.RainbowColorId),
                rainbowColorId: GamePalette.RainbowColorId, allowedColors: new[] { "Blue" });

            Assert.AreEqual(2, result.Pops, "host + the one right-arm target; both neighbours survive untouched by geometry");
            Assert.IsFalse(result.BoardCleared);

            var green = FindByPosition(workingSet, new Vector2(50f, 50f));
            Assert.IsTrue(green.IsRainbow, "the surviving paintable neighbour converts");
            Assert.AreEqual(GamePalette.RainbowColorId, green.ColorId);

            var tough = FindByPosition(workingSet, new Vector2(60f, 60f));
            Assert.IsFalse(tough.IsRainbow, "a non-paintable (tough) neighbour is never converted");
        }

        // Entries a pop's own swap-remove leaves reordered are still findable by their (untouched)
        // world position — a plain linear scan, since this test only cares about the two survivors'
        // final colour state, not their array index.
        private static ShotBalloonState FindByPosition(ShotBalloonState[] workingSet, Vector2 position)
        {
            for (var i = 0; i < workingSet.Length; i++)
            {
                if (workingSet[i].Position == position)
                {
                    return workingSet[i];
                }
            }

            Assert.Fail($"no working-set entry at {position}");
            return default;
        }

        [Test]
        public void ApplyEffectHits_LaserArmLengthBoundary_JustInsideHitsJustOutsideSurvives()
        {
            // castRadius 0 + the target's own 0 radius = a point target, so the entry distance equals
            // the raw offset exactly — a clean ±0.01 margin around castDistance 1.0, same convention as
            // BombBlast's own radius-boundary test.
            var insideBoard = new[]
            {
                ShotBoardBuilder.Green(
                    new Vector2(0.99f, 1f), 0f, "Blue", 3, 1, new Vector2Int(1, 0), 0, 0, 0f, false, null),
                ShotBoardBuilder.Green(
                    new Vector2(0f, 1f), 0f, "Red", 2, 1, new Vector2Int(9, 9), 0, 0, 0f, false, null,
                    item: ItemType.Laser),
            };
            var insideWorkingSet = new ShotBalloonState[insideBoard.Length];
            var insideResult = ShotSimulator.Simulate(
                insideBoard, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: insideWorkingSet,
                items: CreateLaserItemLayer(castRadius: 0f, castDistance: 1.0f, damage: 1, flags: DamageFlags.Normal));

            Assert.AreEqual(2, insideResult.Pops, "distance 0.99 is inside castDistance 1.0 — the target dies");
            Assert.IsTrue(insideResult.BoardCleared);

            var outsideBoard = new[]
            {
                ShotBoardBuilder.Green(
                    new Vector2(1.01f, 1f), 0f, "Blue", 3, 1, new Vector2Int(1, 0), 0, 0, 0f, false, null),
                ShotBoardBuilder.Green(
                    new Vector2(0f, 1f), 0f, "Red", 2, 1, new Vector2Int(9, 9), 0, 0, 0f, false, null,
                    item: ItemType.Laser),
            };
            var outsideWorkingSet = new ShotBalloonState[outsideBoard.Length];
            var outsideResult = ShotSimulator.Simulate(
                outsideBoard, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: outsideWorkingSet,
                items: CreateLaserItemLayer(castRadius: 0f, castDistance: 1.0f, damage: 1, flags: DamageFlags.Normal));

            Assert.AreEqual(1, outsideResult.Pops, "distance 1.01 is outside castDistance 1.0 — no hit at all");
            Assert.IsFalse(outsideResult.BoardCleared);
        }
    }
}
