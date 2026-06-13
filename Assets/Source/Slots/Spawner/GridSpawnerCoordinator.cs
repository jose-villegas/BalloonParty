using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using BalloonParty.Game.Run;
using BalloonParty.Shared;
using Cysharp.Threading.Tasks;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Slots.Spawner
{
    internal class GridSpawnerCoordinator : IStartable, IDisposable, IRunResettable
    {
        private readonly IReadOnlyList<IGridSpawner> _spawners;
        private readonly IReadyGate _gate;
        private readonly CancellationTokenSource _cts = new();

        [Inject]
        internal GridSpawnerCoordinator(IEnumerable<IGridSpawner> spawners, IReadyGate gate)
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

        public int ResetOrder => RunResetOrder.Respawn;

        // Final reset stage: the board has been cleared and counters reset, so re-run every
        // spawner to repopulate a fresh board. Skips the navigation gate — RestartRun has
        // already put us back in Game.
        public void ResetRun(int generation)
        {
            RunGroupsAsync(_cts.Token).Forget();
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
