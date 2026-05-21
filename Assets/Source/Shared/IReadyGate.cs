using System.Threading;
using Cysharp.Threading.Tasks;

namespace BalloonParty.Shared
{
    /// <summary>
    /// Blocks a caller until some external condition allows it to proceed.
    /// Implementations decide what "ready" means — navigation state, asset load, cinematic end, etc.
    /// </summary>
    internal interface IReadyGate
    {
        UniTask WaitAsync(CancellationToken ct);
    }
}

