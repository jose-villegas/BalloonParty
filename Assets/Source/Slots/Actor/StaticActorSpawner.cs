using System.Collections.Generic;
using System.Threading;
using BalloonParty.Configuration;
using BalloonParty.Game.Level;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pool;
using BalloonParty.Slots.Actor.Archetype;
using BalloonParty.Slots.Grid;
using BalloonParty.Slots.Spawner;
using Cysharp.Threading.Tasks;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Slots.Actor
{
    internal class StaticActorSpawner : IStartable, IGridSpawner
    {
        private readonly Dictionary<SlotPlacementMode, ISlotSelectionStrategy> _strategyCache = new();

        // One registration line per actor type — the model self-reports its GridActorType, so adding a
        // type means adding the model + an entry here, never editing a switch.
        private readonly Dictionary<GridActorType, System.Func<IWriteableSlotActor>> _modelFactories = new()
        {
            { GridActorType.Puff, () => new PuffObstacleModel() },
            { GridActorType.Bush, () => new BushObstacleModel() }
        };

        private readonly ISubscriber<BoardClearMessage> _boardClearSubscriber;
        private readonly SlotGrid _grid;
        private readonly PoolManager _poolManager;
        private readonly IObjectResolver _resolver;
        private readonly IGridActorConfiguration _gridActorConfig;
        private readonly IActiveLevelParameters _levelParams;
        private readonly ScenarioContentRoot _scenarioRoot;
        private bool _poolsRegistered;

        public SpawnStage SpawnPriority => SpawnStage.StaticActors;

        [Inject]
        internal StaticActorSpawner(
            SlotGrid grid,
            PoolManager poolManager,
            IObjectResolver resolver,
            IGridActorConfiguration gridActorConfig,
            IActiveLevelParameters levelParams,
            ISubscriber<BoardClearMessage> boardClearSubscriber,
            ScenarioContentRoot scenarioRoot)
        {
            _grid = grid;
            _poolManager = poolManager;
            _resolver = resolver;
            _gridActorConfig = gridActorConfig;
            _levelParams = levelParams;
            _boardClearSubscriber = boardClearSubscriber;
            _scenarioRoot = scenarioRoot;
        }

        // Bypasses pool and MonoBehaviour infrastructure — used in tests.
        internal StaticActorSpawner(SlotGrid grid, IGridActorConfiguration gridActorConfig, IActiveLevelParameters levelParams)
        {
            _grid = grid;
            _gridActorConfig = gridActorConfig;
            _levelParams = levelParams;
        }

        public void Start()
        {
            RegisterPools();
            _boardClearSubscriber?.Subscribe(OnBoardClear);
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

                if (!_levelParams.TryGetGridActorCount(entry.ActorType, out var levelCount))
                {
                    // Absent from this level's range — the type gate (mirrors the balloon gate).
                    continue;
                }

                var count = Mathf.Min(levelCount, emptySlots.Count);
                if (count <= 0)
                {
                    continue;
                }

                var strategy = GetStrategy(entry.PlacementMode);
                var selected = strategy.SelectSlots(emptySlots, count, entry.MaxPerCluster);

                foreach (var slot in selected)
                {
                    PlaceActor(entry, slot, emptySlots);
                }
            }
        }

        private void PlaceActor(GridActorPrefabEntry entry, Vector2Int slot, List<Vector2Int> emptySlots)
        {
            var model = CreateModel(entry.ActorType);
            GridActorView view = null;

            if (_poolsRegistered)
            {
                view = _poolManager.Get<GridActorView>(entry.PoolKey);

                // Parent under the scenario root (at the origin during a normal spawn) so the
                // level-transition Ascent can lift and slide the whole scenario as one unit.
                view.transform.SetParent(_scenarioRoot?.Transform, false);
                view.transform.position = _grid.IndexToWorldPosition(slot);
            }

            _grid.Place(model, view, slot);

            var idx = emptySlots.IndexOf(slot);
            if (idx >= 0)
            {
                emptySlots.SwapRemoveAt(idx);
            }
        }

        private void OnBoardClear(BoardClearMessage _)
        {
            if (!_poolsRegistered)
            {
                return;
            }

            // StaticActorSpawner owns the Get(), so it owns the Return(). Removing each slot
            // fires SlotGrid.OnChanged(Removed), which the cluster registries consume to dissolve
            // their clusters incrementally — no explicit rebuild needed for the clear.
            foreach (var entry in _gridActorConfig.Entries)
            {
                ReturnActorsForEntry(entry);
            }
        }

        private void ReturnActorsForEntry(GridActorPrefabEntry entry)
        {
            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    ReturnActorAt(new Vector2Int(col, row), entry);
                }
            }
        }

        private void ReturnActorAt(Vector2Int slot, GridActorPrefabEntry entry)
        {
            var model = _grid.At(slot);
            if (model == null || !ModelMatches(model, entry.ActorType))
            {
                return;
            }

            var view = _grid.ViewAt(slot) as GridActorView;
            _grid.Remove(slot);
            if (view != null)
            {
                _poolManager.Return(entry.PoolKey, view);
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

        private ISlotSelectionStrategy GetStrategy(SlotPlacementMode mode)
        {
            if (_strategyCache.TryGetValue(mode, out var cached))
            {
                return cached;
            }

            ISlotSelectionStrategy strategy = mode switch
            {
                SlotPlacementMode.Cluster => new ClusterSlotSelectionStrategy(),
                _ => new RandomSlotSelectionStrategy()
            };

            _strategyCache[mode] = strategy;
            return strategy;
        }

        private IWriteableSlotActor CreateModel(GridActorType actorType)
        {
            if (_modelFactories.TryGetValue(actorType, out var factory))
            {
                return factory();
            }

            throw new System.Exception("Unknown actor type: " + actorType);
        }

        private static bool ModelMatches(IWriteableSlotActor model, GridActorType actorType)
        {
            return model is IGridActorModel actor && actor.ActorType == actorType;
        }
    }
}
