using System.Collections.Generic;
using System.Threading;
using BalloonParty.Configuration;
using BalloonParty.Shared;
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
        internal StaticActorSpawner(SlotGrid grid, IGameConfiguration config)
        {
            _grid = grid;
            _config = config;
        }

        public void Start()
        {
            RegisterPools();
        }

        public UniTask SpawnAsync(CancellationToken ct)
        {
            //SpawnStaticActors();
            return UniTask.CompletedTask;
        }

        internal void SpawnStaticActors()
        {
            var slots = new List<Vector2Int>(_grid.AllEmptySlots());
            var count = Mathf.Min(
                Random.Range(_config.MinStaticActors, _config.MaxStaticActors + 1),
                slots.Count);

            Shuffle(slots);

            for (var i = 0; i < count; i++)
            {
                var entry = _gridActorConfig.PickRandom(_activeCounts);
                if (entry == null)
                {
                    break;
                }

                var model = CreateModel(entry.ActorType);
                GridActorView view = null;

                if (_poolsRegistered)
                {
                    view = _poolManager.Get<GridActorView>(entry.PoolKey);
                    view.transform.position = _grid.IndexToWorldPosition(slots[i]);
                }

                _grid.Place(model, view, slots[i]);
                _activeCounts[entry.PoolKey] = _activeCounts.GetValueOrDefault(entry.PoolKey) + 1;
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

        private static IWriteableSlotActor CreateModel(GridActorType actorType)
        {
            return actorType switch
            {
                GridActorType.Puff => new PuffObstacleModel(),
                _ => throw new System.Exception("Unknown actor type: " + actorType)
            };
        }

        private static void Shuffle<T>(List<T> list)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
