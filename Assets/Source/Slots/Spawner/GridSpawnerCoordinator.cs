using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using BalloonParty.Game.Run;
using BalloonParty.Shared.GameState;
using Cysharp.Threading.Tasks;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Slots.Spawner
{
    internal class GridSpawnerCoordinator : IStartable, IDisposable, IBoardResettable
    {
        private readonly IReadOnlyList<IGridSpawner> _spawners;
        private readonly NavigationReadyGate _gate;
        private readonly CancellationTokenSource _cts = new();

        public int ResetOrder => RunResetOrder.Respawn;

        [Inject]
        internal GridSpawnerCoordinator(IEnumerable<IGridSpawner> spawners, NavigationReadyGate gate)
        {
            _spawners = spawners.OrderBy(s => (int)s.SpawnPriority).ToList();
            _gate = gate;
        }

        public void Start()
        {
            CoordinateAsync(_cts.Token).Forget();
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
        }

        // Final reset stage — repopulates the cleared board, skipping the navigation gate. Skipped (as an
        // IBoardResettable) when the loss→restart cinematic drives the board swap itself.
        public void ResetRun(int generation)
        {
            RunGroupsAsync(_cts.Token).Forget();
        }

        // Runs only matching stages, in priority order — used by Ascent to sequence statics/balloons.
        internal async UniTask RunStagesAsync(Func<SpawnStage, bool> predicate, CancellationToken ct)
        {
            var groups = _spawners
                .Where(s => predicate(s.SpawnPriority))
                .GroupBy(s => s.SpawnPriority)
                .OrderBy(g => (int)g.Key);

            foreach (var group in groups)
            {
                await UniTask.WhenAll(group.Select(s => s.SpawnAsync(ct)));
            }
        }

        private async UniTaskVoid CoordinateAsync(CancellationToken ct)
        {
            await _gate.WaitAsync(ct);
            await RunGroupsAsync(ct);
        }

        // Spawners within the same SpawnStage run in parallel; stages run sequentially.
        private async UniTask RunGroupsAsync(CancellationToken ct)
        {
            var groups = _spawners
                .GroupBy(s => s.SpawnPriority)
                .OrderBy(g => (int)g.Key);

            foreach (var group in groups)
            {
                await UniTask.WhenAll(group.Select(s => s.SpawnAsync(ct)));
            }
        }
    }
}
