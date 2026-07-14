using System;
using System.Threading;
using DG.Tweening;

namespace BalloonParty.Shared.Extensions
{
    internal static class LifecycleHelper
    {
        /// <summary>Disposes and nulls an <see cref="IDisposable"/> in one call.</summary>
        internal static void DisposeAndClear(ref IDisposable disposable)
        {
            disposable?.Dispose();
            disposable = null;
        }

        /// <summary>Cancels, disposes, and nulls a <see cref="CancellationTokenSource"/>.</summary>
        internal static void CancelAndDispose(ref CancellationTokenSource cts)
        {
            cts?.Cancel();
            cts?.Dispose();
            cts = null;
        }

        /// <summary>Kills and nulls a DOTween <see cref="Tween"/>.</summary>
        internal static void KillAndClear(ref Tween tween)
        {
            tween?.Kill();
            tween = null;
        }

        /// <summary>Kills and nulls a DOTween <see cref="Tweener"/>.</summary>
        internal static void KillAndClear(ref Tweener tweener)
        {
            tweener?.Kill();
            tweener = null;
        }
    }
}


