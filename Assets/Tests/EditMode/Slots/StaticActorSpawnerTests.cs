using BalloonParty.Shared;
using BalloonParty.Slots;
using BalloonParty.Slots.Grid;
using BalloonParty.Slots.StaticActor;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Slots
{
    [TestFixture]
    public class StaticActorSpawnerTests
    {
        private IGameConfiguration _config;
        private SlotGrid _grid;

        [SetUp]
        public void SetUp()
        {
            _config = Substitute.For<IGameConfiguration>();
            _config.SlotsSize.Returns(new Vector2Int(6, 10));
            _config.SlotSeparation.Returns(new Vector2(1f, 0.85f));
            _config.SlotsOffset.Returns(new Vector2(2.5f, 4f));

            _grid = new SlotGrid(_config);
        }

        [Test]
        public void Spawn_PlacesExactCount_WhenGridHasEnoughEmptySlots()
        {
            _config.MinStaticActors.Returns(3);
            _config.MaxStaticActors.Returns(3);

            var spawner = new StaticActorSpawner(_grid, _config, () => null);
            spawner.Start();
            spawner.SpawnAsync(default).GetAwaiter().GetResult();

            Assert.AreEqual(3, CountActorsInGrid());
        }

        [Test]
        public void Spawn_PlacedActors_AreAllStatic()
        {
            _config.MinStaticActors.Returns(3);
            _config.MaxStaticActors.Returns(3);

            var spawner = new StaticActorSpawner(_grid, _config, () => null);
            spawner.Start();
            spawner.SpawnAsync(default).GetAwaiter().GetResult();

            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    if (!_grid.IsEmpty(col, row))
                    {
                        Assert.AreEqual(SlotActorKind.Static, _grid.At(new Vector2Int(col, row)).Kind);
                    }
                }
            }
        }

        [Test]
        public void Spawn_DoesNotExceedAvailableSlots()
        {
            // 2 columns × 1 row = 2 total slots; requesting 5 should cap at 2.
            _config.SlotsSize.Returns(new Vector2Int(2, 1));
            _grid = new SlotGrid(_config);

            _config.MinStaticActors.Returns(5);
            _config.MaxStaticActors.Returns(5);

            var spawner = new StaticActorSpawner(_grid, _config, () => null);

            Assert.DoesNotThrow(() =>
            {
                spawner.Start();
                spawner.SpawnAsync(default).GetAwaiter().GetResult();
            });
            Assert.AreEqual(2, CountActorsInGrid());
        }

        private int CountActorsInGrid()
        {
            var count = 0;

            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    if (!_grid.IsEmpty(col, row))
                    {
                        count++;
                    }
                }
            }

            return count;
        }
    }
}

