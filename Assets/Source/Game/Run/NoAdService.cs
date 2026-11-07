using System.Threading;
using BalloonParty.Shared.Diagnostics;
using Cysharp.Threading.Tasks;

namespace BalloonParty.Game.Run
{
    /// <summary>No ad SDK yet: every ad counts as watched, so a gated retry goes straight through.</summary>
    internal sealed class NoAdService : IAdService
    {
        public UniTask<AdResult> ShowRewardedAsync(AdPlacement placement, CancellationToken ct)
        {
            Log.Info("Ads", $"No ad SDK: {placement} counts as watched");
            return UniTask.FromResult(AdResult.Watched);
        }
    }
}
