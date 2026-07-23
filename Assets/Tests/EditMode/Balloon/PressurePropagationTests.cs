using System.Collections.Generic;
using BalloonParty.Balloon.Controller;
using BalloonParty.Balloon.Model;
using BalloonParty.Shared;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using BalloonParty.Configuration.Palette;

namespace BalloonParty.Tests.Balloon
{
    [TestFixture]
    public class PressurePropagationTests
    {
        private readonly List<PressurePropagation.Move> _moves = new();

        private SlotGrid _grid;
        private PressurePropagation _propagation;

        [SetUp]
        public void SetUp()
        {
            var config = Substitute.For<ISlotGridConfig>();
            config.SlotsSize.Returns(new Vector2Int(3, 3));
            // Non-zero separation: zero degenerates every world-space shove direction to zero.
            config.SlotSeparation.Returns(new Vector2(1f, 1f));
            config.SlotsOffset.Returns(Vector2.zero);

            _grid = new SlotGrid(config, new BalancePathHolder());
            _propagation = new PressurePropagation(_grid, new GridBalanceQuery(_grid).Evaluator);
        }

        [Test]
        public void StraightChain_ResolvesToTheUpGap_MoverFirst()
        {
            PlaceSimple(0, 2);
            PlaceSimple(0, 1);
            PlaceSimple(1, 2); // no side escape for the seed — the chain must go up

            Assert.IsTrue(_propagation.TryResolve(0, _moves));
            Assert.AreEqual(2, _moves.Count);
            Assert.AreEqual(new Vector2Int(0, 1), _moves[0].From, "the mover comes first");
            Assert.AreEqual(new Vector2Int(0, 2), _moves[^1].From, "the seed comes last");
            Assert.AreEqual(new Vector2Int(0, 1), _moves[^1].To, "the seed steps into the vacated cell");
            ExecuteMovesAssertingEmptyDestinations();
            Assert.IsTrue(_grid.IsEmpty(0, 2), "the seed cell must end up free");
        }

        [Test]
        public void BentChain_UpBlocked_TakesTheSideGap()
        {
            FillColumn(0); // column 0 solid — no up gap
            PlaceSimple(1, 0);
            PlaceSimple(1, 1); // (1,2) left empty: the only escape is sideways

            Assert.IsTrue(_propagation.TryResolve(0, _moves));
            Assert.AreEqual(1, _moves.Count);
            Assert.AreEqual(new Vector2Int(0, 2), _moves[0].From);
            Assert.AreEqual(new Vector2Int(1, 2), _moves[0].To);
        }

        [Test]
        public void FullBoard_NoGap_Fails()
        {
            for (var col = 0; col < _grid.Columns; col++)
            {
                FillColumn(col);
            }

            Assert.IsFalse(_propagation.TryResolve(0, _moves));
        }

        [Test]
        public void RelocationTerminal_UnbreakableVacates_EndingTheChain()
        {
            PlaceSimple(0, 2); // seed
            Place(new UnbreakableBalloonModel(new BalloonModelConfig()), 0, 1);
            PlaceBlock(1, 2); // the seed's only other escape — force the chain into the unbreakable

            Assert.IsTrue(_propagation.TryResolve(0, _moves));
            Assert.AreEqual(2, _moves.Count);
            Assert.IsInstanceOf<UnbreakableBalloonModel>(_moves[0].Actor);
            Assert.AreEqual(new Vector2Int(0, 1), _moves[0].From);
            Assert.AreEqual(new Vector2Int(2, 0), _moves[0].To, "RelocateFarthest jumps to the most distant gap");
            Assert.AreEqual(new Vector2Int(0, 1), _moves[^1].To, "the seed steps into the vacated cell");
            ExecuteMovesAssertingEmptyDestinations();
        }

