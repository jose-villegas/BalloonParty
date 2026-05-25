using System;
using System.Collections.Generic;
using System.Threading;
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
        internal const string PoolKey = "StaticActor";

        private readonly SlotGrid _grid;
        private readonly IGameConfiguration _config;
        private readonly PoolManager _poolManager;
        private readonly IObjectResolver _resolver;
        private readonly StaticActorSettings _settings;
        private Func<StaticActorView> _viewFactory;

        public SpawnStage SpawnPriority => SpawnStage.StaticActors;

        [Inject]
        internal StaticActorSpawner(
            SlotGrid grid,
            IGameConfiguration config,
            PoolManager poolManager,
            IObjectResolver resolver,
            StaticActorSettings settings)
        {
            _grid = grid;
            _config = config;
            _poolManager = poolManager;
            _resolver = resolver;
            _settings = settings;
        }

        // Bypasses pool and MonoBehaviour infrastructure — used in tests.
        internal StaticActorSpawner(SlotGrid grid, IGameConfiguration config, Func<StaticActorView> viewFactory)
        {
            _grid = grid;
            _config = config;
            _viewFactory = viewFactory;
        }

        public void Start()
        {
            if (_viewFactory == null)
            {
                _poolManager.Register(PoolKey, new StaticActorPoolChannel(_resolver, _settings.Prefab));
                _viewFactory = () => _poolManager.Get<StaticActorView>(PoolKey);
            }
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
                UnityEngine.Random.Range(_config.MinStaticActors, _config.MaxStaticActors + 1),
                slots.Count);

            Shuffle(slots);

            for (var i = 0; i < count; i++)
            {
                var model = new PuffObstacleModel();
                var view = _viewFactory();

                if (view != null)
                {
                    view.transform.position = _grid.IndexToWorldPosition(slots[i]);
                }

                _grid.Place(model, view, slots[i]);
            }
        }

        private static void Shuffle<T>(List<T> list)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
