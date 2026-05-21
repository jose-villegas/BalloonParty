using System.Threading;
using Cysharp.Threading.Tasks;

namespace BalloonParty.Slots
{
    internal interface IGridSpawner
    {
        SpawnStage SpawnPriority { get; }
        UniTask SpawnAsync(CancellationToken ct);
    }
}

