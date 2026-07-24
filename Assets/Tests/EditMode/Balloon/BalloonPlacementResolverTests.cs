using BalloonParty.Balloon.Controller;
using BalloonParty.Balloon.Spawner;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pause;
using BalloonParty.Slots.Actor.Archetype;
using BalloonParty.Slots.Grid;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Balloon
{
    // Regression coverage for the tight-board under-spawn bug: a static actor (e.g. a bush) sitting
    // at a column's entry row used to swallow that column's whole line allotment with no compensation.
    // The fix is neighbour rehoming — Resolve(col, Rehome) reaches into the nearest open column when
    // the column's own entry is capped; Resolve(col, OwnColumn) still refuses, matching pop-spawn extras.
    [TestFixture]
    public class BalloonPlacementResolverTests
    {
        private SlotGrid _grid;
        private BalloonBalancer _balancer;
        private BalloonPlacementResolver _resolver;

        [SetUp]
        public void SetUp()
        {
            BuildGrid(columns: 3, rows: 3);
        }

        [Test]
        public void Resolve_OwnColumnReach_ColumnCappedAtEntryByBush_ReturnsNull()
        {
            // Bush at the bottom (entry) row: no row exists below it to rise into.
            PlaceBushAt(col: 1, row: _grid.Rows - 1);

            var result = _resolver.Resolve(1, PlacementReach.OwnColumn);

            Assert.IsNull(result);
        }

        [Test]
        public void Resolve_RehomeReach_ColumnCappedAtEntryByBush_RehomesToNeighborColumn()
        {
            // The bug: column 1's whole allotment used to vanish here. The fix rehomes into
            // column 0 (open, and checked before column 2 at the same distance).
            PlaceBushAt(col: 1, row: _grid.Rows - 1);

            var result = _resolver.Resolve(1, PlacementReach.Rehome);

            Assert.AreEqual(new Vector2Int(0, 0), result);
        }

        [Test]
        public void Resolve_OwnColumnReach_AllColumnsCappedAtEntry_ReturnsNull()
        {
            // No column can compensate for another — initial fill takes only what is genuinely
            // reachable rather than forcing an overflow.
            PlaceBushAt(col: 0, row: _grid.Rows - 1);
            PlaceBushAt(col: 1, row: _grid.Rows - 1);
            PlaceBushAt(col: 2, row: _grid.Rows - 1);

            var result = _resolver.Resolve(1, PlacementReach.Rehome);

            Assert.IsNull(result);
        }

        [Test]
        public void Resolve_RehomeReach_OwnColumnOpen_DoesNotRehome()
        {
            // Own entry is available — rehoming must not kick in when it isn't needed.
            var result = _resolver.Resolve(1, PlacementReach.Rehome);

            Assert.AreEqual(new Vector2Int(1, 0), result);
        }

        [Test]
        public void Resolve_RehomeReach_NearerColumnPreferredOverFarther()
        {
            BuildGrid(columns: 5, rows: 3);
            // From column 2: column 1 (distance 1) is open, column 0 (distance 2) is also open —
            // the nearer one must win.
            PlaceBushAt(col: 2, row: _grid.Rows - 1);

            var result = _resolver.Resolve(2, PlacementReach.Rehome);

            Assert.AreEqual(new Vector2Int(1, 0), result);
        }

        [Test]
        public void ReachableCapacity_UnblockedColumn_CountsAllEmptyRows()
        {
            Assert.AreEqual(_grid.Rows, _resolver.ReachableCapacity(0));
        }

        [Test]
        public void ReachableCapacity_ColumnCappedAtEntry_ReturnsZero()
        {
            PlaceBushAt(col: 1, row: _grid.Rows - 1);

            Assert.AreEqual(0, _resolver.ReachableCapacity(1));
        }

        [Test]
        public void ReachableCapacity_BlockerMidColumn_CountsOnlyRowsBelowIt()
        {
            BuildGrid(columns: 3, rows: 4);
            // Bush at row 2 (of 0..3): only row 3 is below it and reachable from the entry;
            // rows 0 and 1 sit above the blocker and are not.
            PlaceBushAt(col: 1, row: 2);

            Assert.AreEqual(1, _resolver.ReachableCapacity(1));
        }

        // Rebuilds the grid, balancer, and resolver together — the resolver is bound to a specific
        // grid instance, so tests using a non-default size must go through this rather than reassigning
        // the grid alone.
        private void BuildGrid(int columns, int rows)
        {
            var config = Substitute.For<ISlotGridConfig>();
            config.SlotsSize.Returns(new Vector2Int(columns, rows));
            _grid = new SlotGrid(config, new BalancePathHolder());

            var balanceQuery = new GridBalanceQuery(_grid);
            var balloonsConfig = Substitute.For<IBalloonsConfiguration>();
            var pauseService = new PauseService(
                Substitute.For<IPublisher<PausedMessage>>(), Substitute.For<IPublisher<ResumedMessage>>());

            // Never exercised by OwnColumn/Rehome — only Pressure reach dereferences the balancer.
            _balancer = new BalloonBalancer(
                _grid, balanceQuery, balloonsConfig, new BalancePathHolder(),
                Substitute.For<ISubscriber<BalanceBalloonsMessage>>(),
                Substitute.For<ISubscriber<ProjectileLoadedMessage>>(),
                Substitute.For<ISubscriber<ProjectileDestroyedMessage>>(),
                pauseService, null, new BalloonMotionTicker());

            _resolver = new BalloonPlacementResolver(_grid, _balancer);
        }

        private void PlaceBushAt(int col, int row)
        {
            _grid.Place(new BushObstacleModel(), null, new Vector2Int(col, row));
        }
    }
}
