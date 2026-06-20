using System.Collections.Generic;
using BalloonParty.Balloon.Controller;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Shared;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Balloon
{
    [TestFixture]
    public class PressureCascadeTests
    {
        private readonly List<Vector2Int> _chain = new();

        [Test]
        public void EntryOccupiedByImmovable_ReturnsFalse()
        {
            var grid = NewGrid(3, 3);
            Place(grid, Block(), 0, 2);

            Assert.IsFalse(PressureCascade.TryFindChain(grid, 0, _chain));
        }

        [Test]
        public void EntryEmptyColumnNotBlocked_ReturnsFalse()
        {
            // (0,2) empty — there is nothing to shove; the caller wouldn't even ask, but guard it.
            var grid = NewGrid(3, 3);

            Assert.IsFalse(PressureCascade.TryFindChain(grid, 0, _chain));
        }

        [Test]
        public void SideHop_OpensColumnIntoAdjacentBottomGap()
        {
            var grid = NewGrid(3, 3);
            FillColumn(grid, 0);          // column 0 solid → blocked
            Place(grid, Movable(), 1, 0);
            Place(grid, Movable(), 1, 1); // (1,2) left empty → a side gap at the bottom

            Assert.IsTrue(PressureCascade.TryFindChain(grid, 0, _chain));
            Assert.AreEqual(new Vector2Int(0, 2), _chain[0]);
            Assert.AreEqual(new Vector2Int(1, 2), _chain[^1]);
            Assert.IsTrue(grid.IsEmpty(_chain[^1].x, _chain[^1].y));
        }

        [Test]
        public void MultiHop_RoutesThroughFilledNeighboursToNearestGap()
        {
            var grid = NewGrid(3, 3);
            FillColumn(grid, 0);
            FillColumn(grid, 1);
            Place(grid, Movable(), 2, 0);
            Place(grid, Movable(), 2, 1); // (2,2) empty — two side hops away

            Assert.IsTrue(PressureCascade.TryFindChain(grid, 0, _chain));
            Assert.AreEqual(3, _chain.Count);
            Assert.AreEqual(new Vector2Int(0, 2), _chain[0]);
            Assert.AreEqual(new Vector2Int(2, 2), _chain[^1]);
        }

        [Test]
        public void FullGrid_ReturnsFalse()
        {
            var grid = NewGrid(3, 3);
            FillColumn(grid, 0);
            FillColumn(grid, 1);
            FillColumn(grid, 2);

            Assert.IsFalse(PressureCascade.TryFindChain(grid, 0, _chain));
        }

        [Test]
        public void ShoveNeighbour_WithBlockedNeighbours_CannotRelieveDistantGap()
        {
            var grid = NewGrid(3, 3);
            Place(grid, Movable(PressureResponse.ShoveNeighbour), 0, 2);
            Place(grid, Block(), 1, 2); // entry's only in-bounds neighbours are this block and (0,1)
            Place(grid, Block(), 0, 1); // ...now both blocked, so no contiguous chain exists
            Place(grid, Movable(), 2, 0); // a gap remains elsewhere, but a normal balloon can't reach it

            Assert.IsFalse(PressureCascade.TryFindChain(grid, 0, _chain));
        }

        [Test]
        public void Relocate_EntryVacatesEvenWhenBoxedInByImmovableNeighbours()
        {
            var grid = NewGrid(3, 3);
            Place(grid, Movable(PressureResponse.RelocateNearest), 0, 2);
            Place(grid, Block(), 1, 2);
            Place(grid, Block(), 0, 1); // entry is boxed in by immovable neighbours
            Place(grid, Movable(), 2, 1); // a free slot still exists elsewhere

            Assert.IsTrue(PressureCascade.TryFindChain(grid, 0, _chain));
            Assert.AreEqual(new Vector2Int(0, 2), _chain[0]);
            Assert.IsTrue(grid.IsEmpty(_chain[^1].x, _chain[^1].y));
        }

        [Test]
        public void Relocate_ReachedThroughTheChain_EndsItByVacating()
        {
            var grid = NewGrid(3, 3);
            Place(grid, Movable(PressureResponse.ShoveNeighbour), 0, 2); // entry
            Place(grid, Movable(PressureResponse.RelocateFarthest), 1, 2); // its only movable neighbour
            Place(grid, Block(), 0, 1); // force the chain through the relocator
            Place(grid, Movable(), 2, 1); // leaves free slots for the relocator to jump to

            Assert.IsTrue(PressureCascade.TryFindChain(grid, 0, _chain));
            Assert.AreEqual(new Vector2Int(0, 2), _chain[0]);
            Assert.AreEqual(new Vector2Int(1, 2), _chain[1]);
            Assert.IsTrue(grid.IsEmpty(_chain[^1].x, _chain[^1].y));
        }

        [Test]
        public void Shove_RoutesThroughAPassThroughObstacleToTheGapBeyond()
        {
            var grid = NewGrid(3, 3);
            Place(grid, Movable(), 0, 2);      // entry
            Place(grid, PassThrough(), 1, 2);  // a puff sits between the entry and the gap
            Place(grid, Block(), 0, 1);        // the only other in-bounds direction is blocked
            // (2,2) stays empty — reachable only by passing through (1,2)

            Assert.IsTrue(PressureCascade.TryFindChain(grid, 0, _chain));
            Assert.AreEqual(new Vector2Int(0, 2), _chain[0]);
            Assert.AreEqual(new Vector2Int(2, 2), _chain[^1]); // landed past the puff, not on it
        }

        [Test]
        public void Shove_IsHaltedByANonTraversableObstacle()
        {
            var grid = NewGrid(3, 3);
            Place(grid, Movable(), 0, 2);
            Place(grid, Block(), 1, 2); // a solid blocker, not a puff — the ray cannot pass it
            Place(grid, Block(), 0, 1);
            // (2,2) empty but unreachable: every direction from the entry is blocked or out of bounds

            Assert.IsFalse(PressureCascade.TryFindChain(grid, 0, _chain));
        }

        [Test]
        public void RelocateNearest_PicksTheClosestGap()
        {
            var grid = NewGrid(3, 3);
            Place(grid, Movable(PressureResponse.RelocateNearest), 0, 2);
            // Leave only two gaps: (1,2) is adjacent, (2,0) is the far corner.
            FillExcept(grid, new Vector2Int(0, 2), new Vector2Int(1, 2), new Vector2Int(2, 0));

            Assert.IsTrue(PressureCascade.TryFindChain(grid, 0, _chain));
            Assert.AreEqual(new Vector2Int(1, 2), _chain[^1]);
        }

        [Test]
        public void RelocateFarthest_PicksTheMostDistantGap()
        {
            var grid = NewGrid(3, 3);
            Place(grid, Movable(PressureResponse.RelocateFarthest), 0, 2);
            FillExcept(grid, new Vector2Int(0, 2), new Vector2Int(1, 2), new Vector2Int(2, 0));

            Assert.IsTrue(PressureCascade.TryFindChain(grid, 0, _chain));
            Assert.AreEqual(new Vector2Int(2, 0), _chain[^1]);
        }

        [Test]
        public void BalloonModel_ShovesANeighbour()
        {
            IPressureMovable model = new BalloonModel();

            Assert.AreEqual(PressureResponse.ShoveNeighbour, model.PushResponse);
        }

        [Test]
        public void UnbreakableBalloonModel_RelocatesFarthest()
        {
            IPressureMovable model = new UnbreakableBalloonModel(new BalloonModelConfig());

            Assert.AreEqual(PressureResponse.RelocateFarthest, model.PushResponse);
        }

        [Test]
        public void BubbleClusterModel_RelocatesNearest()
        {
            IPressureMovable model = new BubbleClusterModel(new BalloonModelConfig(), Substitute.For<IGamePalette>());

            Assert.AreEqual(PressureResponse.RelocateNearest, model.PushResponse);
        }

        private static SlotGrid NewGrid(int columns, int rows)
        {
            var config = Substitute.For<IGameConfiguration>();
            config.SlotsSize.Returns(new Vector2Int(columns, rows));
            return new SlotGrid(config, new BalancePathHolder());
        }

        private static void FillColumn(SlotGrid grid, int col)
        {
            for (var row = 0; row < grid.Rows; row++)
            {
                Place(grid, Movable(), col, row);
            }
        }

        // Blocks every cell except the listed ones (which are left as-is — already occupied or kept empty).
        private static void FillExcept(SlotGrid grid, params Vector2Int[] skip)
        {
            var skipSet = new HashSet<Vector2Int>(skip);

            for (var col = 0; col < grid.Columns; col++)
            {
                for (var row = 0; row < grid.Rows; row++)
                {
                    var cell = new Vector2Int(col, row);
                    if (!skipSet.Contains(cell))
                    {
                        Place(grid, Block(), col, row);
                    }
                }
            }
        }

        private static IWriteableSlotActor Movable(PressureResponse response = PressureResponse.ShoveNeighbour)
        {
            var actor = Substitute.For<IWriteableSlotActor, IPressureMovable>();
            ((IPressureMovable)actor).PushResponse.Returns(response);
            return actor;
        }

        private static IWriteableSlotActor Block()
        {
            return Substitute.For<IWriteableSlotActor>();
        }

        // A pass-through occupant (e.g. a puff cloud) — occupied, but a shove can ray straight through it.
        private static IWriteableSlotActor PassThrough()
        {
            return Substitute.For<IWriteableSlotActor, IPassThrough>();
        }

        private static void Place(SlotGrid grid, IWriteableSlotActor actor, int col, int row)
        {
            grid.Place(actor, null, new Vector2Int(col, row));
        }
    }
}
