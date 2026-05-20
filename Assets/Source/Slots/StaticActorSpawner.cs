using System;
using System.Collections.Generic;
using BalloonParty.Shared;
using BalloonParty.Shared.Pool;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Slots
{
    internal class StaticActorSpawner : IStartable
    {
        internal const string PoolKey = "StaticActor";

        private readonly SlotGrid _grid;
        private readonly IGameConfiguration _config;
        private readonly PoolManager _poolManager;
        private readonly IObjectResolver _resolver;
        private readonly StaticActorSettings _settings;
        private Func<StaticActorView> _viewFactory;

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

        // For tests — avoids pool and MonoBehaviour overhead.
        internal StaticActorSpawner(SlotGrid grid, IGameConfiguration config, Func<StaticActorView> viewFactory)
        {
            _grid = grid;
            _config = config;
            _viewFactory = viewFactory;
        }

        // Coordination contract: Start() must remain synchronous and must not yield.
        // BalloonSpawner.PrewarmAndPopulateAsync awaits NavigationState.Game before filling
        // the grid — that async gap guarantees statics are already placed when balloons land.
        // If this ever needs to become async, introduce a GridSpawnerCoordinator (see Phase 8).
        public void Start()
        {
            if (_viewFactory == null)
            {
                _poolManager.Register(PoolKey, new StaticActorPoolChannel(_resolver, _settings.Prefab));
                _viewFactory = () => _poolManager.Get<StaticActorView>(PoolKey);
            }

            SpawnStaticActors();
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
                var model = new StaticActorModel();
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