        [Test]
        public void Heaviness_RoutesTheChainThroughLightMovers()
        {
            PlaceSimple(1, 2); // seed
            PlaceSimple(0, 2);
            PlaceSimple(2, 2);
            // Both up neighbours are equally aligned; without the heaviness cost the tough (earlier in
            // the neighbour buffer) would win the tie and be shoved.
            Place(new ToughBalloonModel(new BalloonModelConfig(maxBalanceSteps: 1)), 1, 1);
            PlaceSimple(0, 1);

            Assert.IsTrue(_propagation.TryResolve(1, _moves));
            Assert.AreEqual(2, _moves.Count);
            Assert.AreEqual(new Vector2Int(0, 1), _moves[0].From, "the light chain must be chosen");
            foreach (var move in _moves)
            {
                Assert.AreNotEqual(new Vector2Int(1, 1), move.From, "the heavy tough must not be shoved");
            }
        }

        [Test]
        public void Static_BlocksPropagation()
        {
            PlaceSimple(0, 2);
            PlaceBlock(0, 1);
            PlaceBlock(1, 2);
            PlaceSimple(2, 0); // gaps remain elsewhere, unreachable through the statics

            Assert.IsFalse(_propagation.TryResolve(0, _moves));
        }

        [Test]
        public void EntryBlockedByImmovable_Fails()
        {
            PlaceBlock(0, 2);

            Assert.IsFalse(_propagation.TryResolve(0, _moves));
        }

        [Test]
        public void EmptyColumn_NothingToShove_Fails()
        {
            Assert.IsFalse(_propagation.TryResolve(0, _moves));
        }

        [Test]
        public void PuffAtEntry_SeedsTheBalloonAboveIt()
        {
            PlacePassThrough(0, 2); // a rising balloon passes through the puff — it is not the blocker
            PlaceSimple(0, 1);

            Assert.IsTrue(_propagation.TryResolve(0, _moves));
            Assert.AreEqual(new Vector2Int(0, 1), _moves[^1].From);
        }

        [Test]
        public void RelocateNearest_PicksTheClosestGap()
        {
            Place(new BubbleClusterModel(new BalloonModelConfig(), Substitute.For<IGamePalette>()), 0, 2);
            FillExcept(new Vector2Int(0, 2), new Vector2Int(1, 2), new Vector2Int(2, 0));

            Assert.IsTrue(_propagation.TryResolve(0, _moves));
            Assert.AreEqual(1, _moves.Count);
            Assert.AreEqual(new Vector2Int(1, 2), _moves[0].To);
        }

        [Test]
        public void RelocateFarthest_PicksTheMostDistantGap()
        {
            Place(new UnbreakableBalloonModel(new BalloonModelConfig()), 0, 2);
            FillExcept(new Vector2Int(0, 2), new Vector2Int(1, 2), new Vector2Int(2, 0));

            Assert.IsTrue(_propagation.TryResolve(0, _moves));
            Assert.AreEqual(1, _moves.Count);
            Assert.AreEqual(new Vector2Int(2, 0), _moves[0].To);
        }

        // Replays the moves in order, proving each destination is vacant when its move executes.
        private void ExecuteMovesAssertingEmptyDestinations()
        {
            foreach (var move in _moves)
            {
                Assert.IsTrue(_grid.IsEmpty(move.To.x, move.To.y), $"destination {move.To} occupied");
                var view = _grid.ViewAt(move.From);
                _grid.Remove(move.From);
                _grid.Place(move.Actor, view, move.To);
            }
        }

        private void FillColumn(int col)
        {
            for (var row = 0; row < _grid.Rows; row++)
            {
                PlaceSimple(col, row);
            }
        }

        // Blocks every cell except the listed ones (already occupied or deliberately kept empty).
        private void FillExcept(params Vector2Int[] skip)
        {
            var skipSet = new HashSet<Vector2Int>(skip);

            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    if (!skipSet.Contains(new Vector2Int(col, row)))
                    {
                        PlaceBlock(col, row);
                    }
                }
            }
        }

        private void PlaceSimple(int col, int row)
        {
            Place(new BalloonModel(), col, row);
        }

        private void PlaceBlock(int col, int row)
        {
            _grid.Place(Substitute.For<IWriteableSlotActor>(), null, new Vector2Int(col, row));
        }

        private void PlacePassThrough(int col, int row)
        {
            _grid.Place(Substitute.For<IWriteableSlotActor, IPassThrough>(), null, new Vector2Int(col, row));
        }

        private void Place(IWriteableSlotActor actor, int col, int row)
        {
            _grid.Place(actor, null, new Vector2Int(col, row));
        }
    }
}
