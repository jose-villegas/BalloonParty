using System.Collections.Generic;
using BalloonParty.Configuration.Items;
using BalloonParty.Item.Effects;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Solver;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.ShotSolver
{
    /// <summary>The nuke reward balloon in the solver (PLAN-NukeBalloon): the ordinary shot deflects off it and wears
    /// it down, and its last dent arms it instead of popping it, the ball bouncing off. A piercing shot plows it and
    /// arms it on the way. Armed, it stays on the board and deflects every ball; the arming is counted once, apart, as
    /// <c>NukePops</c>, paying no score. Its blast comes a fuse later, after the flight, so the board is left as it
    /// is. Mirrors <c>NukeBalloonModel.EvaluateHit</c>. Plain item damage not wearing a nuke is not covered here: the
    /// only non-piercing damage item (Lightning) matches by colour and a nuke has none.</summary>
    [TestFixture]
    public class ShotNukeBalloonTests
    {
        private static readonly Vector4 WideOpenWalls = new(1000f, 1000f, -1000f, -1000f);

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

        [Test]
        public void Simulate_PlainShotAtThreeHitNuke_DeflectsAndItSurvivesUnarmed()
        {
            // Dead-centre shot with a bottom wall one shield away: deflect (3 to 2), bounce, deflect (2 to 1), then
            // the second bounce outruns the shield. It never arms while it has more than one hit left.
            var walls = new Vector4(1000f, 1000f, -1f, -1000f);
            var board = new[] { ShotBalloonSnapshot.ForNukeTarget(new Vector2(0f, 2f), 0.3f, 3) };

            var result = ShotSimulator.Simulate(
                board, walls, Vector2.zero, Vector2.up, startingShields: 1, projectileContactRadius: 0f,
                workingSet: new ShotBalloonState[board.Length], rippleDamage: 1);

            Assert.AreEqual(0, result.Pops);
            Assert.AreEqual(0, result.NukePops);
            Assert.IsTrue(result.Died);
        }

        // The same shot at a two-hit nuke: the first deflect leaves one hit, so the returning shot arms it and
        // bounces off again. That the three-hit one above stayed unarmed on the same path shows each deflect cost a
        // hit.
        [Test]
        public void Simulate_PlainShotAtTwoHitNuke_DeflectsOnceThenArmsItOnTheReturn()
        {
            var walls = new Vector4(1000f, 1000f, -1f, -1000f);
            var board = new[] { ShotBalloonSnapshot.ForNukeTarget(new Vector2(0f, 2f), 0.3f, 2) };

            var result = ShotSimulator.Simulate(
                board, walls, Vector2.zero, Vector2.up, startingShields: 1, projectileContactRadius: 0f,
                workingSet: new ShotBalloonState[board.Length], rippleDamage: 1);

            Assert.AreEqual(0, result.Pops, "arming is no pop");
            Assert.AreEqual(1, result.NukePops);
            Assert.IsTrue(result.Died, "the arming hit bounced the ball back down at the wall");
        }

        [Test]
        public void Simulate_PlainShotAtOneHitNuke_ArmsItScorelessAndLeavesItStanding()
        {
            var board = new[] { ShotBalloonSnapshot.ForNukeTarget(new Vector2(0f, 1f), 0.05f, 1) };

            var result = ShotSimulator.Simulate(
                board, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: new ShotBalloonState[board.Length], rippleDamage: 1);

            Assert.AreEqual(0, result.Pops);
            Assert.AreEqual(1, result.NukePops);
            Assert.AreEqual(0, result.RawScore);
            Assert.AreEqual(0, result.ToughsCleared, "scoreless: not tallied as a tough clear");
            Assert.AreEqual(0, result.GoldenPops);
            Assert.AreEqual(0, result.HeartPops);
            Assert.AreEqual(0, result.DiscoPops);
            Assert.IsFalse(result.BoardCleared, "the armed nuke is still on the board");
        }

        // Up, arm, back down off the wall, up into the armed nuke, and again: every return bounces off it, and the
        // arming is counted once.
        [Test]
        public void Simulate_AnArmedNuke_DeflectsEveryReturnAndCountsOnce()
        {
            var walls = new Vector4(1000f, 1000f, -1f, -1000f);
            var board = new[] { ShotBalloonSnapshot.ForNukeTarget(new Vector2(0f, 2f), 0.3f, 1) };

            var result = ShotSimulator.Simulate(
                board, walls, Vector2.zero, Vector2.up, startingShields: 2, projectileContactRadius: 0f,
                workingSet: new ShotBalloonState[board.Length], rippleDamage: 1);

            Assert.AreEqual(0, result.Pops);
            Assert.AreEqual(1, result.NukePops);
            Assert.IsTrue(result.Died, "the ball never got past it");
        }

        [Test]
        public void Simulate_PiercingShotThroughNuke_PlowsOnArmingItAndPopsWhatIsBehind()
        {
            // A Snipe host arms the pierce. The nuke pays nothing and stays; the ball plows on to the Red behind it.
            var board = new[]
            {
                ShotBoardBuilder.Green(new Vector2(0f, 1f), 0.05f, "Red", 1, 1, item: ItemType.Snipe),
                ShotBalloonSnapshot.ForNukeTarget(new Vector2(0f, 2f), 0.05f, 3),
                ShotBoardBuilder.Green(new Vector2(0f, 3f), 0.05f, "Red", 10, 1),
            };

            var result = ShotSimulator.Simulate(
                board, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: new ShotBalloonState[board.Length], rippleDamage: 1, items: CreateSnipeItemLayer());

            Assert.AreEqual(2, result.Pops, "the host and the Red behind the nuke");
            Assert.Greater(result.RawScore, 1, "the Red behind it scored");
            Assert.AreEqual(1, result.NukePops);
            Assert.AreEqual(0, result.ToughsCleared);
            Assert.IsFalse(result.BoardCleared, "the armed nuke is still on the board");
        }

        [Test]
        public void Simulate_ArmingANuke_ClearsNothingWithinTheFlight()
        {
            // The Reds sit well off the shot's line. The blast that would take them comes after the fuse, past the
            // flight.
            var board = new[]
            {
                ShotBalloonSnapshot.ForNukeTarget(new Vector2(0f, 1f), 0.05f, 1),
                ShotBoardBuilder.Green(new Vector2(5f, 5f), 0.05f, "Red", 100, 1),
                ShotBoardBuilder.Green(new Vector2(-5f, 6f), 0.05f, "Blue", 100, 2),
            };

            var result = ShotSimulator.Simulate(
                board, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: new ShotBalloonState[board.Length], rippleDamage: 1);

            Assert.AreEqual(0, result.RawScore);
            Assert.AreEqual(0, result.Pops);
            Assert.AreEqual(1, result.NukePops);
            Assert.IsFalse(result.BoardCleared);
        }
    }
}
