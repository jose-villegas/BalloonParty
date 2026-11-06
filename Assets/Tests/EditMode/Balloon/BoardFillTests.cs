using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.Spawner;
using BalloonParty.Configuration.Level;
using BalloonParty.Game.Level;
using BalloonParty.Shared;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Grid;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Balloon
{
    [TestFixture]
    public class BoardFillTests
    {
        private SlotGrid _grid;
        private BoardFill _fill;

        // A 4x6 grid whose level starts with 2 board lines: a starting fill of 8 balloons.
        [SetUp]
        public void SetUp()
        {
            var gridConfig = Substitute.For<ISlotGridConfig>();
            gridConfig.SlotsSize.Returns(new Vector2Int(4, 6));
            _grid = new SlotGrid(gridConfig, new BalancePathHolder());

            var level = Substitute.For<ILevelParameters>();
            level.BoardLines.Returns(2);
            var levelParams = Substitute.For<IActiveLevelParameters>();
            levelParams.Current.Returns(level);

            _fill = new BoardFill(_grid, levelParams);
        }

        [Test]
        public void Fill_EmptyBoard_IsZero()
        {
            Assert.AreEqual(0f, _fill.StartingFill, 0.0001f);
        }

        [Test]
        public void Fill_CountsBalloonsAgainstTheStartingFill()
        {
            PlaceBalloons(4);

            Assert.AreEqual(0.5f, _fill.StartingFill, 0.0001f);
        }

        [Test]
        public void Fill_MoreThanTheStartingFill_ClampsToOne()
        {
            PlaceBalloons(12);

            Assert.AreEqual(1f, _fill.StartingFill, 0.0001f);
        }

        [Test]
        public void Fill_IgnoresGridActors()
        {
            _grid.Place(new StaticActorModel(), null, new Vector2Int(0, 0));
            _grid.Place(new StaticActorModel(), null, new Vector2Int(1, 0));

            Assert.AreEqual(0f, _fill.StartingFill, 0.0001f, "grid actors aren't targets");
        }

        [Test]
        public void Fill_RecountsAfterTheGridChanges()
        {
            PlaceBalloons(4);
            Assert.AreEqual(0.5f, _fill.StartingFill, 0.0001f);

            _grid.Remove(new Vector2Int(0, 0));
            _grid.Remove(new Vector2Int(1, 0));

            Assert.AreEqual(0.25f, _fill.StartingFill, 0.0001f);
        }

        [Test]
        public void Occupancy_IsBalloonsOverTheWholeBoard()
        {
            PlaceBalloons(6);

            Assert.AreEqual(0.25f, _fill.Occupancy, 0.0001f, "6 of 24 slots");
        }

        [Test]
        public void Occupancy_LeavesGridActorSlotsOutOfTheBoard()
        {
            _grid.Place(new StaticActorModel(), null, new Vector2Int(0, 5));
            _grid.Place(new StaticActorModel(), null, new Vector2Int(1, 5));
            _grid.Place(new StaticActorModel(), null, new Vector2Int(2, 5));
            _grid.Place(new StaticActorModel(), null, new Vector2Int(3, 5));
            PlaceBalloons(5);

            Assert.AreEqual(0.25f, _fill.Occupancy, 0.0001f, "5 of the 20 slots actors leave open");
        }

        [Test]
        public void Occupancy_RecountsAfterTheGridChanges()
        {
            PlaceBalloons(12);
            Assert.AreEqual(0.5f, _fill.Occupancy, 0.0001f);

            _grid.Remove(new Vector2Int(0, 0));

            Assert.AreEqual(11f / 24f, _fill.Occupancy, 0.0001f);
        }

        private void PlaceBalloons(int count)
        {
            for (var i = 0; i < count; i++)
            {
                _grid.Place(new BalloonModel(), null, new Vector2Int(i % 4, i / 4));
            }
        }
    }
}
