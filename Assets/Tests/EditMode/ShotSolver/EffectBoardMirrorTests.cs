using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration.Palette;
using BalloonParty.Item.Bomb;
using BalloonParty.Item.Effects;
using BalloonParty.Item.Laser;
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
            Rainbow
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
