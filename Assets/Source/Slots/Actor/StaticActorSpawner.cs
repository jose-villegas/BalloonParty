using System;
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
using BalloonParty.Configuration.GridActors;

namespace BalloonParty.Slots.Actor
{
    internal class StaticActorSpawner : IStartable, IGridSpawner, IDisposable
    {
        private readonly Dictionary<SlotPlacementMode, ISlotSelectionStrategy> _strategyCache = new();

        // Add a type by adding the model + an entry here, never a switch. The number handed in is the actor's
        // place in its entry's selection, which a mushroom keeps as its cap's id; its wall is asked of the
        // strategy that picked the slots, so that entry is added where the strategy is known (the constructors).
        private readonly Dictionary<GridActorType, System.Func<int, IWriteableSlotActor>> _modelFactories = new()
        {
            { GridActorType.Puff, _ => new PuffObstacleModel() },
            { GridActorType.Bush, _ => new BushObstacleModel() }
        };

        private readonly ISubscriber<BoardClearMessage> _boardClearSubscriber;
        private readonly SlotGrid _grid;
        private readonly PoolManager _poolManager;
        private readonly IObjectResolver _resolver;
        private readonly IGridActorConfiguration _gridActorConfig;
        private readonly IActiveLevelParameters _levelParams;
        private readonly ScenarioContentRoot _scenarioRoot;
        private readonly WallSlotSelectionStrategy _wallStrategy;
        private readonly List<Vector2Int> _clearOfCaps = new();
        private bool _poolsRegistered;
        private IDisposable _subscription;

        public SpawnStage SpawnPriority => SpawnStage.StaticActors;

        [Inject]
        internal StaticActorSpawner(
            SlotGrid grid,
            PoolManager poolManager,
            IObjectResolver resolver,
            IGridActorConfiguration gridActorConfig,
            IActiveLevelParameters levelParams,
            ISubscriber<BoardClearMessage> boardClearSubscriber,
            ScenarioContentRoot scenarioRoot,
            WallSlotSelectionStrategy wallStrategy)
        {
            _grid = grid;
            _poolManager = poolManager;
            _resolver = resolver;
            _gridActorConfig = gridActorConfig;
            _levelParams = levelParams;
            _boardClearSubscriber = boardClearSubscriber;
            _scenarioRoot = scenarioRoot;
            _wallStrategy = wallStrategy;
            AddMushroomFactory();
        }

        // Test-only: bypasses pool and MonoBehaviour infrastructure. With no wall strategy a Mushroom entry
        // places nothing.
        internal StaticActorSpawner(
            SlotGrid grid, IGridActorConfiguration gridActorConfig, IActiveLevelParameters levelParams,
            WallSlotSelectionStrategy wallStrategy = null)
        {
            _grid = grid;
            _gridActorConfig = gridActorConfig;
            _levelParams = levelParams;
            _wallStrategy = wallStrategy;
            AddMushroomFactory();
        }

        public void Start()
        {
            RegisterPools();
            _subscription = _boardClearSubscriber?.Subscribe(OnBoardClear);
        }

        public void Dispose()
        {
            _subscription?.Dispose();
        }

        public UniTask SpawnAsync(CancellationToken ct)
        {
            SpawnStaticActors();
            return UniTask.CompletedTask;
        }

        // The mushrooms first, then the clouds, then everything else, each group in the configuration's own
        // order. The walls are picked by the level's seed for the slots that are free, and the clouds and the
        // bushes are placed with the engine's own random numbers, which a retry does not replay: placed first,
        // on a board with nothing on it, the walls stand where they stood on every retry. What is placed after
        // them keeps off the slots next to a cap. On a level with no mushrooms the order is the one it always
        // was, the shipped configuration listing its clouds first.
        internal void SpawnStaticActors()
        {
            var emptySlots = new List<Vector2Int>(_grid.AllEmptySlots());
            var wallsPlaced = false;

            for (var stage = PlacementStage.Walls; stage <= PlacementStage.Rest; stage++)
            {
                foreach (var entry in _gridActorConfig.Entries)
                {
                    if (StageOf(entry.ActorType) != stage)
                    {
                        continue;
                    }

                    if (emptySlots.Count == 0)
                    {
                        return;
                    }

                    wallsPlaced |= PlaceEntry(entry, emptySlots, wallsPlaced);
                }
            }
        }

