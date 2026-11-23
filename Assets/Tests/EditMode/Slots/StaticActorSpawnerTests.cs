using System.Collections.Generic;
using System.Reflection;
using BalloonParty.Configuration;
using BalloonParty.Game.Level;
using BalloonParty.Shared;
using BalloonParty.Shared.Pool;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Actor.Archetype;
using BalloonParty.Slots.Grid;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using VContainer;
using BalloonParty.Configuration.GridActors;
using BalloonParty.Configuration.Level;

namespace BalloonParty.Tests.Slots
{
    [TestFixture]
    public class StaticActorSpawnerTests
    {
        private ISlotGridConfig _config;
        private SlotGrid _grid;

        [SetUp]
        public void SetUp()
        {
            _config = Substitute.For<ISlotGridConfig>();
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

        // A saved run continued from the launch screen spawns its board as the mist reveal starts scaling the
        // root: the actors must still stand on their slots once the reveal has ended.
        [Test]
        public void Spawn_UnderAScaledRoot_StandsOnItsSlotsOnceTheRootIsAtRest()
        {
            var prefab = new GameObject("TestPuff").AddComponent<GridActorView>();
            prefab.gameObject.SetActive(false);
            var entry = new GridActorPrefabEntry();
            SetField(entry, "_actorType", GridActorType.Puff);
            SetField(entry, "_prefab", prefab);
            var gridActorConfig = Substitute.For<IGridActorConfiguration>();
            gridActorConfig.Entries.Returns(new List<GridActorPrefabEntry> { entry });
            var root = new ScenarioContentRoot();
            var pools = new PoolManager();
            var spawner = new StaticActorSpawner(
                _grid, pools, new ContainerBuilder().Build(), gridActorConfig,
                CreateLevelParams(GridActorType.Puff, 4), null, root, null);

            try
            {
                spawner.Start();
                root.SetDepth(2f / 3f, 1f);
                spawner.SpawnStaticActors();
                root.SetDepth(1f, 1f);

                var placed = 0;
                for (var col = 0; col < _grid.Columns; col++)
                {
                    for (var row = 0; row < _grid.Rows; row++)
                    {
                        var slot = new Vector2Int(col, row);
                        if (_grid.ViewAt(slot) is GridActorView view)
                        {
                            placed++;
                            Assert.That(
                                Vector3.Distance(view.transform.position, _grid.IndexToWorldPosition(slot)),
                                Is.LessThan(1e-4f), $"the actor at {slot} is off its slot");
                        }
                    }
                }

                Assert.AreEqual(4, placed);
            }
            finally
            {
                Object.DestroyImmediate(root.Transform.gameObject);
                Object.DestroyImmediate(prefab.gameObject);
                var poolRoot = GameObject.Find("[Pool]");
                if (poolRoot != null)
                {
                    Object.DestroyImmediate(poolRoot);
                }
            }
        }

        [Test]
        public void Spawn_TypeGatedOutOfLevel_PlacesNone()
        {
            var gridActorConfig = CreateGridActorConfig();
            var levelParams = Substitute.For<IActiveLevelParameters>();
            var current = Substitute.For<ILevelParameters>();
            levelParams.Current.Returns(current);
            current.TryGetGridActorGate(Arg.Any<GridActorType>(), out Arg.Any<ResolvedGridActorGate>()).Returns(false);

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
                .TryGetGridActorGate(gatedType, out Arg.Any<ResolvedGridActorGate>())
                .Returns(ci =>
                {
                    ci[1] = new ResolvedGridActorGate(gatedType, count, 0);
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
