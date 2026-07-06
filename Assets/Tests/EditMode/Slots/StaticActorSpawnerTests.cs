using System.Collections.Generic;
using System.Reflection;
using BalloonParty.Configuration;
using BalloonParty.Game.Level;
using BalloonParty.Shared;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Actor.Archetype;
using BalloonParty.Slots.Grid;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using BalloonParty.Configuration.GridActors;
using BalloonParty.Configuration.Level;

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
            var gridActorConfig = CreateGridActorConfig();
            var levelParams = CreateLevelParams(GridActorType.Puff, 3);

            var spawner = new StaticActorSpawner(_grid, gridActorConfig, levelParams);
            spawner.SpawnStaticActors();

            Assert.AreEqual(3, CountActorsInGrid());
        }

        [Test]
        public void Spawn_PlacedActors_AreAllStatic()
        {
            var gridActorConfig = CreateGridActorConfig();
            var levelParams = CreateLevelParams(GridActorType.Puff, 3);

            var spawner = new StaticActorSpawner(_grid, gridActorConfig, levelParams);
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
            var gridActorConfig = CreateGridActorConfig();
            var levelParams = CreateLevelParams(GridActorType.Puff, 5);

            var spawner = new StaticActorSpawner(_grid, gridActorConfig, levelParams);

            Assert.DoesNotThrow(() => spawner.SpawnStaticActors());
            Assert.AreEqual(2, CountActorsInGrid());
        }

        [Test]
        public void Spawn_TypeGatedOutOfLevel_PlacesNone()
        {
            var gridActorConfig = CreateGridActorConfig();
            var levelParams = Substitute.For<IActiveLevelParameters>();
            var current = Substitute.For<ILevelParameters>();
            levelParams.Current.Returns(current);
            current.TryGetGridActorCount(Arg.Any<GridActorType>(), out Arg.Any<int>()).Returns(false);

            var spawner = new StaticActorSpawner(_grid, gridActorConfig, levelParams);
            spawner.SpawnStaticActors();

            Assert.AreEqual(0, CountActorsInGrid());
        }

        private static IActiveLevelParameters CreateLevelParams(GridActorType gatedType, int count)
        {
            var levelParams = Substitute.For<IActiveLevelParameters>();
            var current = Substitute.For<ILevelParameters>();
            levelParams.Current.Returns(current);
            current
                .TryGetGridActorCount(gatedType, out Arg.Any<int>())
                .Returns(ci =>
                {
                    ci[1] = count;
                    return true;
                });
            return levelParams;
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

        private static IGridActorConfiguration CreateGridActorConfig()
        {
            var entry = new GridActorPrefabEntry();
            SetField(entry, "_actorType", GridActorType.Puff);

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
