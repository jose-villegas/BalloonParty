using System.Collections.Generic;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Palette;
using BalloonParty.Item.Effects;
using BalloonParty.Item.Paint;
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

        // Phase C4 (Lightning): a real Lightning config entry — same one-item-type-worth-of-settings
        // convention as CreateBombItemLayer/CreateLaserItemLayer above. UNLIKE Bomb/Laser (which read
        // the occupant's live/time-evaluated Position and never touch SlotPosition), Lightning's
        // selection sorts over the LATTICE SlotPosition (topology, not physical overlap) — a
        // default(ShotSlotLattice) collapses every occupant's SlotPosition to the same point (zero
        // Separation), which would silently defeat every distance-ordering assertion below. A real
        // 2000x2000 lattice comfortably covers every SlotIndex these tests use, for both the chain's
        // own sort and FindNearestConcreteColor's ring-walk SearchRadius.
        private static ShotItemLayer CreateLightningItemLayer(
            int damage, DamageFlags flags = DamageFlags.Normal, string rainbowColorId = null)
        {
            var effectParams = new Dictionary<ItemType, ItemEffectParams>
            {
                {
                    ItemType.Lightning,
                    new ItemEffectParams(
                        default, default, new LightningEffectParams(0f, 0f, 0f), default, default, damage, flags)
                },
            };
            var lattice = new ShotSlotLattice(new Vector2(1f, 1f), Vector2.zero, 2000, 2000);
            return new ShotItemLayer(effectParams, in lattice, rainbowColorId);
        }

        // Phase C5 (Paint): a real Paint config entry — same one-item-type-worth-of-settings
        // convention as the sibling factories above. UNLIKE Bomb/Laser, Paint's selection sorts over
        // the LATTICE SlotPosition (topology, not physical overlap — same reasoning as Lightning's own
        // comment), so a real lattice matters here too; an optional override lets a boundary test tune
        // Separation/Offset so a convenient slot lands at an EXACT distance from a packed blob (see
        // ShotSlotLattice.SlotPosition's formula) instead of only "within" a coarse unit grid.
        private static ShotItemLayer CreatePaintItemLayer(
            float spreadOffset, float spreadLength, float spreadBaseWidth, float spreadBlobRadius,
            ShotSlotLattice? lattice = null, int damage = 1, DamageFlags flags = DamageFlags.Normal)
        {
            var effectParams = new Dictionary<ItemType, ItemEffectParams>
            {
                {
                    ItemType.Paint,
                    new ItemEffectParams(
                        default, default, default,
                        new PaintEffectParams(spreadOffset, spreadLength, spreadBaseWidth, spreadBlobRadius), default,
                        damage, flags)
                },
            };
            var resolvedLattice = lattice ?? new ShotSlotLattice(new Vector2(1f, 1f), Vector2.zero, 2000, 2000);
            return new ShotItemLayer(effectParams, in resolvedLattice);
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

        [Test]
        public void ApplyEffectHits_LightningNormalHost_DistanceOrderedChainClimbsTheStreakSequentially()
        {
            // Same-row slots (0,0)/(1,0)/(3,0)/(6,0), sep=(1,1)/offset=0: hIndex = 2*col (row 0 is
            // even), so world.x = 2*col - 0.5 — distances from the host's slot are 2/6/12, strictly
            // increasing (Near < Mid < Far), independent of the targets' (deliberately far off-ray)
            // physical Position, which only keeps them out of the projectile's own path.
            //
            // Hand-trace: host (normal, non-rainbow) pops via direct contact first — adopts
            // ProjectileColor "Red", RecordColor("Red") on a fresh streak (0 -> 1): score 1000*1=1000.
            // The chain then pops Near/Mid/Far, in that DISTANCE order, each via an ITEM cause
            // (RecordColor("Red") — same colour as the just-adopted streak, so it just keeps
            // climbing, never resets): Near 1*2=2, Mid 10*3=30, Far 100*4=400. Total
            // 1000+2+30+400=1432 — a DIFFERENT distance order would climb the SAME multipliers against
            // DIFFERENT scoreValues, producing a different total, so this total is itself proof the
            // chain resolved nearest-first.
            var board = new[]
            {
                ShotBoardBuilder.Green(
                    new Vector2(0f, 1f), 0.05f, "Red", 1000, 1, new Vector2Int(0, 0), 0, 0, 0f, false, null,
                    item: ItemType.Lightning),
                ShotBoardBuilder.Green(
                    new Vector2(50f, 50f), 0.05f, "Red", 1, 1, new Vector2Int(1, 0), 0, 0, 0f, false, null),
                ShotBoardBuilder.Green(
                    new Vector2(60f, 60f), 0.05f, "Red", 10, 1, new Vector2Int(3, 0), 0, 0, 0f, false, null),
                ShotBoardBuilder.Green(
                    new Vector2(70f, 70f), 0.05f, "Red", 100, 1, new Vector2Int(6, 0), 0, 0, 0f, false, null),
            };
            var workingSet = new ShotBalloonState[board.Length];

            var result = ShotSimulator.Simulate(
                board, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: workingSet, items: CreateLightningItemLayer(damage: 1));

            Assert.AreEqual(4, result.Pops);
            Assert.AreEqual(1000 + 2 + 30 + 400, result.RawScore);
            Assert.IsTrue(result.BoardCleared);
        }

        [Test]
        public void ApplyEffectHits_LightningRainbowHost_ConvertsTheColourGroupInsteadOfPopping()
        {
            // A seeded "Red" projectile colour (mirrors an earlier real pop, standing in for one here so
            // the matchColor precedence resolves DIRECTLY off the projectile's own colour, not the
            // FindNearestConcreteColor fallback — that fallback gets its own dedicated test below) — a
            // rainbow host converts every same-coloured occupant to rainbow instead of destroying it;
            // Pops must stay at exactly 1 (the host's own pop only), and a colourless Tough among the
            // matched slot set must never be touched (it never satisfies the ColorId equality gate at
            // all, since a Tough has no colour).
            var board = new[]
            {
                ShotBoardBuilder.Rainbow(
                    new Vector2(0f, 1f), 0.05f, GamePalette.RainbowColorId, 50, 1, new Vector2Int(10, 10), 0, 0, 0f,
                    false, null, item: ItemType.Lightning),
                ShotBoardBuilder.Green(
                    new Vector2(50f, 50f), 0.05f, "Red", 1, 1, new Vector2Int(11, 10), 0, 0, 0f, false, null),
                ShotBoardBuilder.Green(
                    new Vector2(60f, 60f), 0.05f, "Red", 1, 1, new Vector2Int(13, 10), 0, 0, 0f, false, null),
                ShotBoardBuilder.Tough(
                    new Vector2(70f, 70f), 0.05f, 1, 5, new Vector2Int(15, 10), 0, 0, 0f, false, null),
            };
            var workingSet = new ShotBalloonState[board.Length];

            var result = ShotSimulator.Simulate(
                board, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: workingSet,
                items: CreateLightningItemLayer(damage: 1, rainbowColorId: GamePalette.RainbowColorId),
                rainbowColorId: GamePalette.RainbowColorId, allowedColors: new[] { "Red" },
                seed: ShotFlightSeed.Fresh(projectileColor: "Red"));

            Assert.AreEqual(1, result.Pops, "only the rainbow host itself pops — the chain converts, never destroys");
            Assert.IsFalse(result.BoardCleared);

            var first = FindByPosition(workingSet, new Vector2(50f, 50f));
            Assert.IsTrue(first.IsRainbow, "the matched Red occupant converts to rainbow");
            Assert.AreEqual(GamePalette.RainbowColorId, first.ColorId);

            var second = FindByPosition(workingSet, new Vector2(60f, 60f));
            Assert.IsTrue(second.IsRainbow, "the SECOND matched Red occupant converts too — the whole group, not just the nearest");
            Assert.AreEqual(GamePalette.RainbowColorId, second.ColorId);

            var tough = FindByPosition(workingSet, new Vector2(70f, 70f));
            Assert.IsFalse(tough.IsRainbow, "a colourless Tough never satisfies the ColorId match gate — untouched");
            Assert.AreEqual(5, tough.HitsRemaining, "untouched means untouched — durability unchanged too");
        }

        [Test]
        public void ApplyEffectHits_LightningRainbowHostColorlessProjectile_FallsBackToNearestConcreteColor()
        {
            // No seed this time — the rainbow host IS the flight's very first contact, so
            // state.ProjectileColor is still empty when RunItemEffects reads it (rainbow adoption never
            // touches ProjectileColor either — see ResolvePopScore's own adoption guard). That empties
            // matchColor's primary source, forcing the LightningChain.FindNearestConcreteColor fallback
            // (mirrors LightningItemHandler.cs:96-100's own SlotGrid.FindNearestColorId call).
            // HexCoordinates.HexNeighborIndices(5,5) (row 5 is odd, shiftedCol=6) gives the host's ring-1
            // neighbours as {(4,5),(6,5),(5,4),(6,4),(5,6),(6,6)} — (4,5) is the ONLY concrete-coloured
            // occupant among them ("Green"), so the ring walk returns "Green" deterministically
            // regardless of its own internal side-traversal order. (2,5) sits further out (not a ring-1
            // neighbour of (5,5) at all) yet still shares that SAME "Green" colour, proving the fallback
            // colour propagates to the WHOLE chain selection, not just the ring-found anchor itself; the
            // "Blue" occupant proves a different colour is excluded from both the fallback and the chain.
            var board = new[]
            {
                ShotBoardBuilder.Rainbow(
                    new Vector2(0f, 1f), 0.05f, GamePalette.RainbowColorId, 20, 1, new Vector2Int(5, 5), 0, 0, 0f,
                    false, null, item: ItemType.Lightning),
                ShotBoardBuilder.Green(
                    new Vector2(50f, 50f), 0.05f, "Green", 1, 1, new Vector2Int(4, 5), 0, 0, 0f, false, null),
                ShotBoardBuilder.Green(
                    new Vector2(60f, 60f), 0.05f, "Green", 1, 1, new Vector2Int(2, 5), 0, 0, 0f, false, null),
                ShotBoardBuilder.Green(
                    new Vector2(70f, 70f), 0.05f, "Blue", 1, 1, new Vector2Int(0, 0), 0, 0, 0f, false, null),
            };
            var workingSet = new ShotBalloonState[board.Length];

            var result = ShotSimulator.Simulate(
                board, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: workingSet,
                items: CreateLightningItemLayer(damage: 1, rainbowColorId: GamePalette.RainbowColorId),
                rainbowColorId: GamePalette.RainbowColorId, allowedColors: new[] { "Green" });

            Assert.AreEqual(1, result.Pops, "only the rainbow host pops — the fallback-resolved colour still converts, never destroys");

            var near = FindByPosition(workingSet, new Vector2(50f, 50f));
            Assert.IsTrue(near.IsRainbow, "the ring-1 neighbour that seeded the fallback converts");
            Assert.AreEqual(GamePalette.RainbowColorId, near.ColorId);

            var far = FindByPosition(workingSet, new Vector2(60f, 60f));
            Assert.IsTrue(far.IsRainbow, "a same-coloured occupant OUTSIDE ring 1 converts too — the fallback colour, not just its anchor slot");
            Assert.AreEqual(GamePalette.RainbowColorId, far.ColorId);

            var blue = FindByPosition(workingSet, new Vector2(70f, 70f));
            Assert.IsFalse(blue.IsRainbow, "a different colour never matches the fallback-resolved \"Green\"");
            Assert.AreEqual("Blue", blue.ColorId);
        }

        [Test]
        public void ApplyEffectHits_LightningRainbowHostRainbowProjectileColor_FallsBackToNearestConcreteColor()
        {
            // The OTHER half of ResolveLightning's rainbow-host guard — the sibling test above covers
            // the `IsNullOrEmpty(projectileColorId)` disjunct; this one covers the SECOND disjunct
            // (`!string.Equals(projectileColorId, _rainbowColorId)`). Here the seeded "active
            // projectile" colour is itself the rainbow MARKER (mirrors a rainbow projectile having
            // been the last one loaded) — non-empty, so it would wrongly pass the first disjunct on
            // its own, but must still be rejected because it equals the rainbow marker, forcing the
            // SAME LightningChain.FindNearestConcreteColor fallback an empty colour would (same
            // ring-1 cluster/derivation as the sibling test above).
            var board = new[]
            {
                ShotBoardBuilder.Rainbow(
                    new Vector2(0f, 1f), 0.05f, GamePalette.RainbowColorId, 20, 1, new Vector2Int(5, 5), 0, 0, 0f,
                    false, null, item: ItemType.Lightning),
                ShotBoardBuilder.Green(
                    new Vector2(50f, 50f), 0.05f, "Green", 1, 1, new Vector2Int(4, 5), 0, 0, 0f, false, null),
                ShotBoardBuilder.Green(
                    new Vector2(70f, 70f), 0.05f, "Blue", 1, 1, new Vector2Int(0, 0), 0, 0, 0f, false, null),
            };
            var workingSet = new ShotBalloonState[board.Length];

            var result = ShotSimulator.Simulate(
                board, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: workingSet,
                items: CreateLightningItemLayer(damage: 1, rainbowColorId: GamePalette.RainbowColorId),
                rainbowColorId: GamePalette.RainbowColorId, allowedColors: new[] { "Green" },
                seed: ShotFlightSeed.Fresh(projectileColor: GamePalette.RainbowColorId));

            Assert.AreEqual(1, result.Pops, "only the rainbow host pops — the fallback-resolved colour still converts, never destroys");

            var near = FindByPosition(workingSet, new Vector2(50f, 50f));
            Assert.IsTrue(
                near.IsRainbow,
                "a rainbow-coloured seeded projectile colour must still trigger the fallback, never get used directly as matchColorId");
            Assert.AreEqual(GamePalette.RainbowColorId, near.ColorId);

            var blue = FindByPosition(workingSet, new Vector2(70f, 70f));
            Assert.IsFalse(blue.IsRainbow, "a different colour never matches the fallback-resolved \"Green\"");
            Assert.AreEqual("Blue", blue.ColorId);
        }

        [Test]
        public void ApplyEffectHits_LightningHost_ChainNeverReSelectsTheAlreadyPoppedHost()
        {
            // The host's own colour equals matchColorId for a normal host — if the effect board (or the
            // chain itself) ever re-included the already-popped host, it would take a SECOND Damage hit
            // on top of its own direct-contact pop, double-counting Pops. ShotSimEffectBoard.Bind
            // excludes the host's slot up front, and by the time RunItemEffects runs the host is already
            // swap-removed from the active working set regardless (ShotSimulator.ResolveBalloonContact's
            // own ordering) — a single same-colour chain target proves the count stays exactly 2 (host +
            // the one real target), never 3.
            var board = new[]
            {
                ShotBoardBuilder.Green(
                    new Vector2(0f, 1f), 0.05f, "Red", 10, 1, new Vector2Int(5, 5), 0, 0, 0f, false, null,
                    item: ItemType.Lightning),
                ShotBoardBuilder.Green(
                    new Vector2(50f, 50f), 0.05f, "Red", 1, 1, new Vector2Int(7, 5), 0, 0, 0f, false, null),
            };
            var workingSet = new ShotBalloonState[board.Length];

            var result = ShotSimulator.Simulate(
                board, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: workingSet, items: CreateLightningItemLayer(damage: 1));

            Assert.AreEqual(
                2, result.Pops,
                "host + the single chain target — never a phantom third pop from the host re-selecting itself");
            Assert.IsTrue(result.BoardCleared);
        }

        [Test]
        public void ApplyEffectHits_LightningChainExhaustsBudget_LaterChainedCarrierGrantNeverFires()
        {
            // Cross-item chain (@ref plan_shot_solver_accuracy Phase C2 memo's own term) exercised for
            // Lightning specifically: unlike Bomb's physical-radius hop-by-hop domino chain, a NORMAL
            // Lightning host's own re-chained activation always re-searches its OWN (already-exhausted)
            // colour — a same-colour Lightning sweep hits EVERY matching occupant in ONE Resolve() call,
            // so Damage APPLICATION itself is never budget-gated; only each hit's OWN chained
            // continuation (PopItemHit's TryBeginActivation call) is. 31 filler "Red" Lightning-carriers
            // are placed NEARER (lower SlotIndex column) than a trailing Shield-carrying "Red" occupant,
            // so the fillers are always processed FIRST in the chain's nearest-first order — exactly
            // exhausting the budget (host's own activation begins the count at 1; the 31 fillers'
            // chained activations bring it to exactly 32) before the Shield-carrier's own hit is even
            // applied. TryBeginActivation rejects the Shield-carrier's chained activation outright
            // (count already at 32), so its own +1 grant never fires — even though the Shield-carrier
            // itself still POPS (popping happens unconditionally, before the chain-continuation check).
            // A tight, close right wall with 0 starting shields is the only way to OBSERVE the missing
            // grant: with it, the wall bounce would have survived; without it, the very first bounce
            // kills the flight before the trailing "Blue" filler (kept off the flight's ray, at x<0) is
            // ever reached — proving BoardCleared stays false and the grant truly never applied.
            const int fillerCount = 31;
            var walls = new Vector4(1000f, 1.5f, -1000f, -1000f);
            var board = new ShotBalloonSnapshot[1 + fillerCount + 1 + 1];
            board[0] = ShotBoardBuilder.Green(
                new Vector2(0.5f, 0f), 0.05f, "Red", 1, 1, new Vector2Int(0, 0), 0, 0, 0f, false, null,
                item: ItemType.Lightning);

            for (var k = 0; k < fillerCount; k++)
            {
                board[1 + k] = ShotBoardBuilder.Green(
                    new Vector2(500f + k, 500f), 0.05f, "Red", 1, 1, new Vector2Int(1 + k, 0), 0, 0, 0f, false, null,
                    item: ItemType.Lightning);
            }

            // Slot column 1000 guarantees this sorts LAST (farthest) in the chain's nearest-first order,
            // however many fillers precede it.
            board[1 + fillerCount] = ShotBoardBuilder.Green(
                new Vector2(9000f, 9000f), 0.05f, "Red", 1, 1, new Vector2Int(1000, 0), 0, 0, 0f, false, null,
                item: ItemType.Shield);

            // Off the flight's ray (negative x, the shot travels +x) and only reachable AFTER a
            // surviving wall bounce reflects it back — the one observable the missing grant denies.
            board[board.Length - 1] = ShotBoardBuilder.Green(new Vector2(-0.5f, 0f), 0.1f, "Blue", 1, 1);

            var workingSet = new ShotBalloonState[board.Length];
            var result = ShotSimulator.Simulate(
                board, walls, Vector2.zero, Vector2.right, startingShields: 0, projectileContactRadius: 0f,
                workingSet: workingSet, items: CreateLightningItemLayer(damage: 1));

            Assert.AreEqual(
                1 + fillerCount + 1, result.Pops,
                "every Red occupant pops in the host's single sweep — Lightning's own hit APPLICATION is never budget-gated, only each hit's own chained continuation is");
            Assert.IsTrue(
                result.Died,
                "the Shield-carrier's own chained activation was rejected by the exhausted budget, so its +1 grant never applied — 0 shields dies on the very first bounce");
            Assert.IsFalse(result.BoardCleared, "the trailing Blue filler, past the fatal bounce, is never reached");
        }

        [Test]
        public void ApplyEffectHits_PaintRecolorsDownstreamBalloon_LaterPopScoresUnderTheNewColour()
        {
            // THE headline case (@ref plan_shot_solver_accuracy Phase C5's "paint recolors the balloon
            // the shot is already flying toward"): the Paint host pops FIRST (adopts "Red", streak 1),
            // recolouring the downstream balloon (originally "Blue") to "Red" via ApplyEffectHits'
            // EXISTING Recolor branch — BEFORE the ray's own continuing flight resolves the downstream
            // contact. A stale snapshot (the recolour landing too late) would have the downstream pop
            // still see "Blue", adopt IT instead, and RESET the streak to 1; recolouring in time
            // instead EXTENDS the "Red" streak to 2. The two outcomes diverge by 2x (201 vs 101), so
            // this total is itself the proof — no need to separately re-run a "buggy" variant.
            //
            // Host at (0,1), downstream at (0,2): SpreadOffset 0 + SpreadLength == SpreadBlobRadius
            // collapses PaintTriangle.PackBlobs to exactly ONE blob, landing at host.Position + up*1 =
            // (0,2). The downstream's LATTICE position (-0.5,2) — slot (0,-2), sep=(1,1)/offset=0, see
            // ShotSlotLattice.SlotPosition's formula — sits 0.5 from that blob, inside the 1.0 radius.
            const float r = 1.0f;
            var board = new[]
            {
                ShotBoardBuilder.Green(
                    new Vector2(0f, 1f), 0.05f, "Red", 1, 1, new Vector2Int(50, 50), 0, 0, 0f, false, null,
                    item: ItemType.Paint),
                ShotBoardBuilder.Green(
                    new Vector2(0f, 2f), 0.05f, "Blue", 100, 1, new Vector2Int(0, -2), 0, 0, 0f, false, null),
            };
            var workingSet = new ShotBalloonState[board.Length];

            var result = ShotSimulator.Simulate(
                board, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: workingSet,
                items: CreatePaintItemLayer(spreadOffset: 0f, spreadLength: r, spreadBaseWidth: 4f * r, spreadBlobRadius: r));

            Assert.AreEqual(2, result.Pops);
            Assert.IsTrue(result.BoardCleared);
            Assert.AreEqual(
                (1 * 1) + (100 * 2), result.RawScore,
                "the downstream pop must score under the NEW colour (\"Red\", streak 2) — the stale \"Blue\" would instead score 100*1=100, total 101");
        }

        [Test]
        public void ApplyEffectHits_PaintRainbowHost_PaintsDownstreamRainbowWhichThenScoresViaTheRainbowBranch()
        {
            // A rainbow host's colour IS the rainbow marker (PaintItemHandler.cs:63-71) — the SAME
            // recolour path Paint always runs converts a normal downstream balloon to rainbow too. A
            // real coloured seed pop establishes a non-null, non-rainbow ProjectileColor FIRST, so
            // neither the host's own pop nor the downstream's takes the DEFERRED branch (see
            // ResolvePopScore's isRainbowTargetDeferred guard) — letting the downstream's LATER pop
            // exercise the ordinary rainbow-attribution branch (item 4 in ResolvePopScore, not
            // Wildcard/Deferred): primary = allowed("Green") -> RecordColor("Green"), extending the
            // SAME streak the seed and host built.
            //
            // Hand-trace: seed (5*streak1=5) -> host (rainbow, primary "Green", streak1->2: 10*2=20) ->
            // paint converts downstream to rainbow -> downstream (rainbow, primary "Green",
            // streak2->3: 100*3=300). Total 5+20+300=325.
            const float r = 1.0f;
            var board = new[]
            {
                ShotBoardBuilder.Green(
                    new Vector2(0f, 0.5f), 0.05f, "Green", 5, 1, new Vector2Int(100, 100), 0, 0, 0f, false, null),
                ShotBoardBuilder.Rainbow(
                    new Vector2(0f, 1f), 0.05f, GamePalette.RainbowColorId, 10, 1, new Vector2Int(50, 50), 0, 0, 0f,
                    false, null, item: ItemType.Paint),
                ShotBoardBuilder.Green(
                    new Vector2(0f, 2f), 0.05f, "Blue", 100, 1, new Vector2Int(0, -2), 0, 0, 0f, false, null),
            };
            var workingSet = new ShotBalloonState[board.Length];

            var result = ShotSimulator.Simulate(
                board, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: workingSet,
                items: CreatePaintItemLayer(spreadOffset: 0f, spreadLength: r, spreadBaseWidth: 4f * r, spreadBlobRadius: r),
                rainbowColorId: GamePalette.RainbowColorId, allowedColors: new[] { "Green" });

            Assert.AreEqual(3, result.Pops);
            Assert.IsTrue(result.BoardCleared);
            Assert.AreEqual(
                5 + 20 + 300, result.RawScore,
                "the painted-rainbow downstream must score via the rainbow attribution branch, not an ordinary colour pop");
        }

        [Test]
        public void ApplyEffectHits_PaintGreenOnRainbowTarget_ClearsIsRainbow()
        {
            // The other direction of the C5 headline pair: a normal (non-rainbow) host paints an
            // ALREADY-rainbow survivor back to an ordinary colour — TryClassify accepts any IPaintable
            // whose colour differs from the paint colour, rainbow included, and ApplyRecolor's plain
            // assignment clears IsRainbow with no separate branch (mirrors ApplyColorChange writing
            // IHasColor.Color.Value directly, live-side). The target sits off the flight's ray entirely
            // (only ever reachable via Paint) so it survives to inspect afterward.
            const float r = 1.0f;
            var board = new[]
            {
                ShotBoardBuilder.Green(
                    new Vector2(0f, 1f), 0.05f, "Green", 2, 1, new Vector2Int(50, 50), 0, 0, 0f, false, null,
                    item: ItemType.Paint),
                ShotBoardBuilder.Rainbow(
                    new Vector2(300f, 300f), 0.05f, GamePalette.RainbowColorId, 1, 1, new Vector2Int(0, -2), 0, 0,
                    0f, false, null),
            };
            var workingSet = new ShotBalloonState[board.Length];

            var result = ShotSimulator.Simulate(
                board, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: workingSet,
                items: CreatePaintItemLayer(spreadOffset: 0f, spreadLength: r, spreadBaseWidth: 4f * r, spreadBlobRadius: r),
                rainbowColorId: GamePalette.RainbowColorId);

            Assert.AreEqual(1, result.Pops, "only the host pops — the target survives, reachable only via paint");
            Assert.IsFalse(result.BoardCleared);

            var target = FindByPosition(workingSet, new Vector2(300f, 300f));
            Assert.IsFalse(target.IsRainbow, "green paint clears IsRainbow on a previously-rainbow target");
            Assert.AreEqual("Green", target.ColorId);
        }

        [Test]
        public void ApplyEffectHits_ChainedPaintActivation_FansUpwardRegardlessOfTheShotsRealDirection()
        {
            // A CHAINED activation (a popped item's own item pop, not a direct hit) always passes
            // Vector2.zero as ProjectileDirection (@ref plan_shot_solver_accuracy Phase C §3's
            // ShotItemActivation doc) — PaintTriangle.Build's own direction fallback treats that as
            // "fan upward", regardless of the shot's REAL travel direction. The shot here travels
            // along +x (RIGHT) so "up" is geometrically distinguishable from it: a Bomb-carrying host
            // pops a Paint-carrying balloon B (off the flight's ray, only ever reached via the blast),
            // and B's own chained Paint activation must fan UP from B's position, not rightward along
            // the shot's actual travel.
            //
            // A single blob lands at B.Position(5,5) + up*1 = (5,6) (SpreadOffset 0 + SpreadLength ==
            // SpreadBlobRadius, same one-blob recipe as the other Paint tests). Target T's LATTICE
            // position is placed at (5.5,6) via slot (3,-6) — distance 0.5 from that blob, inside the
            // 1.0 radius. Had the fan wrongly used the shot's real direction (right) instead, the blob
            // would land at (6,5) — distance ~1.118 from T, OUTSIDE the radius — so T recolouring at
            // all is itself the proof the zero-direction fallback fired.
            const float r = 1.0f;
            var effectParams = new Dictionary<ItemType, ItemEffectParams>
            {
                {
                    ItemType.Bomb,
                    new ItemEffectParams(
                        new BombEffectParams(10f, rainbowEffectScale: 1f, rainbowConversionRange: 0f), default,
                        default, default, default, 1, DamageFlags.Normal)
                },
                {
                    ItemType.Paint,
                    new ItemEffectParams(
                        default, default, default, new PaintEffectParams(0f, r, 4f * r, r), default, 1,
                        DamageFlags.Normal)
                },
            };
            var lattice = new ShotSlotLattice(new Vector2(1f, 1f), Vector2.zero, 2000, 2000);
            var items = new ShotItemLayer(effectParams, in lattice);

            var board = new[]
            {
                ShotBoardBuilder.Green(
                    new Vector2(1f, 0f), 0.05f, "Yellow", 1, 1, new Vector2Int(0, 0), 0, 0, 0f, false, null,
                    item: ItemType.Bomb),
                ShotBoardBuilder.Green(
                    new Vector2(5f, 5f), 0.05f, "Red", 1, 1, new Vector2Int(99, 99), 0, 0, 0f, false, null,
                    item: ItemType.Paint),
                ShotBoardBuilder.Green(
                    new Vector2(400f, 400f), 0.05f, "Blue", 1, 1, new Vector2Int(3, -6), 0, 0, 0f, false, null),
            };
            var workingSet = new ShotBalloonState[board.Length];

            var result = ShotSimulator.Simulate(
                board, WideOpenWalls, Vector2.zero, Vector2.right, startingShields: 0, projectileContactRadius: 0f,
                workingSet: workingSet, items: items);

            Assert.AreEqual(2, result.Pops, "host (direct) and B (bomb-triggered) pop; T survives untouched by either");
            Assert.IsFalse(result.BoardCleared);

            var target = FindByPosition(workingSet, new Vector2(400f, 400f));
            Assert.AreEqual(
                "Red", target.ColorId,
                "the chained paint fanned UP from B's position, not along the shot's real rightward travel");
        }

        [Test]
        public void ApplyEffectHits_PaintBlobRadiusBoundary_JustInsideRecolorsJustOutsideSurvives()
        {
            // Host at (0,1) firing straight up: SpreadOffset 0 + SpreadLength == SpreadBlobRadius (1.0)
            // collapses PaintTriangle.PackBlobs to exactly ONE blob, landing at host.Position + up*1 =
            // (0,2) (same derivation as the other Paint tests). The target's LATTICE position (its real
            // Position sits off the ray entirely) is placed at EXACTLY blobRadius +/- 0.01 from that
            // blob by tuning the lattice's own Separation.x/Offset so slot (0,0) lands there precisely:
            // ShotSlotLattice.SlotPosition's formula gives slot (0,0) at (-Separation.x/2, Offset.y)
            // when Offset.x is 0.
            const float r = 1.0f;

            var insideLattice = new ShotSlotLattice(new Vector2(-2f * (r - 0.01f), 1f), new Vector2(0f, 2f), 10, 10);
            var insideBoard = new[]
            {
                ShotBoardBuilder.Green(
                    new Vector2(0f, 1f), 0.05f, "Yellow", 2, 1, new Vector2Int(50, 50), 0, 0, 0f, false, null,
                    item: ItemType.Paint),
                ShotBoardBuilder.Green(
                    new Vector2(200f, 200f), 0.05f, "Blue", 1, 1, new Vector2Int(0, 0), 0, 0, 0f, false, null),
            };
            var insideWorkingSet = new ShotBalloonState[insideBoard.Length];
            var insideResult = ShotSimulator.Simulate(
                insideBoard, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: insideWorkingSet,
                items: CreatePaintItemLayer(
                    spreadOffset: 0f, spreadLength: r, spreadBaseWidth: 4f * r, spreadBlobRadius: r,
                    lattice: insideLattice));

            Assert.AreEqual(1, insideResult.Pops, "only the host pops — the target is off the ray, only ever reachable via paint");
            var insideTarget = FindByPosition(insideWorkingSet, new Vector2(200f, 200f));
            Assert.AreEqual("Yellow", insideTarget.ColorId, "distance r-0.01 sits inside the blob radius — the target recolors");

            var outsideLattice = new ShotSlotLattice(new Vector2(-2f * (r + 0.01f), 1f), new Vector2(0f, 2f), 10, 10);
            var outsideBoard = new[]
            {
                ShotBoardBuilder.Green(
                    new Vector2(0f, 1f), 0.05f, "Yellow", 2, 1, new Vector2Int(50, 50), 0, 0, 0f, false, null,
                    item: ItemType.Paint),
                ShotBoardBuilder.Green(
                    new Vector2(200f, 200f), 0.05f, "Blue", 1, 1, new Vector2Int(0, 0), 0, 0, 0f, false, null),
            };
            var outsideWorkingSet = new ShotBalloonState[outsideBoard.Length];
            var outsideResult = ShotSimulator.Simulate(
                outsideBoard, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: outsideWorkingSet,
                items: CreatePaintItemLayer(
                    spreadOffset: 0f, spreadLength: r, spreadBaseWidth: 4f * r, spreadBlobRadius: r,
                    lattice: outsideLattice));

            Assert.AreEqual(1, outsideResult.Pops);
            var outsideTarget = FindByPosition(outsideWorkingSet, new Vector2(200f, 200f));
            Assert.AreEqual("Blue", outsideTarget.ColorId, "distance r+0.01 sits outside the blob radius — no hit, no recolor");
        }

        [Test]
        public void ResolveBalloonContact_PaintCarryingBoard_ItemsNullOrConfigMissing_MatchesPlainBoard()
        {
            // The Paint-specific fast-path lock (mirrors the Shield/Bomb locks above) — a
            // Paint-carrying host that never gets to activate (items:null) or whose item layer has no
            // Paint entry in its config dict must behave byte-identically to a board with no item at
            // all.
            var plainBoard = new[]
            {
                ShotBoardBuilder.Green(new Vector2(0f, 1f), 0.05f, "Red", 3, 1),
                ShotBoardBuilder.Green(new Vector2(0f, 2f), 0.05f, "Blue", 1, 1),
            };
            var paintBoard = new[]
            {
                ShotBoardBuilder.Green(new Vector2(0f, 1f), 0.05f, "Red", 3, 1, item: ItemType.Paint),
                ShotBoardBuilder.Green(new Vector2(0f, 2f), 0.05f, "Blue", 1, 1),
            };

            var plainWorkingSet = new ShotBalloonState[plainBoard.Length];
            var plainResult = ShotSimulator.Simulate(
                plainBoard, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: plainWorkingSet);

            var itemsNullWorkingSet = new ShotBalloonState[paintBoard.Length];
            var itemsNullResult = ShotSimulator.Simulate(
                paintBoard, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: itemsNullWorkingSet);

            var missingConfigWorkingSet = new ShotBalloonState[paintBoard.Length];
            var missingConfigResult = ShotSimulator.Simulate(
                paintBoard, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: missingConfigWorkingSet, items: CreateItemLayer());

            AssertResultsMatch(plainResult, itemsNullResult);
            AssertResultsMatch(plainResult, missingConfigResult);
        }

        [Test]
        public void PackBlobs_CandidatesExceedTheCap_StopsExactlyAtItAndNeverOverflows()
        {
            // Direct pin on PaintTriangle.PackBlobs' own loop-termination boundary — no test anywhere
            // exercises the cap itself (ShotItemLayer.MaxPaintBlobs (@ref plan_shot_solver_accuracy Phase
            // C5) mirrors PaintItemHandler.MaxBlobs at 64; every Paint test above deliberately collapses
            // to exactly ONE blob, which never comes close to exercising `results.Count < maxBlobs`).
            // A wide/long triangle (length 20, base width 40, blob radius 0.5) packs FAR more than 4 rows
            // worth of candidates — comfortably enough to overflow a small cap — so a cap of 4 stopping
            // at exactly 4 (never 3, never 5+) proves the boundary check itself (`<` not `<=`, and
            // re-checked inside the inner loop too, per PackBlobs' own two count-guarded loops).
            var triangle = PaintTriangle.Build(
                Vector2.zero, Vector2.up, new PaintSpreadParams(spreadOffset: 0f, spreadLength: 20f, spreadBaseWidth: 40f));

            var results = new List<Vector2>();
            triangle.PackBlobs(blobRadius: 0.5f, maxBlobs: 4, results);

            Assert.AreEqual(4, results.Count, "the cap must stop generation at exactly maxBlobs, never over- or under-shoot it");

            // Sanity: the SAME triangle with no meaningful cap packs many more rows worth of blobs —
            // proving the 4-blob result above was actually cap-limited, not just what the geometry
            // happens to produce on its own.
            var uncapped = new List<Vector2>();
            triangle.PackBlobs(blobRadius: 0.5f, maxBlobs: 1000, uncapped);
            Assert.Greater(uncapped.Count, 4, "the geometry alone produces more than 4 blobs — the capped run above was genuinely limited by maxBlobs");
        }

        [Test]
        public void ApplyEffectHits_PaintRecolorsAnItemCarryingBalloon_TheItemSurvivesAndLaterFiresOnItsOwnPop()
        {
            // Cross-item interaction (@ref plan_shot_solver_accuracy Phase C5's cross-cutting list):
            // ApplyRecolor only ever writes ColorId/IsRainbow (see its own doc) — a balloon's Item field
            // must ride through a repaint untouched. Proven the same INDIRECT way the Shield grant is
            // already pinned elsewhere in this file (ResolveBalloonContact_ShieldItem_Grants...): a
            // downstream Shield-carrying balloon B is repainted by the Paint host's own splash (same
            // one-blob geometry as the headline Paint test), then popped DIRECTLY by the continuing ray;
            // if the repaint had wiped B's Item, its own Shield grant would never fire, and a wall bounce
            // immediately behind it — otherwise fatal at 0 starting shields — would kill the flight
            // before it ever reaches the return-path filler balloon.
            const float r = 1.0f;
            var walls = new Vector4(2.5f, 1000f, -1000f, -1000f);

            ShotBalloonSnapshot[] BuildBoard(ItemType targetItem)
            {
                return new[]
                {
                    ShotBoardBuilder.Green(
                        new Vector2(0f, 1f), 0.05f, "Yellow", 1, 1, new Vector2Int(50, 50), 0, 0, 0f, false, null,
                        item: ItemType.Paint),
                    ShotBoardBuilder.Green(
                        new Vector2(0f, 2f), 0.05f, "Blue", 1, 1, new Vector2Int(0, -2), 0, 0, 0f, false, null,
                        item: targetItem),
                    ShotBoardBuilder.Green(
                        new Vector2(0f, -5f), 0.05f, "Purple", 1, 1, new Vector2Int(500, 500), 0, 0, 0f, false, null),
                };
            }

            var grantingBoard = BuildBoard(ItemType.Shield);
            var grantingWorkingSet = new ShotBalloonState[grantingBoard.Length];
            var granted = ShotSimulator.Simulate(
                grantingBoard, walls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: grantingWorkingSet,
                items: CreatePaintItemLayer(spreadOffset: 0f, spreadLength: r, spreadBaseWidth: 4f * r, spreadBlobRadius: r));

            Assert.IsFalse(granted.Died, "B's own Shield item survived the repaint and covered the immediate wall bounce");
            Assert.IsTrue(granted.BoardCleared, "the reflected ray goes on to clear the return-path filler");
            Assert.AreEqual(3, granted.Pops);

            // Control: same repaint, but B carries no item at all — nothing can grant the shield, so the
            // identical bounce is fatal. This isolates the survival above to B's OWN preserved item,
            // not some other side effect of the repaint or the board layout.
            var controlBoard = BuildBoard(ItemType.None);
            var controlWorkingSet = new ShotBalloonState[controlBoard.Length];
            var control = ShotSimulator.Simulate(
                controlBoard, walls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: controlWorkingSet,
                items: CreatePaintItemLayer(spreadOffset: 0f, spreadLength: r, spreadBaseWidth: 4f * r, spreadBlobRadius: r));

            Assert.IsTrue(control.Died, "without an item to preserve, nothing grants the shield the same bounce needs");
            Assert.AreEqual(2, control.Pops, "the flight dies at the wall, never reaching the return-path filler");
        }
    }
}