        // True when the entry placed the mushrooms' walls.
        private bool PlaceEntry(GridActorPrefabEntry entry, List<Vector2Int> emptySlots, bool wallsPlaced)
        {
            if (!_levelParams.Current.TryGetGridActorGate(entry.ActorType, out var gate))
            {
                // Absent from this level's range.
                return false;
            }

            var isWalls = entry.ActorType == GridActorType.Mushroom;
            if (isWalls && _wallStrategy == null)
            {
                return false;
            }

            // A mushroom gate counts walls, and a wall is up to three caps: the strategy answers with the caps.
            // Whatever comes after the walls, clouds and bushes alike, is not offered a slot next to a cap.
            var candidates = wallsPlaced && !isWalls ? ClearOfCaps(emptySlots) : emptySlots;
            var count = Mathf.Min(gate.Count, candidates.Count);
            if (count <= 0)
            {
                return false;
            }

            var strategy = isWalls ? _wallStrategy : GetStrategy(entry.PlacementMode);
            var selected = strategy.SelectSlots(candidates, count, gate.MaxPerCluster);

            for (var i = 0; i < selected.Count; i++)
            {
                PlaceActor(entry, selected[i], emptySlots, i);
            }

            return isWalls && selected.Count > 0;
        }

        // The empty slots a permanent thing may still take once the mushrooms stand: the ones that are not next
        // to a cap.
        private List<Vector2Int> ClearOfCaps(List<Vector2Int> emptySlots)
        {
            _clearOfCaps.Clear();
            foreach (var slot in emptySlots)
            {
                if (!_wallStrategy.IsBesideACap(slot))
                {
                    _clearOfCaps.Add(slot);
                }
            }

            return _clearOfCaps;
        }

        private void PlaceActor(
            GridActorPrefabEntry entry, Vector2Int slot, List<Vector2Int> emptySlots, int placeInSelection)
        {
            var model = CreateModel(entry.ActorType, placeInSelection);
            GridActorView view = null;

            if (_poolsRegistered)
            {
                view = _poolManager.Get<GridActorView>(entry.PoolKey);

                // Parent under the scenario root so Ascent can move the whole scenario as one unit. Placed in
                // the root's own space, where it stands at rest: a saved run continued from the launch screen
                // spawns as its mist reveal starts scaling the root, and a world position set then would leave
                // the view off its slot by that scale once the reveal ends.
                view.transform.SetParent(_scenarioRoot?.Transform, false);
                view.transform.localPosition = _grid.IndexToWorldPosition(slot);
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
            ClearStaticActors();
        }

        // Callable directly so Ascent can clear old statics without touching balloons mid-pop.
        internal void ClearStaticActors()
        {
            if (!_poolsRegistered)
            {
                return;
            }

            foreach (var entry in _gridActorConfig.Entries)
            {
                ReturnActorsForEntry(entry);
            }
        }

        // One kind only, so the scene stage (Editor/Staging) can take the clouds off a board and leave its bushes.
        internal void ClearStaticActors(GridActorType actorType)
        {
            if (!_poolsRegistered)
            {
                return;
            }

            foreach (var entry in _gridActorConfig.Entries)
            {
                if (entry.ActorType == actorType)
                {
                    ReturnActorsForEntry(entry);
                }
            }
        }

        // One actor in a chosen slot, so the scene stage (Editor/Staging) can stand a bush where a staged board
        // wants it. Not a mushroom: a cap belongs to a wall the level's seed picks. False when the slot is taken,
        // the type has no entry, or it is a mushroom.
        internal bool PlaceAt(GridActorType actorType, Vector2Int slot)
        {
            if (!_poolsRegistered || actorType == GridActorType.Mushroom || !_grid.IsEmpty(slot.x, slot.y))
            {
                return false;
            }

            foreach (var entry in _gridActorConfig.Entries)
            {
                if (entry.ActorType == actorType && _modelFactories.ContainsKey(actorType))
                {
                    PlaceActor(entry, slot, new List<Vector2Int>(), 0);
                    return true;
                }
            }

            return false;
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

        private void AddMushroomFactory()
        {
            _modelFactories[GridActorType.Mushroom] = placeInSelection => new MushroomBumperModel(
                placeInSelection, _wallStrategy?.WallOf(placeInSelection) ?? placeInSelection);
        }

        private IWriteableSlotActor CreateModel(GridActorType actorType, int placeInSelection)
        {
            if (_modelFactories.TryGetValue(actorType, out var factory))
            {
                return factory(placeInSelection);
            }

            throw new System.Exception("Unknown actor type: " + actorType);
        }

        private static bool ModelMatches(IWriteableSlotActor model, GridActorType actorType)
        {
            return model is IGridActorModel actor && actor.ActorType == actorType;
        }

        private static PlacementStage StageOf(GridActorType actorType)
        {
            return actorType switch
            {
                GridActorType.Puff => PlacementStage.Clouds,
                GridActorType.Mushroom => PlacementStage.Walls,
                _ => PlacementStage.Rest
            };
        }

        // The order a level's static actors are placed in, whatever order the configuration lists them in.
        private enum PlacementStage
        {
            Walls,
            Clouds,
            Rest
        }
    }
}
