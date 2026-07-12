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

namespace BalloonParty.Tests.Balloon
{
    /// <summary>
    ///     Saturation guarantee: repeated resolve → execute → spawn must fill every empty slot that is
    ///     not sealed behind statics. A failed resolve charges the player a hit point, so failing while
    ///     reachable space remains is a bug, never acceptable pruning.
    /// </summary>
    [TestFixture]
    public class PressureSaturationTests
    {
        private readonly List<PressurePropagation.Move> _moves = new();

        private SlotGrid _grid;
        private PressurePropagation _propagation;

        [Test]
        public void ScatteredGaps_RoundRobinPressure_FillsTheBoard()
        {
            CreateGrid(5, 5);
            FillExcept(
                new Vector2Int(1, 0),
                new Vector2Int(3, 1),
                new Vector2Int(0, 2),
                new Vector2Int(2, 3),
                new Vector2Int(4, 4));

            Saturate(0, 1, 2, 3, 4);

            AssertBoardFullExcept();
        }

        [Test]
        public void BentPocket_SideThenUpChain_Fills()
        {
            CreateGrid(3, 4);
            // Both up neighbours of the seed (0,3) are statics: the chain must bend sideways to
            // (1,3), then up-right to (2,2), then up-left into the pocket at (1,1).
            PlaceBlock(0, 2);
            PlaceBlock(1, 2);
            FillExcept(new Vector2Int(0, 2), new Vector2Int(1, 2), new Vector2Int(1, 1));

            Saturate(0);

            AssertBoardFullExcept();
        }

        [Test]
        public void CornerGap_PressureFromTheFarColumn_Fills()
        {
            CreateGrid(3, 3);
            FillExcept(new Vector2Int(2, 0));

            Saturate(0);

            AssertBoardFullExcept();
        }

        [Test]
        public void BackflowPocket_GapBelowTheSeed_ResolvesViaTheRelaxedPass()
        {
            CreateGrid(3, 3);
            // The gap sits below the seed and its only other neighbour is static: every route into
            // it has alignment < 0 against the incoming shove, so the strict directed pass fails.
            PlaceBlock(1, 2);
            FillExcept(new Vector2Int(0, 2), new Vector2Int(1, 2));

            Assert.IsTrue(_propagation.TryResolve(0, _moves), "the relaxed fallback must find the pocket");
            Assert.AreEqual(1, _moves.Count, "the seed itself drops into the pocket");
            Assert.AreEqual(new Vector2Int(0, 1), _moves[0].From);
            Assert.AreEqual(new Vector2Int(0, 2), _moves[0].To);

            ExecuteMoves();
            PlaceSimple(_moves[^1].From.x, _moves[^1].From.y);

            AssertBoardFullExcept();
        }

        [Test]
        public void HeavyWall_ToughChainsAreCostedNotGated_Fills()
        {
            CreateGrid(3, 3);
            // The only routes to the gap pass through a solid row of MaxBalanceSteps=1 toughs.
            for (var col = 0; col < 3; col++)
            {
                Place(new ToughBalloonModel(new BalloonModelConfig(maxBalanceSteps: 1)), col, 1);
                PlaceSimple(col, 2);
            }

            PlaceSimple(0, 0);
            PlaceSimple(2, 0);

            Saturate(1);

            AssertBoardFullExcept();
        }

        [Test]
        public void RelocationTerminal_UnbreakableRelocationsStillSaturate()
        {
            CreateGrid(3, 3);
            var unbreakable = new UnbreakableBalloonModel(new BalloonModelConfig());
            Place(unbreakable, 1, 1);
            FillExcept(new Vector2Int(1, 1), new Vector2Int(0, 0), new Vector2Int(2, 0));

            Saturate(1);

            AssertBoardFullExcept();
            Assert.AreSame(unbreakable, _grid.At(new Vector2Int(0, 0)), "RelocateFarthest jumps to the distant gap");
        }

        [Test]
        public void StaticSeal_OnlyTheSealedPocketStaysEmpty()
        {
            CreateGrid(3, 3);
            // (0,0)'s only neighbours are (1,0) and (0,1): statics there seal the pocket.
            PlaceBlock(1, 0);
            PlaceBlock(0, 1);
            FillExcept(
                new Vector2Int(1, 0),
                new Vector2Int(0, 1),
                new Vector2Int(0, 0),
                new Vector2Int(2, 0));

            Saturate(0, 1, 2);

            AssertBoardFullExcept(new Vector2Int(0, 0));
            for (var col = 0; col < 3; col++)
            {
                Assert.IsFalse(
                    _propagation.TryResolve(col, _moves),
                    $"column {col} must report false once only sealed slots remain");
            }
        }

