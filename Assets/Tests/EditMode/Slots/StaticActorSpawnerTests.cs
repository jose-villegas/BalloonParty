using System.Collections.Generic;
using System.Reflection;
using BalloonParty.Configuration;
using BalloonParty.Shared;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Actor.Archetype;
using BalloonParty.Slots.Grid;
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

            _grid = new SlotGrid(_config, new BalancePathHolder());
        }

        [Test]
        public void Spawn_PlacesExactCount_WhenGridHasEnoughEmptySlots()
        {
            var gridActorConfig = CreateGridActorConfig(minCount: 3, maxCount: 3);

            var spawner = new StaticActorSpawner(_grid, gridActorConfig);
            spawner.SpawnStaticActors();

            Assert.AreEqual(3, CountActorsInGrid());
        }

        [Test]
        public void Spawn_PlacedActors_AreAllStatic()
        {
            var gridActorConfig = CreateGridActorConfig(minCount: 3, maxCount: 3);

            var spawner = new StaticActorSpawner(_grid, gridActorConfig);
            spawner.SpawnStaticActors();

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
            _config.SlotsSize.Returns(new Vector2Int(2, 1));
            _grid = new SlotGrid(_config, new BalancePathHolder());
            var gridActorConfig = CreateGridActorConfig(minCount: 5, maxCount: 5);

            var spawner = new StaticActorSpawner(_grid, gridActorConfig);

            Assert.DoesNotThrow(() => spawner.SpawnStaticActors());
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

        private static IGridActorConfiguration CreateGridActorConfig(int minCount, int maxCount)
        {
            var entry = new GridActorPrefabEntry();
            SetField(entry, "_actorType", GridActorType.Puff);
            SetField(entry, "_weight", 1f);
            SetField(entry, "_minCount", minCount);
            SetField(entry, "_maxCount", maxCount);

            var config = Substitute.For<IGridActorConfiguration>();
            config.Entries.Returns(new List<GridActorPrefabEntry> { entry });
            return config;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            target.GetType()
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(target, value);
        }
    }
}
