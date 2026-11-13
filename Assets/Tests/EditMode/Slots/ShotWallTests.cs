using BalloonParty.Balloon.Model;
using BalloonParty.Shared;
using BalloonParty.Slots.Actor.Archetype;
using BalloonParty.Slots.Grid;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Slots
{
    [TestFixture]
    public class ShotWallTests
    {
        private SlotGrid _grid;
        private ShotWall _wall;

        [SetUp]
        public void SetUp()
        {
            var config = Substitute.For<ISlotGridConfig>();
            config.SlotsSize.Returns(new Vector2Int(7, 8));
            _grid = new SlotGrid(config, new BalancePathHolder());
            _wall = new ShotWall();
        }

        [Test]
        public void AFullRowOfUnbreakables_Spans()
        {
            PlaceUnbreakables(2, 0, 6);

            Assert.IsTrue(LoadAndSpan());
        }

        [Test]
        public void ARowWithOneGap_DoesNotSpan()
        {
            PlaceUnbreakables(2, 0, 2);
            PlaceUnbreakables(2, 4, 6);

            Assert.IsFalse(LoadAndSpan());
        }

        [Test]
        public void TwoRowsTouchingEndToEnd_Span()
        {
            // Row 2 is even, so its slot 3 touches slots 2 and 3 of row 3 below.
            PlaceUnbreakables(2, 0, 3);
            PlaceUnbreakables(3, 3, 6);

            Assert.IsTrue(LoadAndSpan());
        }

        [Test]
        public void ToughBalloons_GiveWayInTime_SoNeverWall()
        {
            PlaceUnbreakables(2, 0, 5);
            _grid.Place(new ToughBalloonModel(new BalloonModelConfig()), null, new Vector2Int(6, 2));

            Assert.IsFalse(LoadAndSpan());
        }

        [Test]
        public void ADeflector_WallsLikeAnUnbreakable()
        {
            PlaceUnbreakables(2, 0, 5);
            _grid.Place(new DeflectorActorModel(), null, new Vector2Int(6, 2));

            Assert.IsTrue(LoadAndSpan());
        }

        [Test]
        public void Set_TriesAMoveWithoutTouchingTheGrid()
        {
            PlaceUnbreakables(2, 0, 5);
            _wall.Load(_grid);

            _wall.Set(new Vector2Int(6, 2), true);
            Assert.IsTrue(_wall.Spans());

            _wall.Set(new Vector2Int(6, 2), false);
            Assert.IsFalse(_wall.Spans());
            Assert.IsTrue(_grid.IsEmpty(6, 2));
        }

        private bool LoadAndSpan()
        {
            _wall.Load(_grid);
            return _wall.Spans();
        }

        private void PlaceUnbreakables(int row, int fromColumn, int toColumn)
        {
            for (var col = fromColumn; col <= toColumn; col++)
            {
                _grid.Place(new UnbreakableBalloonModel(new BalloonModelConfig()), null, new Vector2Int(col, row));
            }
        }
    }
}
