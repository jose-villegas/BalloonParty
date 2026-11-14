using System.Collections.Generic;
using BalloonParty.Configuration.Items;
using BalloonParty.Item.Effects;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Solver;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.ShotSolver
{
    /// <summary>The heart reward balloon in the solver (PLAN-RewardBalloons phase 4): any hit pops it, the shot's
    /// or an item's, and its pop pays no score and leaves the streak alone. Mirrors <c>HeartBalloonModel</c>.</summary>
    [TestFixture]
    public class ShotHeartBalloonTests
    {
        private static readonly Vector4 WideOpenWalls = new(1000f, 1000f, -1000f, -1000f);

        private static ShotBalloonSnapshot Heart(Vector2 position, float radius, Vector2Int? slot = null)
        {
            var balance = slot.HasValue
                ? new BalanceProfile(slot.Value, 0, 0, 0f, false, null)
                : (BalanceProfile?)null;
            return ShotBalloonSnapshot.ForHeartTarget(position, radius, balance);
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

        [Test]
        public void Simulate_PlainShotThroughAHeart_PopsItScorelessAndKeepsTheStreak()
        {
            // The two Reds either side are one same-colour streak only if the heart touched neither the streak
            // count nor the colour: 1*1 for the first, 10*2 for the second. A heart that broke the streak like a
            // tough pop would leave 1 + 10.
            var board = new[]
            {
                ShotBoardBuilder.Green(new Vector2(0f, 1f), 0.05f, "Red", 1, 1),
                Heart(new Vector2(0f, 2f), 0.05f),
                ShotBoardBuilder.Green(new Vector2(0f, 3f), 0.05f, "Red", 10, 1),
            };
            var workingSet = new ShotBalloonState[board.Length];

            var result = ShotSimulator.Simulate(
                board, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: workingSet);

            Assert.AreEqual(3, result.Pops);
            Assert.AreEqual(1 + 10 * 2, result.RawScore, "the heart pays nothing and the streak climbs through it");
            Assert.AreEqual(0, result.ToughsCleared, "scoreless: not tallied as a tough clear either");
            Assert.IsTrue(result.BoardCleared);
        }

        [Test]
        public void ApplyEffectHits_BombBesideAHeart_PopsIt()
        {
            var host = ShotBoardBuilder.Green(
                new Vector2(0f, 1f), 0.05f, "Red", 1, 1, new Vector2Int(3, 3), 0, 0, 0f, false, null,
                item: ItemType.Bomb);
            var board = new[] { host, Heart(new Vector2(0.5f, 1f), 0.05f, new Vector2Int(2, 3)) };
            var workingSet = new ShotBalloonState[board.Length];

            var result = ShotSimulator.Simulate(
                board, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: workingSet, items: CreateBombItemLayer());

            Assert.AreEqual(2, result.Pops, "the host, then the heart beside it");
            Assert.AreEqual(1, result.RawScore, "only the host scores");
            Assert.IsTrue(result.BoardCleared);
        }
    }
}
