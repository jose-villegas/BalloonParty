using System.Collections.Generic;
using System.Threading;
using BalloonParty.Configuration;
using BalloonParty.Shared.Pool;
using BalloonParty.Slots.Actor.Archetype;
using BalloonParty.Slots.Grid;
using BalloonParty.Slots.Spawner;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Slots.Actor
{
    internal class StaticActorSpawner : IStartable, IGridSpawner
    {
        private static readonly Dictionary<SlotPlacementMode, ISlotSelectionStrategy> StrategyCache = new();

        private readonly SlotGrid _grid;
        private readonly PoolManager _poolManager;
        private readonly IObjectResolver _resolver;
        private readonly GridActorConfiguration _gridActorConfig;
        private bool _poolsRegistered;

        public SpawnStage SpawnPriority => SpawnStage.StaticActors;

        [Inject]
        internal StaticActorSpawner(
            SlotGrid grid,
            PoolManager poolManager,
            IObjectResolver resolver,
            GridActorConfiguration gridActorConfig)
        {
            _grid = grid;
            _poolManager = poolManager;
            _resolver = resolver;
            _gridActorConfig = gridActorConfig;
        }

        // Bypasses pool and MonoBehaviour infrastructure — used in tests.
        internal StaticActorSpawner(SlotGrid grid, GridActorConfiguration gridActorConfig)
        {
            _grid = grid;
            _gridActorConfig = gridActorConfig;
        }

        public void Start()
        {
            RegisterPools();
        }

        public UniTask SpawnAsync(CancellationToken ct)
        {
            SpawnStaticActors();
            return UniTask.CompletedTask;
        }

        internal void SpawnStaticActors()
        {
            var emptySlots = new List<Vector2Int>(_grid.AllEmptySlots());

            foreach (var entry in _gridActorConfig.Entries)
            {
                if (emptySlots.Count == 0)
                {
                    break;
                }

                var max = entry.MaxCount > 0 ? entry.MaxCount : emptySlots.Count;
                var count = Mathf.Min(
                    Random.Range(entry.MinCount, max + 1),
                    emptySlots.Count);

                if (count <= 0)
                {
                    continue;
                }

                var strategy = GetStrategy(entry.PlacementMode);
                var selected = strategy.SelectSlots(emptySlots, count, entry.MaxPerCluster);

                foreach (var slot in selected)
                {
                    var model = CreateModel(entry.ActorType);
                    GridActorView view = null;

                    if (_poolsRegistered)
                    {
                        view = _poolManager.Get<GridActorView>(entry.PoolKey);
                        view.transform.position = _grid.IndexToWorldPosition(slot);
                    }

                    _grid.Place(model, view, slot);
                    emptySlots.Remove(slot);
                }
            }
        }

        private void RegisterPools()
        {
            if (_poolManager == null || _gridActorConfig == null)
            {
                return;
            }

            foreach (var entry in _gridActorConfig.Entries)
            {
                if (entry.Prefab == null)
                {
                    continue;
                }

                _poolManager.Register(entry.PoolKey, new GridActorPoolChannel(_resolver, entry.Prefab));
            }

            _poolsRegistered = true;
        }

        private static ISlotSelectionStrategy GetStrategy(SlotPlacementMode mode)
        {
            if (StrategyCache.TryGetValue(mode, out var cached))
            {
                return cached;
            }

            ISlotSelectionStrategy strategy = mode switch
            {
                SlotPlacementMode.Cluster => new ClusterSlotSelectionStrategy(),
                _ => new RandomSlotSelectionStrategy()
            };

            StrategyCache[mode] = strategy;
            return strategy;
        }

        private static IWriteableSlotActor CreateModel(GridActorType actorType)
        {
            return actorType switch
            {
                GridActorType.Puff => new PuffObstacleModel(),
                _ => throw new System.Exception("Unknown actor type: " + actorType)
            };
        }
    }
}
