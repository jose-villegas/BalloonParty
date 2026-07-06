using System.Threading;
using Cysharp.Threading.Tasks;

namespace BalloonParty.Shared
{
    /// <summary>Blocks a caller until some external condition allows it to proceed; "ready" is implementation-defined.</summary>
    internal interface IReadyGate
    {
        UniTask WaitAsync(CancellationToken ct);
    }
}
