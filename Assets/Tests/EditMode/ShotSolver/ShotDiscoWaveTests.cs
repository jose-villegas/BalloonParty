using System.Collections.Generic;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Palette;
using BalloonParty.Item.Effects;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Solver;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.ShotSolver
{
    /// <summary>
    ///     The disco ball's wave in the solver (PLAN-DiscoBalloon phase 3): the ball's pop paints every paintable
    ///     balloon rainbow at once, and the shot's later pops of what it painted pay every allowed colour at its streak
    ///     as it stands, never climbing it. Mirrors <c>DiscoWave</c>, <c>ShotPaintLedger</c> and
    ///     <c>ScoreController</c>'s OwnPaint.
    /// </summary>
    [TestFixture]
    public class ShotDiscoWaveTests
    {
        private static readonly Vector4 WideOpenWalls = new(1000f, 1000f, -1000f, -1000f);
        private static readonly string[] TwoColors = { "Red", "Blue" };

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

        // A Snipe host arms the pierce; two Reds build a x2 streak; the pierce pops the disco ball, which paints the
        // two Reds beyond it. Each pays both colours at the x2 the streak stood at: 10*2*2 and 100*2*2. Painted
        // balloons that climbed the streak as any rainbow pop would pay 10*2*3 and 100*2*4.
        [Test]
        public void Simulate_PopsOfWhatTheShotsDiscoBallPainted_PayEveryColourAtTheStreakAndNeverClimbIt()
        {
            var board = new[]
            {
                ShotBoardBuilder.Green(new Vector2(0f, 1f), 0.05f, "Red", 1, 1, item: ItemType.Snipe),
                ShotBoardBuilder.Green(new Vector2(0f, 1.5f), 0.05f, "Red", 1, 1),
                ShotBalloonSnapshot.ForDiscoTarget(new Vector2(0f, 2f), 0.05f, null),
                ShotBoardBuilder.Green(new Vector2(0f, 3f), 0.05f, "Red", 10, 1),
                ShotBoardBuilder.Green(new Vector2(0f, 4f), 0.05f, "Red", 100, 1),
            };
            var workingSet = new ShotBalloonState[board.Length];

            var result = ShotSimulator.Simulate(
                board, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: workingSet, rippleDamage: 1, allowedColors: TwoColors, rainbowColorId: GamePalette.RainbowColorId,
                items: CreateSnipeItemLayer());

            Assert.AreEqual(1, result.DiscoPops);
            Assert.AreEqual(1 + 2 + 10 * 2 * 2 + 100 * 2 * 2, result.RawScore);
        }

        // The wave reaches the whole board, not only the shot's line: an off-line Blue is rainbow after the pop.
        [Test]
        public void Simulate_TheDiscoBallsPop_PaintsPaintableBalloonsOffTheShotsLine()
        {
            var board = new[]
            {
                ShotBoardBuilder.Green(new Vector2(0f, 1f), 0.05f, "Red", 1, 1, item: ItemType.Snipe),
                ShotBalloonSnapshot.ForDiscoTarget(new Vector2(0f, 2f), 0.05f, null),
                ShotBoardBuilder.Green(new Vector2(50f, 50f), 0.05f, "Blue", 1, 1),
            };
            var workingSet = new ShotBalloonState[board.Length];

            ShotSimulator.Simulate(
                board, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: workingSet, rippleDamage: 1, allowedColors: TwoColors, rainbowColorId: GamePalette.RainbowColorId,
                items: CreateSnipeItemLayer());

            // Two pops swap-removed the line's balloons, leaving the off-line Blue first in the working set.
            Assert.IsTrue(workingSet[0].IsRainbow);
            Assert.IsTrue(workingSet[0].OwnPaint);
        }
    }
}
