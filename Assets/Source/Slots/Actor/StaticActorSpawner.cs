using System.Collections.Generic;
using System.Threading;
using BalloonParty.Configuration;
using BalloonParty.Shared;
using BalloonParty.Shared.Extensions;
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
        private readonly SlotGrid _grid;
        private readonly IGameConfiguration _config;
        private readonly PoolManager _poolManager;
        private readonly IObjectResolver _resolver;
        private readonly GridActorConfiguration _gridActorConfig;
        private readonly Dictionary<string, int> _activeCounts = new();
        private bool _poolsRegistered;

        public SpawnStage SpawnPriority => SpawnStage.StaticActors;

        [Inject]
        internal StaticActorSpawner(
            SlotGrid grid,
            IGameConfiguration config,
            PoolManager poolManager,
            IObjectResolver resolver,
            GridActorConfiguration gridActorConfig)
        {
            _grid = grid;
            _config = config;
            _poolManager = poolManager;
            _resolver = resolver;
            _gridActorConfig = gridActorConfig;
        }

        // Bypasses pool and MonoBehaviour infrastructure — used in tests.
        internal StaticActorSpawner(SlotGrid grid, IGameConfiguration config, GridActorConfiguration gridActorConfig)
        {
            _grid = grid;
            _config = config;
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
            var totalCount = Mathf.Min(
                Random.Range(_config.MinStaticActors, _config.MaxStaticActors + 1),
                emptySlots.Count);

            var remaining = totalCount;

            while (remaining > 0 && emptySlots.Count > 0)
            {
                var entry = _gridActorConfig.Entries.PickRandom(_activeCounts);
                if (entry == null)
                {
                    break;
                }

                var strategy = GetStrategy(entry.PlacementMode);
                var selected = strategy.SelectSlots(emptySlots, remaining);

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
                    remaining--;
                }

                _activeCounts[entry.PoolKey] = _activeCounts.GetValueOrDefault(entry.PoolKey) + selected.Count;
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

        private static readonly Dictionary<SlotPlacementMode, ISlotSelectionStrategy> StrategyCache = new();

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
