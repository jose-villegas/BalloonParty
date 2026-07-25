using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration.Palette;
using BalloonParty.Item.Bomb;
using BalloonParty.Item.Effects;
using BalloonParty.Item.Laser;
using BalloonParty.Item.Lightning;
using BalloonParty.Item.Paint;
using BalloonParty.Shared;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Grid;
using BalloonParty.Solver;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.ShotSolver
{
    /// <summary>Phase C2 (@ref plan_shot_solver_accuracy Phase C2): proves <see cref="BombBlast.Resolve" />
    /// selects an IDENTICAL hit set whether run over a live <see cref="GridEffectBoard" /> (a real
    /// headless <see cref="SlotGrid" /> carrying real <see cref="BalloonModel" />/<see cref="ToughBalloonModel" />/
    /// <see cref="UnbreakableBalloonModel" /> occupants) or a <see cref="ShotSimEffectBoard" /> bound to
    /// an equivalent hand-built working set — the two boards are the only two <see cref="IEffectBoard" />
    /// implementations any effect core ever runs over, so a divergence here would silently make the
    /// solver's Bomb geometry disagree with the live game's. Every occupant shares one uniform radius:
    /// <see cref="GridEffectBoard" />'s viewless fallback (no live view registered — this IS the headless
    /// path <c>BalanceBiasFidelityTests</c> already exercises for <see cref="SlotGrid" />) assigns the
    /// SAME <c>viewlessRadius</c> to every occupant regardless of its own model, so any per-occupant
    /// radius variety would only measure that fallback, not <see cref="BombBlast" /> itself.</summary>
    [TestFixture]
    public class EffectBoardMirrorTests
    {
        private static readonly Vector2 Separation = new(1f, 1f);
        private static readonly Vector2 Offset = Vector2.zero;
        private const float OccupantRadius = 0.1f;

        private enum OccupantKind
        {
            Green,
            Tough,
            Unbreakable,
            Rainbow,

            // Soap: colourless like Tough/Unbreakable, but IWashesProjectileColor, NOT IResistsPaint
            // (BubbleClusterModel implements neither IPaintable nor IResistsPaint) — see
            // ResistsPaint_ToughUnbreakableAndSoapOccupants_MatchesLiveFormulaOnBothBoards.
            Soap
        }

        private readonly struct OccupantSpec
        {
            public readonly Vector2Int Slot;
            public readonly OccupantKind Kind;
            public readonly string ColorId;

            public OccupantSpec(Vector2Int slot, OccupantKind kind, string colorId = null)
            {
                Slot = slot;
                Kind = kind;
                ColorId = colorId;
            }
        }

        [Test]
        public void Resolve_NormalHostAgainstMixedCluster_GridAndSimAgreeOnHitSet()
        {
            // Distances from hostSlot (3,3), sep=(1,1)/offset=0 (hand-derived via HexCoordinates):
            // (2,3)/(4,3) are hex-neighbours at 2.0; (3,2)/(4,2) are hex-neighbours at ~1.414; (3,5) is
            // a NON-neighbour also at 2.0; (0,0) sits far outside everything (~7.6).
            var hostSlot = new Vector2Int(3, 3);
            var occupants = new[]
            {
                new OccupantSpec(new Vector2Int(2, 3), OccupantKind.Green, "Red"),
                new OccupantSpec(new Vector2Int(3, 2), OccupantKind.Green, "Blue"),
                new OccupantSpec(new Vector2Int(4, 3), OccupantKind.Tough),
                new OccupantSpec(new Vector2Int(4, 2), OccupantKind.Unbreakable),
                new OccupantSpec(new Vector2Int(3, 5), OccupantKind.Green, "Green"),
                new OccupantSpec(new Vector2Int(0, 0), OccupantKind.Green, "Purple"),
            };

            var grid = BuildGrid(6, 7);
            PlaceOnGrid(grid, occupants);
            var gridBoard = new GridEffectBoard(grid, OccupantRadius);
            gridBoard.Rebuild(hostSlot);

            var workingSet = BuildWorkingSet(grid, occupants);
            var simBoard = new ShotSimEffectBoard(BuildLattice(grid));
            simBoard.Bind(workingSet, workingSet.Length, hostSlot);

            // Kill radius 2.0 plus the uniform 0.1 occupant radius = 2.1 combined — covers both
            // hex-neighbour distances (2.0, 1.414) and the non-neighbour at 2.0; excludes (0,0).
            var blastParams = new BombBlastParams(radius: 2.0f, rainbowConversionRange: 0f);
            var origin = (Vector2)grid.IndexToWorldPosition(hostSlot);

            var gridHits = new List<EffectHit>();
            BombBlast.Resolve(gridBoard, origin, hostSlot, false, null, in blastParams, gridHits);

            var simHits = new List<EffectHit>();
            BombBlast.Resolve(simBoard, origin, hostSlot, false, null, in blastParams, simHits);

            AssertSameHitSet(gridBoard, gridHits, simBoard, simHits);

            Assert.AreEqual(5, gridHits.Count);
            AssertHit(gridBoard, gridHits, new Vector2Int(2, 3), EffectHitKind.PiercingDamage);
            AssertHit(gridBoard, gridHits, new Vector2Int(3, 2), EffectHitKind.PiercingDamage);
            AssertHit(gridBoard, gridHits, new Vector2Int(4, 3), EffectHitKind.PiercingDamage);
            AssertHit(gridBoard, gridHits, new Vector2Int(4, 2), EffectHitKind.PiercingDamage);
            AssertHit(gridBoard, gridHits, new Vector2Int(3, 5), EffectHitKind.Damage);
            AssertNoHit(gridBoard, gridHits, new Vector2Int(0, 0));
        }

        [Test]
        public void Resolve_RainbowHostAgainstMixedCluster_GridAndSimAgreeOnHitSet()
        {
            // Rainbow classification drops the occupant-radius term entirely (centre distance alone):
            // (2,3)/(3,5)/(4,3)/(4,2) sit at/under the 2.0 kill radius (two hex-neighbours, one non-
            // neighbour, one at the shorter diagonal distance); (2,2) at ~3.16 and (2,1) at ~2.83 both
            // fall in the wider 2.0-4.0 conversion ring, but only (2,2) (a paintable BalloonModel) — NOT
            // (2,1) (a non-paintable Tough) — actually recolors; (0,0) at ~7.6 sits outside the ring too.
            var hostSlot = new Vector2Int(3, 3);
            var occupants = new[]
            {
                new OccupantSpec(new Vector2Int(2, 3), OccupantKind.Green, "Red"),
                new OccupantSpec(new Vector2Int(3, 5), OccupantKind.Green, "Blue"),
                new OccupantSpec(new Vector2Int(4, 3), OccupantKind.Tough),
                new OccupantSpec(new Vector2Int(4, 2), OccupantKind.Unbreakable),
                new OccupantSpec(new Vector2Int(2, 2), OccupantKind.Green, "Green"),
                new OccupantSpec(new Vector2Int(2, 1), OccupantKind.Tough),
                new OccupantSpec(new Vector2Int(0, 0), OccupantKind.Green, "Purple"),
            };

            var grid = BuildGrid(6, 7);
            PlaceOnGrid(grid, occupants);
            var gridBoard = new GridEffectBoard(grid, OccupantRadius);
            gridBoard.Rebuild(hostSlot);

            var workingSet = BuildWorkingSet(grid, occupants);
            var simBoard = new ShotSimEffectBoard(BuildLattice(grid));
            simBoard.Bind(workingSet, workingSet.Length, hostSlot);

            var blastParams = new BombBlastParams(radius: 2.0f, rainbowConversionRange: 2.0f);
            var origin = (Vector2)grid.IndexToWorldPosition(hostSlot);
            var rainbowColorId = GamePalette.RainbowColorId;

            var gridHits = new List<EffectHit>();
            BombBlast.Resolve(gridBoard, origin, hostSlot, true, rainbowColorId, in blastParams, gridHits);

            var simHits = new List<EffectHit>();
            BombBlast.Resolve(simBoard, origin, hostSlot, true, rainbowColorId, in blastParams, simHits);

            AssertSameHitSet(gridBoard, gridHits, simBoard, simHits);

            Assert.AreEqual(5, gridHits.Count);
            AssertHit(gridBoard, gridHits, new Vector2Int(2, 3), EffectHitKind.PiercingDamage);
            AssertHit(gridBoard, gridHits, new Vector2Int(3, 5), EffectHitKind.PiercingDamage);
            AssertHit(gridBoard, gridHits, new Vector2Int(4, 3), EffectHitKind.PiercingDamage);
            AssertHit(gridBoard, gridHits, new Vector2Int(4, 2), EffectHitKind.PiercingDamage);
            AssertHit(gridBoard, gridHits, new Vector2Int(2, 2), EffectHitKind.Recolor, rainbowColorId);
            AssertNoHit(gridBoard, gridHits, new Vector2Int(2, 1));
            AssertNoHit(gridBoard, gridHits, new Vector2Int(0, 0));
        }

        [Test]
        public void Resolve_RainbowHostWithAlreadyRainbowRingOccupant_BothBoardsRecolorItAgainToo()
        {
            // C0 IsPaintable regression pin: `model is IPaintable` (BombItemHandler.RainbowBlast) has
            // NO IsRainbow guard at all — a BalloonModel that's ALREADY rainbow-coloured is still
            // IPaintable, so it recolors again (a value no-op, but a real dispatched hit) exactly like
            // any other paintable ring occupant. ShotSimEffectBoard used to special-case `!IsRainbow &&`
            // into its own IsPaintable computation (fixed this phase — see its own comment); this is the
            // scenario that bug would have silently diverged the sim from live on, and neither of the
            // two mixed-cluster tests above happens to include an already-rainbow occupant to catch it.
            var hostSlot = new Vector2Int(3, 3);
            var occupants = new[]
            {
                new OccupantSpec(new Vector2Int(2, 2), OccupantKind.Rainbow, GamePalette.RainbowColorId),
            };

            var grid = BuildGrid(6, 7);
            PlaceOnGrid(grid, occupants);
            var gridBoard = new GridEffectBoard(grid, OccupantRadius);
            gridBoard.Rebuild(hostSlot);

            var workingSet = BuildWorkingSet(grid, occupants);
            var simBoard = new ShotSimEffectBoard(BuildLattice(grid));
            simBoard.Bind(workingSet, workingSet.Length, hostSlot);

            // (2,2) sits at ~3.16 from (3,3) (same hand-derivation as the mixed-cluster rainbow test
            // above) — inside the 1.0-4.0 conversion ring (kill radius 1.0, range 3.0), outside the kill
            // zone.
            var blastParams = new BombBlastParams(radius: 1.0f, rainbowConversionRange: 3.0f);
            var origin = (Vector2)grid.IndexToWorldPosition(hostSlot);
            var rainbowColorId = GamePalette.RainbowColorId;

            var gridHits = new List<EffectHit>();
            BombBlast.Resolve(gridBoard, origin, hostSlot, true, rainbowColorId, in blastParams, gridHits);

            var simHits = new List<EffectHit>();
            BombBlast.Resolve(simBoard, origin, hostSlot, true, rainbowColorId, in blastParams, simHits);

            AssertSameHitSet(gridBoard, gridHits, simBoard, simHits);
            Assert.AreEqual(1, gridHits.Count);
            AssertHit(gridBoard, gridHits, new Vector2Int(2, 2), EffectHitKind.Recolor, rainbowColorId);
        }

        [Test]
        public void LaserResolve_NormalCrossAgainstMixedCluster_GridAndSimAgreeOnHitSet()
        {
            // Distances from hostSlot (2,2), sep=(1,1)/offset=0 (hand-derived via HexCoordinates): the
            // four arm targets (3,2)/(1,2)/(2,0)/(2,4) each sit exactly 2.0 along one of the four cast
            // axes; (4,2) sits a further 4.0 along the SAME right arm as (3,2) — proving one arm can
            // hit two occupants in a single pass; (0,0) sits ~4.47 away, off every axis entirely.
            var hostSlot = new Vector2Int(2, 2);
            var occupants = new[]
            {
                new OccupantSpec(new Vector2Int(3, 2), OccupantKind.Green, "Red"),
                new OccupantSpec(new Vector2Int(4, 2), OccupantKind.Tough),
                new OccupantSpec(new Vector2Int(1, 2), OccupantKind.Unbreakable),
                new OccupantSpec(new Vector2Int(2, 4), OccupantKind.Green, "Blue"),
                new OccupantSpec(new Vector2Int(2, 0), OccupantKind.Green, "Green"),
                new OccupantSpec(new Vector2Int(0, 0), OccupantKind.Green, "Purple"),
            };

            var grid = BuildGrid(6, 6);
            PlaceOnGrid(grid, occupants);
            var gridBoard = new GridEffectBoard(grid, OccupantRadius);
            gridBoard.Rebuild(hostSlot);

            var workingSet = BuildWorkingSet(grid, occupants);
            var simBoard = new ShotSimEffectBoard(BuildLattice(grid));
            simBoard.Bind(workingSet, workingSet.Length, hostSlot);

            // castRadius 0.1 + the uniform 0.1 occupant radius = 0.2 combined — the farthest target
            // (distance 4.0) needs an entry distance of 3.8, so castDistance 4.5 comfortably reaches
            // every arm target while (0,0) (~4.47 away, off-axis) still misses every arm.
            var crossParams = new LaserCrossParams(castRadius: 0.1f, castDistance: 4.5f);
            var origin = (Vector2)grid.IndexToWorldPosition(hostSlot);

            var gridHits = new List<EffectHit>();
            LaserCross.Resolve(gridBoard, origin, rotationDegrees: 0f, false, null, in crossParams, gridHits);

            var simHits = new List<EffectHit>();
            LaserCross.Resolve(simBoard, origin, rotationDegrees: 0f, false, null, in crossParams, simHits);

            AssertSameHitSet(gridBoard, gridHits, simBoard, simHits);

            Assert.AreEqual(5, gridHits.Count);
            AssertHit(gridBoard, gridHits, new Vector2Int(3, 2), EffectHitKind.Damage);
            AssertHit(gridBoard, gridHits, new Vector2Int(4, 2), EffectHitKind.Damage);
            AssertHit(gridBoard, gridHits, new Vector2Int(1, 2), EffectHitKind.Damage);
            AssertHit(gridBoard, gridHits, new Vector2Int(2, 4), EffectHitKind.Damage);
            AssertHit(gridBoard, gridHits, new Vector2Int(2, 0), EffectHitKind.Damage);
            AssertNoHit(gridBoard, gridHits, new Vector2Int(0, 0));
        }

        [Test]
        public void LaserResolve_RainbowHostBorderingConversion_GridAndSimAgreeOnHitSet()
        {
            // (3,2) and (4,2) both sit on the host's right arm (distances 2.0/4.0, same technique as
            // the normal-cross test above) — (4,2) doubles as an ALREADY-HIT hex neighbour of (3,2), so
            // the conversion pass must skip it rather than emit a redundant Recolor. (3,1) is a
            // NON-paintable (Tough) neighbour of both (never converts); (2,1) is a paintable, never-hit
            // neighbour of (3,2) (converts). Neither (3,1) nor (2,1) sits on any cast axis, so neither
            // is ever a candidate for a direct hit.
            var hostSlot = new Vector2Int(2, 2);
            var occupants = new[]
            {
                new OccupantSpec(new Vector2Int(3, 2), OccupantKind.Green, "Blue"),
                new OccupantSpec(new Vector2Int(4, 2), OccupantKind.Green, "Green"),
                new OccupantSpec(new Vector2Int(3, 1), OccupantKind.Tough),
                new OccupantSpec(new Vector2Int(2, 1), OccupantKind.Green, "Yellow"),
            };

            var grid = BuildGrid(6, 6);
            PlaceOnGrid(grid, occupants);
            var gridBoard = new GridEffectBoard(grid, OccupantRadius);
            gridBoard.Rebuild(hostSlot);

            var workingSet = BuildWorkingSet(grid, occupants);
            var simBoard = new ShotSimEffectBoard(BuildLattice(grid));
            simBoard.Bind(workingSet, workingSet.Length, hostSlot);

            var crossParams = new LaserCrossParams(castRadius: 0.1f, castDistance: 4.5f);
            var origin = (Vector2)grid.IndexToWorldPosition(hostSlot);
            var rainbowColorId = GamePalette.RainbowColorId;

            var gridHits = new List<EffectHit>();
            LaserCross.Resolve(gridBoard, origin, rotationDegrees: 0f, true, rainbowColorId, in crossParams, gridHits);

            var simHits = new List<EffectHit>();
            LaserCross.Resolve(simBoard, origin, rotationDegrees: 0f, true, rainbowColorId, in crossParams, simHits);

            AssertSameHitSet(gridBoard, gridHits, simBoard, simHits);

            Assert.AreEqual(3, gridHits.Count, "two direct hits plus exactly one conversion — no redundant recolor of the already-hit neighbour, no conversion of the non-paintable one");
            AssertHit(gridBoard, gridHits, new Vector2Int(3, 2), EffectHitKind.Damage);
            AssertHit(gridBoard, gridHits, new Vector2Int(4, 2), EffectHitKind.Damage);
            AssertHit(gridBoard, gridHits, new Vector2Int(2, 1), EffectHitKind.Recolor, rainbowColorId);
            AssertNoHit(gridBoard, gridHits, new Vector2Int(3, 1));
        }

        [Test]
        public void LightningResolve_NormalHostMixedColours_GridAndSimAgreeOnDistanceOrderedChain()
        {
            // Distances from hostSlot (3,3), sep=(1,1)/offset=0 (hand-derived via
            // grid.IndexToWorldPosition): (3,2) is nearest (~1.414), (2,3) is mid (2.0), (0,0) is
            // farthest (~7.615) — all three "Red". (4,3) sits at the SAME distance as (2,3) (2.0) but is
            // "Blue" (must never match, regardless of distance); (4,2) sits at the SAME distance as (3,2)
            // (~1.414) but is a colourless Tough (must never match either — LightningChain's gate is a
            // plain ColorId equality, and a Tough has none).
            var hostSlot = new Vector2Int(3, 3);
            var occupants = new[]
            {
                new OccupantSpec(new Vector2Int(3, 2), OccupantKind.Green, "Red"),
                new OccupantSpec(new Vector2Int(2, 3), OccupantKind.Green, "Red"),
                new OccupantSpec(new Vector2Int(0, 0), OccupantKind.Green, "Red"),
                new OccupantSpec(new Vector2Int(4, 3), OccupantKind.Green, "Blue"),
                new OccupantSpec(new Vector2Int(4, 2), OccupantKind.Tough),
            };

            var grid = BuildGrid(6, 7);
            PlaceOnGrid(grid, occupants);
            var gridBoard = new GridEffectBoard(grid, OccupantRadius);
            gridBoard.Rebuild(hostSlot);

            var workingSet = BuildWorkingSet(grid, occupants);
            var simBoard = new ShotSimEffectBoard(BuildLattice(grid));
            simBoard.Bind(workingSet, workingSet.Length, hostSlot);

            var origin = (Vector2)grid.IndexToWorldPosition(hostSlot);

            var gridHits = new List<EffectHit>();
            LightningChain.Resolve(gridBoard, origin, hostSlot, "Red", false, null, gridHits);

            var simHits = new List<EffectHit>();
            LightningChain.Resolve(simBoard, origin, hostSlot, "Red", false, null, simHits);

            AssertSameHitSet(gridBoard, gridHits, simBoard, simHits);
            Assert.AreEqual(3, gridHits.Count, "only the three same-colour matches — the wrong-colour and colourless occupants never match");

            var nearestFirst = new[] { new Vector2Int(3, 2), new Vector2Int(2, 3), new Vector2Int(0, 0) };
            AssertLightningOrder(gridBoard, gridHits, nearestFirst);
            AssertLightningOrder(simBoard, simHits, nearestFirst);

            for (var i = 0; i < gridHits.Count; i++)
            {
                Assert.AreEqual(EffectHitKind.Damage, gridHits[i].Kind, "a normal host damages, never recolors");
            }
        }

        [Test]
        public void LightningResolve_RainbowHostMixedColours_GridAndSimConvertTheSameGroupInOrder()
        {
            // Same cluster as the normal-host test above, but a rainbow host — every match CONVERTS
            // (Recolor to the rainbow marker) instead of taking damage, in the identical nearest-first
            // order; the wrong-colour and colourless occupants stay excluded for the same reason.
            var hostSlot = new Vector2Int(3, 3);
            var occupants = new[]
            {
                new OccupantSpec(new Vector2Int(3, 2), OccupantKind.Green, "Red"),
                new OccupantSpec(new Vector2Int(2, 3), OccupantKind.Green, "Red"),
                new OccupantSpec(new Vector2Int(0, 0), OccupantKind.Green, "Red"),
                new OccupantSpec(new Vector2Int(4, 3), OccupantKind.Green, "Blue"),
                new OccupantSpec(new Vector2Int(4, 2), OccupantKind.Tough),
            };

            var grid = BuildGrid(6, 7);
            PlaceOnGrid(grid, occupants);
            var gridBoard = new GridEffectBoard(grid, OccupantRadius);
            gridBoard.Rebuild(hostSlot);

            var workingSet = BuildWorkingSet(grid, occupants);
            var simBoard = new ShotSimEffectBoard(BuildLattice(grid));
            simBoard.Bind(workingSet, workingSet.Length, hostSlot);

            var origin = (Vector2)grid.IndexToWorldPosition(hostSlot);
            var rainbowColorId = GamePalette.RainbowColorId;

            var gridHits = new List<EffectHit>();
            LightningChain.Resolve(gridBoard, origin, hostSlot, "Red", true, rainbowColorId, gridHits);

            var simHits = new List<EffectHit>();
            LightningChain.Resolve(simBoard, origin, hostSlot, "Red", true, rainbowColorId, simHits);

            AssertSameHitSet(gridBoard, gridHits, simBoard, simHits);
            Assert.AreEqual(3, gridHits.Count);

            var nearestFirst = new[] { new Vector2Int(3, 2), new Vector2Int(2, 3), new Vector2Int(0, 0) };
            AssertLightningOrder(gridBoard, gridHits, nearestFirst);
            AssertLightningOrder(simBoard, simHits, nearestFirst);

            for (var i = 0; i < gridHits.Count; i++)
            {
                Assert.AreEqual(EffectHitKind.Recolor, gridHits[i].Kind, "a rainbow host converts, never damages");
                Assert.AreEqual(rainbowColorId, gridHits[i].ColorId);
            }
        }

        [Test]
        public void LightningFindNearestConcreteColor_RingWalk_GridAndSimAgree()
        {
            // Ring-1 neighbours of (3,3) via HexCoordinates.HexNeighborIndices(3,3) (row 3 is odd,
            // shiftedCol=4): {(2,3),(4,3),(3,2),(4,2),(3,4),(4,4)}. (3,2) is ALREADY rainbow-coloured
            // (skipped — the ring walk excludes the rainbow marker exactly like BalloonModelExtensions.
            // FindNearestColorId's own palette.IsRainbow guard); (4,2) is a colourless Tough (skipped —
            // no concrete colour at all); (3,4) is the ONLY concrete colour in ring 1 ("Purple") — found
            // regardless of the ring walk's own side-traversal order, since it's the sole valid
            // candidate among six ring-1 cells.
            var center = new Vector2Int(3, 3);
            var occupants = new[]
            {
                new OccupantSpec(new Vector2Int(3, 2), OccupantKind.Rainbow, GamePalette.RainbowColorId),
                new OccupantSpec(new Vector2Int(4, 2), OccupantKind.Tough),
                new OccupantSpec(new Vector2Int(3, 4), OccupantKind.Green, "Purple"),
            };

            var grid = BuildGrid(8, 8);
            PlaceOnGrid(grid, occupants);
            var gridBoard = new GridEffectBoard(grid, OccupantRadius);
            gridBoard.Rebuild(center);

            var workingSet = BuildWorkingSet(grid, occupants);
            var simBoard = new ShotSimEffectBoard(BuildLattice(grid));
            simBoard.Bind(workingSet, workingSet.Length, center);

            var rainbowColorId = GamePalette.RainbowColorId;
            Assert.AreEqual("Purple", LightningChain.FindNearestConcreteColor(gridBoard, center, rainbowColorId));
            Assert.AreEqual("Purple", LightningChain.FindNearestConcreteColor(simBoard, center, rainbowColorId));
        }

        [Test]
        public void LightningFindNearestConcreteColor_ColourExactlyAtSearchRadius_GridAndSimBothFindIt()
        {
            // SearchRadius = Mathf.Max(Columns, Rows) = 3 on a 3x3 board — the ring walk's own
            // `ring <= maxRadius` loop boundary (an off-by-one `<` instead of `<=` would silently
            // exclude the LAST ring the loop is supposed to check). Hand-derived via the same
            // corner/side/step cube-math as the ring-1 test above, from center (0,0): rings 1 and 2
            // never reach (2,2) at all (hex rings partition the board into disjoint concentric
            // shells), and ring 3's own side-1 walk lands on (2,2) at its second step — the sole
            // occupant on the board — so finding it at all proves ring 3 (the boundary ring itself)
            // was actually searched, not silently skipped.
            var center = new Vector2Int(0, 0);
            var occupants = new[]
            {
                new OccupantSpec(new Vector2Int(2, 2), OccupantKind.Green, "Purple"),
            };

            var grid = BuildGrid(3, 3);
            PlaceOnGrid(grid, occupants);
            var gridBoard = new GridEffectBoard(grid, OccupantRadius);
            gridBoard.Rebuild();

            var workingSet = BuildWorkingSet(grid, occupants);
            var simBoard = new ShotSimEffectBoard(BuildLattice(grid));
            simBoard.Bind(workingSet, workingSet.Length);

            var rainbowColorId = GamePalette.RainbowColorId;
            Assert.AreEqual("Purple", LightningChain.FindNearestConcreteColor(gridBoard, center, rainbowColorId));
            Assert.AreEqual("Purple", LightningChain.FindNearestConcreteColor(simBoard, center, rainbowColorId));
        }

        [Test]
        public void LightningFindNearestConcreteColor_NoConcreteColourAnywhere_GridAndSimBothReturnNull()
        {
            var grid = BuildGrid(4, 4);
            var gridBoard = new GridEffectBoard(grid, OccupantRadius);
            gridBoard.Rebuild();

            var simBoard = new ShotSimEffectBoard(BuildLattice(grid));
            simBoard.Bind(new ShotBalloonState[0], 0);

            var rainbowColorId = GamePalette.RainbowColorId;
            var center = new Vector2Int(1, 1);
            Assert.IsNull(LightningChain.FindNearestConcreteColor(gridBoard, center, rainbowColorId));
            Assert.IsNull(LightningChain.FindNearestConcreteColor(simBoard, center, rainbowColorId));
        }

        [Test]
        public void PaintResolve_MixedClusterAgreesBetweenBoards_AcceptsSkipsResistsAndGapsCorrectly()
        {
            // Row 0 only (HexCoordinates world.x = 2*col - 0.5, world.y = 0 for sep=(1,1)/offset=0):
            // col0=-0.5, col1=1.5, col2=3.5, col5=9.5, col6=11.5. Two blobs at (0,0) and (10,0), radius
            // 2.0 (radiusSqr 4.0): col0 (dist 0.5 from blobA) and col5 (dist 0.5 from blobB) are the
            // only DIFFERENT-colour paintable occupants in range — they accept. col1 (dist 1.5 from
            // blobA, well within range) shares the paint colour ("Red") and must skip regardless of
            // distance — mirrors TryClassify's own colour-equality gate running BEFORE any distance
            // check. col6 (dist 1.5 from blobB) is a Tough (ResistsPaint, not IsPaintable) and must skip
            // despite sitting in range — PaintSpread never emits a hit for a resist occupant (the drip
            // is live-only presentation this core never models). col2 (dist 3.5/6.5 from A/B) sits
            // outside BOTH blobs' reach — the gap case (@ref plan_shot_solver_accuracy Phase C5).
            var occupants = new[]
            {
                new OccupantSpec(new Vector2Int(0, 0), OccupantKind.Green, "Blue"),
                new OccupantSpec(new Vector2Int(1, 0), OccupantKind.Green, "Red"),
                new OccupantSpec(new Vector2Int(2, 0), OccupantKind.Green, "Purple"),
                new OccupantSpec(new Vector2Int(5, 0), OccupantKind.Green, "Green"),
                new OccupantSpec(new Vector2Int(6, 0), OccupantKind.Tough),
            };

            var grid = BuildGrid(7, 1);
            PlaceOnGrid(grid, occupants);
            var gridBoard = new GridEffectBoard(grid, OccupantRadius);
            gridBoard.Rebuild();

            var workingSet = BuildWorkingSet(grid, occupants);
            var simBoard = new ShotSimEffectBoard(BuildLattice(grid));
            simBoard.Bind(workingSet, workingSet.Length);

            var blobPositions = new[] { new Vector2(0f, 0f), new Vector2(10f, 0f) };
            const float blobRadius = 2.0f;
            const string paintColorId = "Red";

            var gridHits = new List<EffectHit>();
            PaintSpread.Resolve(gridBoard, blobPositions, blobRadius, paintColorId, gridHits);

            var simHits = new List<EffectHit>();
            PaintSpread.Resolve(simBoard, blobPositions, blobRadius, paintColorId, simHits);

            AssertSameHitSet(gridBoard, gridHits, simBoard, simHits);

            Assert.AreEqual(2, gridHits.Count, "only the two different-colour, in-range, paintable occupants accept");
            AssertHit(gridBoard, gridHits, new Vector2Int(0, 0), EffectHitKind.Recolor, paintColorId);
            AssertHit(gridBoard, gridHits, new Vector2Int(5, 0), EffectHitKind.Recolor, paintColorId);
            AssertNoHit(gridBoard, gridHits, new Vector2Int(1, 0));
            AssertNoHit(gridBoard, gridHits, new Vector2Int(2, 0));
            AssertNoHit(gridBoard, gridHits, new Vector2Int(6, 0));

            AssertPaintGroup(gridBoard, gridHits, new Vector2Int(0, 0), 0);
            AssertPaintGroup(gridBoard, gridHits, new Vector2Int(5, 0), 1);
            AssertPaintGroup(simBoard, simHits, new Vector2Int(0, 0), 0);
            AssertPaintGroup(simBoard, simHits, new Vector2Int(5, 0), 1);
        }

        [Test]
        public void ResistsPaint_ToughUnbreakableAndSoapOccupants_MatchesLiveFormulaOnBothBoards()
        {
            // A direct pin for EffectOccupant.ResistsPaint itself (@ref plan_shot_solver_accuracy Phase
            // C5) — PaintSpread never actually READS this field yet (only IsPaintable gates its own
            // selection; a resist occupant is already excluded by IsPaintable alone, since Tough/
            // Unbreakable are colourless — see PaintSpread's own doc for the documented future repoint
            // that would read it), so nothing else in the suite asserts its VALUE on either board.
            // Tough/Unbreakable resist (colourless AND not a washer); a Soap (BubbleClusterModel) is
            // ALSO colourless but is NOT IResistsPaint — IWashesProjectileColor is its own distinct
            // marker — so it must compute false, identically to an ordinary paintable green. Getting
            // this formula wrong on either board (GridEffectBoard's `model is IResistsPaint` or
            // ShotSimEffectBoard's `!isPaintable && !washesProjectileColor`) would silently diverge live
            // from the sim the day something starts reading it.
            var occupants = new[]
            {
                new OccupantSpec(new Vector2Int(0, 0), OccupantKind.Green, "Red"),
                new OccupantSpec(new Vector2Int(1, 0), OccupantKind.Tough),
                new OccupantSpec(new Vector2Int(2, 0), OccupantKind.Unbreakable),
                new OccupantSpec(new Vector2Int(3, 0), OccupantKind.Soap),
            };

            var grid = BuildGrid(4, 1);
            PlaceOnGrid(grid, occupants);
            var gridBoard = new GridEffectBoard(grid, OccupantRadius);
            gridBoard.Rebuild();

            var workingSet = BuildWorkingSet(grid, occupants);
            var simBoard = new ShotSimEffectBoard(BuildLattice(grid));
            simBoard.Bind(workingSet, workingSet.Length);

            AssertResistsPaint(gridBoard, new Vector2Int(0, 0), expectedPaintable: true, expectedResists: false);
            AssertResistsPaint(gridBoard, new Vector2Int(1, 0), expectedPaintable: false, expectedResists: true);
            AssertResistsPaint(gridBoard, new Vector2Int(2, 0), expectedPaintable: false, expectedResists: true);
            AssertResistsPaint(gridBoard, new Vector2Int(3, 0), expectedPaintable: false, expectedResists: false);

            AssertResistsPaint(simBoard, new Vector2Int(0, 0), expectedPaintable: true, expectedResists: false);
            AssertResistsPaint(simBoard, new Vector2Int(1, 0), expectedPaintable: false, expectedResists: true);
            AssertResistsPaint(simBoard, new Vector2Int(2, 0), expectedPaintable: false, expectedResists: true);
            AssertResistsPaint(simBoard, new Vector2Int(3, 0), expectedPaintable: false, expectedResists: false);
        }

        private static void AssertResistsPaint(
            IEffectBoard board, Vector2Int slot, bool expectedPaintable, bool expectedResists)
        {
            for (var i = 0; i < board.Occupants.Count; i++)
            {
                if (board.Occupants[i].Slot != slot)
                {
                    continue;
                }

                Assert.AreEqual(expectedPaintable, board.Occupants[i].IsPaintable, $"IsPaintable mismatch at {slot}");
                Assert.AreEqual(expectedResists, board.Occupants[i].ResistsPaint, $"ResistsPaint mismatch at {slot}");
                return;
            }

            Assert.Fail($"expected an occupant at {slot}");
        }

        // Group carries the accepted occupant's nearest-blob index (see EffectHit's own doc) —
        // independent of AssertHit's (slot, kind, colour) check above.
        private static void AssertPaintGroup(
            IEffectBoard board, IReadOnlyList<EffectHit> hits, Vector2Int slot, int expectedGroup)
        {
            for (var i = 0; i < hits.Count; i++)
            {
                if (board.Occupants[hits[i].Handle].Slot != slot)
                {
                    continue;
                }

                Assert.AreEqual(expectedGroup, hits[i].Group, $"unexpected blob group at slot {slot}");
                return;
            }

            Assert.Fail($"expected a hit at slot {slot}");
        }

        // Verifies a chain's OWN claimed jump order — Group is the core's own per-hit ordering index
        // (see EffectHit's doc), so this checks BOTH that the hit list itself is nearest-first AND that
        // Group matches each hit's position in that list, independent of raw Handle numbering (which may
        // legitimately differ between the two board adapters — see AssertSameHitSet's own doc).
        private static void AssertLightningOrder(
            IEffectBoard board, IReadOnlyList<EffectHit> hits, IReadOnlyList<Vector2Int> expectedNearestFirst)
        {
            Assert.AreEqual(expectedNearestFirst.Count, hits.Count);
            for (var i = 0; i < hits.Count; i++)
            {
                Assert.AreEqual(expectedNearestFirst[i], board.Occupants[hits[i].Handle].Slot, $"jump {i} slot mismatch");
                Assert.AreEqual(i, hits[i].Group, $"jump {i} group mismatch");
            }
        }

        private static void PlaceOnGrid(SlotGrid grid, IReadOnlyList<OccupantSpec> occupants)
        {
            for (var i = 0; i < occupants.Count; i++)
            {
                var spec = occupants[i];
                IWriteableSlotActor actor = spec.Kind switch
                {
                    OccupantKind.Tough => new ToughBalloonModel(
                        new BalloonModelConfig(typeName: BalloonType.Tough, hitsToPop: 5)),
                    OccupantKind.Unbreakable => new UnbreakableBalloonModel(
                        new BalloonModelConfig(typeName: BalloonType.Unbreakable)),
                    OccupantKind.Soap => new BubbleClusterModel(
                        new BalloonModelConfig(typeName: BalloonType.BubbleCluster, hitsToPop: 5),
                        Substitute.For<IGamePalette>()),
                    _ => BuildGreen(spec.ColorId),
                };
                grid.Place(actor, null, spec.Slot);
            }
        }

        private static BalloonModel BuildGreen(string colorId)
        {
            var model = new BalloonModel();
            model.Color.Value = colorId;
            return model;
        }

        private static ShotBalloonState[] BuildWorkingSet(SlotGrid grid, IReadOnlyList<OccupantSpec> occupants)
        {
            var states = new ShotBalloonState[occupants.Count];
            for (var i = 0; i < occupants.Count; i++)
            {
                var spec = occupants[i];
                var position = (Vector2)grid.IndexToWorldPosition(spec.Slot);
                var snapshot = spec.Kind switch
                {
                    OccupantKind.Tough => ShotBoardBuilder.Tough(
                        position, OccupantRadius, 1, 5, spec.Slot, 0, 0, 0f, false, null),
                    OccupantKind.Unbreakable => ShotBoardBuilder.Tough(
                        position, OccupantRadius, 1, int.MaxValue, spec.Slot, 0, 0, 0f, false, null,
                        paysSourceColor: true),
                    OccupantKind.Rainbow => ShotBoardBuilder.Rainbow(
                        position, OccupantRadius, spec.ColorId, 1, 1, spec.Slot, 0, 0, 0f, false, null),
                    OccupantKind.Soap => ShotBoardBuilder.Tough(
                        position, OccupantRadius, 1, int.MaxValue, spec.Slot, 0, 0, 0f, false, null, washes: true),
                    _ => ShotBoardBuilder.Green(
                        position, OccupantRadius, spec.ColorId, 1, 1, spec.Slot, 0, 0, 0f, false, null),
                };
                states[i] = new ShotBalloonState(snapshot);
            }

            return states;
        }

        private static ShotSlotLattice BuildLattice(SlotGrid grid)
        {
            return new ShotSlotLattice(Separation, Offset, grid.Columns, grid.Rows);
        }

        private static SlotGrid BuildGrid(int columns, int rows)
        {
            var config = Substitute.For<ISlotGridConfig>();
            config.SlotsSize.Returns(new Vector2Int(columns, rows));
            config.SlotSeparation.Returns(Separation);
            config.SlotsOffset.Returns(Offset);
            return new SlotGrid(config, new BalancePathHolder());
        }

        // Handle numbering may legitimately differ between the two boards (occupant iteration order
        // isn't guaranteed to match) — comparing as a (slot, kind, colour) SET, not the raw handles or
        // list order, is what actually proves the two boards select the same balloons.
        private static void AssertSameHitSet(
            IEffectBoard gridBoard, IReadOnlyList<EffectHit> gridHits, IEffectBoard simBoard,
            IReadOnlyList<EffectHit> simHits)
        {
            var gridSet = ToHitSet(gridBoard, gridHits);
            var simSet = ToHitSet(simBoard, simHits);
            Assert.AreEqual(gridHits.Count, gridSet.Count, "grid board produced a duplicate (slot,kind,colour) hit");
            Assert.AreEqual(simHits.Count, simSet.Count, "sim board produced a duplicate (slot,kind,colour) hit");
            CollectionAssert.AreEquivalent(gridSet, simSet, "grid and sim boards must select the identical hit set");
        }

        private static HashSet<(Vector2Int Slot, EffectHitKind Kind, string ColorId)> ToHitSet(
            IEffectBoard board, IReadOnlyList<EffectHit> hits)
        {
            var set = new HashSet<(Vector2Int, EffectHitKind, string)>();
            for (var i = 0; i < hits.Count; i++)
            {
                set.Add((board.Occupants[hits[i].Handle].Slot, hits[i].Kind, hits[i].ColorId));
            }

            return set;
        }

        private static void AssertHit(
            IEffectBoard board, IReadOnlyList<EffectHit> hits, Vector2Int slot, EffectHitKind kind,
            string colorId = null)
        {
            for (var i = 0; i < hits.Count; i++)
            {
                if (board.Occupants[hits[i].Handle].Slot != slot)
                {
                    continue;
                }

                Assert.AreEqual(kind, hits[i].Kind, $"unexpected hit kind at slot {slot}");
                Assert.AreEqual(colorId, hits[i].ColorId, $"unexpected hit colour at slot {slot}");
                return;
            }

            Assert.Fail($"expected a hit at slot {slot}");
        }

        private static void AssertNoHit(IEffectBoard board, IReadOnlyList<EffectHit> hits, Vector2Int slot)
        {
            for (var i = 0; i < hits.Count; i++)
            {
                Assert.AreNotEqual(slot, board.Occupants[hits[i].Handle].Slot, $"unexpected hit at slot {slot}");
            }
        }
    }
}
