using System.Collections.Generic;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Items;
using BalloonParty.Item.Effects;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Solver;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.ShotSolver
{
    /// <summary>The golden reward balloon in the solver (PLAN-RewardBalloons phase 2): it deflects the
    /// ordinary shot forever, pops only to a piercing shot, pays no score, leaves the streak alone, and is
    /// untouched by item effects. Mirrors <c>GoldenBalloonModel.EvaluateHit</c>.</summary>
    [TestFixture]
    public class ShotGoldenBalloonTests
    {
        private static readonly Vector4 WideOpenWalls = new(1000f, 1000f, -1000f, -1000f);

        private static ShotBalloonSnapshot Golden(Vector2 position, float radius, Vector2Int? slot = null)
        {
            var balance = slot.HasValue
                ? new BalanceProfile(slot.Value, 0, 0, 0f, false, null)
                : (BalanceProfile?)null;
            return ShotBalloonSnapshot.ForGoldenTarget(position, radius, balance);
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
        public void Simulate_PlainShotAtGolden_DeflectsEveryTimeAndNeverPops()
        {
            // Dead-centre shot at a golden with a bottom wall one shield away: deflect, bounce, deflect
            // again, then the second bounce outruns the shield. A plain shot never pops it however often it
            // comes back, unlike a two-hit tough which would have popped on the second contact.
            var walls = new Vector4(1000f, 1000f, -1f, -1000f);
            var board = new[] { Golden(new Vector2(0f, 2f), 0.3f) };
            var workingSet = new ShotBalloonState[board.Length];

            var result = ShotSimulator.Simulate(
                board, walls, Vector2.zero, Vector2.up, startingShields: 1, projectileContactRadius: 0f,
                workingSet: workingSet);

            Assert.AreEqual(0, result.Pops);
            Assert.AreEqual(0, result.RawScore);
            Assert.AreEqual(0, result.ToughsCleared);
            Assert.IsFalse(result.BoardCleared);
            Assert.IsTrue(result.Died);
        }

        [Test]
        public void Simulate_PiercingShotThroughGolden_PopsItScorelessAndKeepsTheStreak()
        {
            // A Snipe host arms the pierce, which plows the golden. The two Reds either side are one
            // same-colour streak only if the golden touched neither the streak count nor the colour: 1*1
            // for the first, 10*2 for the second. A golden that reset the streak like a tough pop would
            // leave 1 + 10.
            var board = new[]
            {
                ShotBoardBuilder.Green(new Vector2(0f, 1f), 0.05f, "Red", 1, 1, item: ItemType.Snipe),
                Golden(new Vector2(0f, 2f), 0.05f),
                ShotBoardBuilder.Green(new Vector2(0f, 3f), 0.05f, "Red", 10, 1),
            };
            var workingSet = new ShotBalloonState[board.Length];

            var result = ShotSimulator.Simulate(
                board, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: workingSet, items: CreateSnipeItemLayer());

            Assert.AreEqual(1 + 10 * 2, result.RawScore, "the golden pays nothing and the streak climbs through it");
            Assert.AreEqual(3, result.Pops);
            Assert.AreEqual(0, result.ToughsCleared, "scoreless: not tallied as a tough clear either");
            Assert.IsTrue(result.BoardCleared);
        }

        [Test]
        public void Simulate_PiercingShotThroughGoldenAlone_ClearsBoardForNothing()
        {
            var board = new[]
            {
                ShotBoardBuilder.Green(new Vector2(0f, 1f), 0.05f, "Red", 1, 1, item: ItemType.Snipe),
                Golden(new Vector2(0f, 2f), 0.05f),
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
        public void ApplyEffectHits_BombBesideGolden_LeavesItStanding()
        {
            // A bomb's hex neighbours take a guaranteed piercing kill (an int.MaxValue tough neighbour dies
            // to it, the control below), but the golden answers an item's hit with None.
            var host = ShotBoardBuilder.Green(
                new Vector2(0f, 1f), 0.05f, "Red", 1, 1, new Vector2Int(3, 3), 0, 0, 0f, false, null,
                item: ItemType.Bomb);
            var board = new[] { host, Golden(new Vector2(0.5f, 1f), 0.05f, new Vector2Int(2, 3)) };
            var workingSet = new ShotBalloonState[board.Length];

            var result = ShotSimulator.Simulate(
                board, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: workingSet, items: CreateBombItemLayer());

            Assert.AreEqual(1, result.Pops, "only the host pops");
            Assert.IsFalse(result.BoardCleared);

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
        public void ApplyEffectHits_LaserThroughGolden_LeavesItStandingAndStillPopsTheRest()
        {
            var board = new[]
            {
                ShotBoardBuilder.Green(
                    new Vector2(1f, 1f), 0.05f, "Blue", 3, 1, new Vector2Int(1, 0), 0, 0, 0f, false, null),
                Golden(new Vector2(2f, 1f), 0.05f, new Vector2Int(2, 0)),
                ShotBoardBuilder.Green(
                    new Vector2(0f, 1f), 0.05f, "Red", 2, 1, new Vector2Int(9, 9), 0, 0, 0f, false, null,
                    item: ItemType.Laser),
            };
            var workingSet = new ShotBalloonState[board.Length];

            var result = ShotSimulator.Simulate(
                board, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: workingSet, items: CreateLaserItemLayer());

            Assert.AreEqual(2, result.Pops, "host + the Blue in the corridor; the golden is skipped");
            Assert.AreEqual(2 + 3, result.RawScore);
            Assert.AreEqual(0, result.ToughsCleared);
            Assert.IsFalse(result.BoardCleared);
        }
    }
}
