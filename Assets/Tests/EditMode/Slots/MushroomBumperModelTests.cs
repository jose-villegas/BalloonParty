using System.Collections.Generic;
using BalloonParty.Balloon.Controller;
using BalloonParty.Balloon.Model;
using BalloonParty.Shared;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Actor.Archetype;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Slots
{
    /// <summary>
    ///     A meadow mushroom on the grid (<see cref="MushroomBumperModel" />): permanent. It is never hit and
    ///     nothing moves it: not the balance race, not a pressure shove, not a balloon rising past.
    /// </summary>
    [TestFixture]
    public class MushroomBumperModelTests
    {
        private SlotGrid _grid;

        [SetUp]
        public void SetUp()
        {
            var config = Substitute.For<ISlotGridConfig>();
            config.SlotsSize.Returns(new Vector2Int(3, 3));
            // Non-zero separation: zero degenerates every world-space shove direction to zero.
            config.SlotSeparation.Returns(new Vector2(1f, 1f));
            config.SlotsOffset.Returns(Vector2.zero);
            _grid = new SlotGrid(config, new BalancePathHolder());
        }

        [Test]
        public void AMushroom_IsAStaticActorOutsideEveryPipelineThatMovesOrHits()
        {
            var mushroom = new MushroomBumperModel(2);
            IWriteableSlotActor actor = mushroom;

            Assert.AreEqual(SlotActorKind.Static, mushroom.Kind);
            Assert.AreEqual(GridActorType.Mushroom, mushroom.ActorType);
            Assert.AreEqual(2, mushroom.ChainIndex);
            Assert.IsFalse(actor is IDynamicSlotActor, "only dynamic actors are ever moved");
            Assert.IsFalse(actor is IPressureMovable, "a pressure shove stops at it");
            Assert.IsFalse(actor is IPreBalanceRelocatable, "the wave-start roam leaves it");
            Assert.IsFalse(actor is IHitable, "nothing hits it: the bounce is worked out, not collided");
            Assert.IsFalse(actor is IDeflectsShots, "it is no balloon deflector: the aim line reads the bumper field");
            Assert.IsFalse(actor is IPassThrough, "a rising balloon stops under it, as under a bush");
        }

        [Test]
        public void BalanceRace_LeavesAMushroomWhereItStands_ThoughTheSlotAboveItIsEmpty()
        {
            var mushroom = new MushroomBumperModel(0);
            _grid.Place(mushroom, null, new Vector2Int(1, 1));
            var planner = new BalancePlanner(_grid, new GridBalanceQuery(_grid));
            var moves = new List<BalanceMove>();

            planner.Plan(new Dictionary<IWriteableDynamicSlotActor, int>(), moves);

            Assert.AreEqual(0, moves.Count);
            Assert.AreSame(mushroom, _grid.At(new Vector2Int(1, 1)));
            Assert.AreEqual(new Vector2Int(1, 1), mushroom.SlotIndex);
        }

        [Test]
        public void BalanceRace_MovesABalloonAroundAMushroom_AndNeverTheMushroom()
        {
            // The balloon at the bottom of column 1 has the mushroom straight above it and an empty slot beside
            // that: it may go round, the mushroom stays.
            var mushroom = new MushroomBumperModel(0);
            _grid.Place(mushroom, null, new Vector2Int(1, 1));
            var balloon = new BalloonModel();
            _grid.Place(balloon, null, new Vector2Int(1, 2));
            var planner = new BalancePlanner(_grid, new GridBalanceQuery(_grid));
            var moves = new List<BalanceMove>();

            planner.Plan(new Dictionary<IWriteableDynamicSlotActor, int>(), moves);

            foreach (var move in moves)
            {
                Assert.AreSame(balloon, move.Actor, "only the balloon is ever moved");
                Assert.AreNotEqual(new Vector2Int(1, 1), move.To, "and never into the mushroom's slot");
            }

            Assert.AreSame(mushroom, _grid.At(new Vector2Int(1, 1)));
        }

        [Test]
        public void PressureShove_StopsAtAMushroom()
        {
            // Column 0 is the balloon to shove with a mushroom above it, and a mushroom closes the side way out.
            // Gaps remain elsewhere, unreachable through the mushrooms.
            _grid.Place(new BalloonModel(), null, new Vector2Int(0, 2));
            var above = new MushroomBumperModel(0);
            var beside = new MushroomBumperModel(1);
            _grid.Place(above, null, new Vector2Int(0, 1));
            _grid.Place(beside, null, new Vector2Int(1, 2));
            var propagation = new PressurePropagation(_grid, new GridBalanceQuery(_grid).Evaluator);
            var moves = new List<PressurePropagation.Move>();

            Assert.IsFalse(propagation.TryResolve(0, moves));
            Assert.AreSame(above, _grid.At(new Vector2Int(0, 1)));
            Assert.AreSame(beside, _grid.At(new Vector2Int(1, 2)));
        }

        [Test]
        public void AMushroomsSlot_IsNotTraversable()
        {
            _grid.Place(new MushroomBumperModel(0), null, new Vector2Int(1, 1));

            Assert.IsFalse(_grid.IsTraversable(1, 1), "spawn and balance paths do not cross a mushroom");
            Assert.IsTrue(_grid.IsKind(1, 1, SlotActorKind.Static));
        }
    }
}
