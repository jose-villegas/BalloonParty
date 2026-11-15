using System.Collections.Generic;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Items;
using BalloonParty.Item.Effects;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Solver;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.ShotSolver
{
    /// <summary>The plated reward balloons in the solver, the golden balloon (PLAN-RewardBalloons phase 2) and the
    /// disco ball (PLAN-DiscoBalloon phase 1): each deflects the ordinary shot forever, pops to a piercing shot
    /// a laser beam or a bomb's blast beside it (as an unbreakable does), pays no score, leaves the streak alone,
    /// and is counted apart from the other. Mirrors <c>PlatedBalloonModel.EvaluateHit</c>.</summary>
    [TestFixture(BalloonType.Golden)]
    [TestFixture(BalloonType.Disco)]
    public class ShotPlatedBalloonTests
    {
        private static readonly Vector4 WideOpenWalls = new(1000f, 1000f, -1000f, -1000f);

        private readonly BalloonType _type;

        public ShotPlatedBalloonTests(BalloonType type)
        {
            _type = type;
        }

        private ShotBalloonSnapshot Plated(Vector2 position, float radius, Vector2Int? slot = null)
        {
            var balance = slot.HasValue
                ? new BalanceProfile(slot.Value, 0, 0, 0f, false, null)
                : (BalanceProfile?)null;
            return _type == BalloonType.Golden
                ? ShotBalloonSnapshot.ForGoldenTarget(position, radius, balance)
                : ShotBalloonSnapshot.ForDiscoTarget(position, radius, balance);
        }

        // This fixture's own kind's pops; the other plated kind's must stay at zero.
        private int RewardPops(in ShotSimulationResult result)
        {
            return _type == BalloonType.Golden ? result.GoldenPops : result.DiscoPops;
        }

        private int OtherPlatedPops(in ShotSimulationResult result)
        {
            return _type == BalloonType.Golden ? result.DiscoPops : result.GoldenPops;
        }

        private static ShotItemLayer CreateSnipeItemLayer()
        {
            var effectParams = new Dictionary<ItemType, ItemEffectParams>
            {
                {
                    ItemType.Snipe,
                    new ItemEffectParams(
                        default, default, default, default,
                        new SnipeEffectParams(
                            1.5f, chargePerToughHit: 0, bloomBaseRadius: 0f, bloomRadiusPerCharge: 0f,
                            bloomRadiusCap: 0f),
                        damage: 0, flags: DamageFlags.Normal)
                },
            };
            var lattice = default(ShotSlotLattice);
            return new ShotItemLayer(effectParams, in lattice);
        }

        private static ShotItemLayer CreateBombItemLayer()
        {
            var effectParams = new Dictionary<ItemType, ItemEffectParams>
            {
                {
                    ItemType.Bomb,
                    new ItemEffectParams(
                        new BombEffectParams(1f, rainbowEffectScale: 1f, rainbowConversionRange: 0f), default, default,
                        default, default, 1, DamageFlags.Normal)
                },
            };
            var lattice = default(ShotSlotLattice);
            return new ShotItemLayer(effectParams, in lattice);
        }

        private static ShotItemLayer CreateLaserItemLayer()
        {
            var effectParams = new Dictionary<ItemType, ItemEffectParams>
            {
                {
                    ItemType.Laser,
                    new ItemEffectParams(
                        default, new LaserEffectParams(3f, 0.1f, colorCycles: 0f), default, default, default, 1,
                        DamageFlags.Piercing)
                },
            };
            var lattice = default(ShotSlotLattice);
            return new ShotItemLayer(effectParams, in lattice);
        }

        [Test]
        public void Simulate_PlainShotAtPlated_DeflectsEveryTimeAndNeverPops()
        {
            // Dead-centre shot at a plated balloon with a bottom wall one shield away: deflect, bounce, deflect
            // again, then the second bounce outruns the shield. A plain shot never pops it however often it
            // comes back, unlike a two-hit tough which would have popped on the second contact.
            var walls = new Vector4(1000f, 1000f, -1f, -1000f);
            var board = new[] { Plated(new Vector2(0f, 2f), 0.3f) };
            var workingSet = new ShotBalloonState[board.Length];

            var result = ShotSimulator.Simulate(
                board, walls, Vector2.zero, Vector2.up, startingShields: 1, projectileContactRadius: 0f,
                workingSet: workingSet);

            Assert.AreEqual(0, result.Pops);
            Assert.AreEqual(0, RewardPops(result));
            Assert.AreEqual(0, result.RawScore);
            Assert.AreEqual(0, result.ToughsCleared);
            Assert.IsFalse(result.BoardCleared);
            Assert.IsTrue(result.Died);
        }

        [Test]
        public void Simulate_PiercingShotThroughPlated_PopsItScorelessAndKeepsTheStreak()
        {
            // A Snipe host arms the pierce, which plows the plated balloon. The two Reds either side are one
            // same-colour streak only if the plated balloon touched neither the streak count nor the colour: 1*1
            // for the first, 10*2 for the second. A plated balloon that reset the streak like a tough pop would
            // leave 1 + 10.
            var board = new[]
            {
                ShotBoardBuilder.Green(new Vector2(0f, 1f), 0.05f, "Red", 1, 1, item: ItemType.Snipe),
                Plated(new Vector2(0f, 2f), 0.05f),
                ShotBoardBuilder.Green(new Vector2(0f, 3f), 0.05f, "Red", 10, 1),
            };
            var workingSet = new ShotBalloonState[board.Length];

            var result = ShotSimulator.Simulate(
                board, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: workingSet, items: CreateSnipeItemLayer());

            Assert.AreEqual(1 + 10 * 2, result.RawScore, "the plated balloon pays nothing and the streak climbs through it");
            Assert.AreEqual(3, result.Pops);
            Assert.AreEqual(1, RewardPops(result), "counted apart, for a ranking that wants it");
            Assert.AreEqual(0, OtherPlatedPops(result));
            Assert.AreEqual(0, result.HeartPops);
            Assert.AreEqual(0, result.ToughsCleared, "scoreless: not tallied as a tough clear either");
            Assert.IsTrue(result.BoardCleared);
        }

        [Test]
        public void Simulate_PiercingShotThroughPlatedAlone_ClearsBoardForNothing()
        {
            var board = new[]
            {
                ShotBoardBuilder.Green(new Vector2(0f, 1f), 0.05f, "Red", 1, 1, item: ItemType.Snipe),
                Plated(new Vector2(0f, 2f), 0.05f),
            };
            var workingSet = new ShotBalloonState[board.Length];

            var result = ShotSimulator.Simulate(
                board, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: workingSet, items: CreateSnipeItemLayer());

            Assert.AreEqual(1, result.RawScore, "only the host scores");
            Assert.AreEqual(2, result.Pops);
            Assert.IsTrue(result.BoardCleared);
        }

        [Test]
        public void ApplyEffectHits_BombBesidePlated_PopsIt_AsItPopsAnUnbreakable()
        {
            // A bomb's hex neighbours take a guaranteed piercing kill: an int.MaxValue tough neighbour (an
            // unbreakable, the control below) dies to it, and so does the plated balloon.
            var host = ShotBoardBuilder.Green(
                new Vector2(0f, 1f), 0.05f, "Red", 1, 1, new Vector2Int(3, 3), 0, 0, 0f, false, null,
                item: ItemType.Bomb);
            var board = new[] { host, Plated(new Vector2(0.5f, 1f), 0.05f, new Vector2Int(2, 3)) };
            var workingSet = new ShotBalloonState[board.Length];

            var result = ShotSimulator.Simulate(
                board, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: workingSet, items: CreateBombItemLayer());

            Assert.AreEqual(2, result.Pops, "the plated balloon pops beside the bomb");
            Assert.AreEqual(1, result.RawScore, "only the host scores");
            Assert.IsTrue(result.BoardCleared);

            var control = new[]
            {
                host,
                ShotBoardBuilder.Tough(
                    new Vector2(0.5f, 1f), 0.05f, 5, int.MaxValue, new Vector2Int(2, 3), 0, 0, 0f, false, null),
            };
            var controlResult = ShotSimulator.Simulate(
                control, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: new ShotBalloonState[control.Length], items: CreateBombItemLayer());

            Assert.AreEqual(2, controlResult.Pops, "the same board with an unbreakable beside the bomb does pop it");
        }

        [Test]
        public void ApplyEffectHits_LaserThroughPlated_PopsItScorelessWithTheRest()
        {
            var board = new[]
            {
                ShotBoardBuilder.Green(
                    new Vector2(1f, 1f), 0.05f, "Blue", 3, 1, new Vector2Int(1, 0), 0, 0, 0f, false, null),
                Plated(new Vector2(2f, 1f), 0.05f, new Vector2Int(2, 0)),
                ShotBoardBuilder.Green(
                    new Vector2(0f, 1f), 0.05f, "Red", 2, 1, new Vector2Int(9, 9), 0, 0, 0f, false, null,
                    item: ItemType.Laser),
            };
            var workingSet = new ShotBalloonState[board.Length];

            var result = ShotSimulator.Simulate(
                board, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: workingSet, items: CreateLaserItemLayer());

            Assert.AreEqual(3, result.Pops, "host, the Blue and the plated balloon in the corridor");
            Assert.AreEqual(1, RewardPops(result));
            Assert.AreEqual(0, OtherPlatedPops(result));
            Assert.AreEqual(2 + 3, result.RawScore, "the plated balloon pays nothing");
            Assert.AreEqual(0, result.ToughsCleared);
            Assert.IsTrue(result.BoardCleared);
        }
    }
}