        private void CreateGrid(int columns, int rows)
        {
            var config = Substitute.For<IGameConfiguration>();
            config.SlotsSize.Returns(new Vector2Int(columns, rows));
            // Non-zero separation: zero degenerates every world-space shove direction to zero.
            config.SlotSeparation.Returns(new Vector2(1f, 1f));
            config.SlotsOffset.Returns(Vector2.zero);

            _grid = new SlotGrid(config, new BalancePathHolder());
            _propagation = new PressurePropagation(_grid, new GridBalanceQuery(_grid).Evaluator);
        }

        // Resolve → execute → spawn round-robin over the given columns until no unsealed empty slot
        // remains; a resolve returning false while unsealed space exists fails the test.
        private void Saturate(params int[] columns)
        {
            var safety = _grid.Columns * _grid.Rows * 4;

            while (CountUnsealedEmpties() > 0)
            {
                Assert.Greater(safety--, 0, "saturation loop did not terminate");

                var progressed = false;
                foreach (var column in columns)
                {
                    if (CountUnsealedEmpties() == 0)
                    {
                        break;
                    }

                    if (!ColumnCanSeed(column))
                    {
                        continue;
                    }

                    Assert.IsTrue(
                        _propagation.TryResolve(column, _moves),
                        $"resolve failed on column {column} with {CountUnsealedEmpties()} unsealed empties");
                    ExecuteMoves();
                    PlaceSimple(_moves[^1].From.x, _moves[^1].From.y);
                    progressed = true;
                }

                Assert.IsTrue(progressed, "no column could seed a resolve while unsealed empties remain");
            }
        }

        // Replays the moves exactly like BalloonBalancer: Remove(from) then Place(to) in list order,
        // proving each destination is vacant when its move executes.
        private void ExecuteMoves()
        {
            foreach (var move in _moves)
            {
                Assert.IsTrue(_grid.IsEmpty(move.To.x, move.To.y), $"destination {move.To} occupied");
                var view = _grid.ViewAt(move.From);
                _grid.Remove(move.From);
                _grid.Place(move.Actor, view, move.To);
            }
        }

        private void AssertBoardFullExcept(params Vector2Int[] expectedEmpty)
        {
            var emptySet = new HashSet<Vector2Int>(expectedEmpty);

            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    var slot = new Vector2Int(col, row);
                    Assert.AreEqual(
                        emptySet.Contains(slot),
                        _grid.IsEmpty(col, row),
                        $"slot {slot} occupancy mismatch");
                }
            }
        }

        // Mirrors TryFindLowestBlocker: the column can host a resolve when its first non-traversable
        // occupant walking up from the entry is pressure-movable.
        private bool ColumnCanSeed(int column)
        {
            for (var row = _grid.Rows - 1; row >= 0; row--)
            {
                if (_grid.IsTraversable(column, row))
                {
                    continue;
                }

                return _grid.At(new Vector2Int(column, row)) is IPressureMovable;
            }

            return false;
        }

        private int CountUnsealedEmpties()
        {
            var count = 0;
            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    if (_grid.IsEmpty(col, row) && !IsSealed(new Vector2Int(col, row)))
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        // An empty region is sealed when nothing pressure-movable borders it — only statics and the
        // board edge. Sealed pockets are the one legitimate reason for a slot to stay empty.
        private bool IsSealed(Vector2Int start)
        {
            var region = new HashSet<Vector2Int> { start };
            var frontier = new Stack<Vector2Int>();
            frontier.Push(start);

            while (frontier.Count > 0)
            {
                var cell = frontier.Pop();
                foreach (var neighbour in HexCoordinates.HexNeighborIndices(cell.x, cell.y))
                {
                    if (!_grid.InBounds(neighbour))
                    {
                        continue;
                    }

                    if (_grid.IsEmpty(neighbour.x, neighbour.y))
                    {
                        if (region.Add(neighbour))
                        {
                            frontier.Push(neighbour);
                        }
                    }
                    else if (_grid.At(neighbour) is IPressureMovable)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        // Occupies every cell except the listed ones (already placed or deliberately kept empty).
        private void FillExcept(params Vector2Int[] skip)
        {
            var skipSet = new HashSet<Vector2Int>(skip);

            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    if (!skipSet.Contains(new Vector2Int(col, row)))
                    {
                        PlaceSimple(col, row);
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

        private void Place(IWriteableSlotActor actor, int col, int row)
        {
            _grid.Place(actor, null, new Vector2Int(col, row));
        }
    }
}
