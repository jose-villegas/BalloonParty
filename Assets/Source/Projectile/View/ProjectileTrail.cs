using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace BalloonParty.Projectile.View
{
    [RequireComponent(typeof(TrailRenderer))]
    public class ProjectileTrail : MonoBehaviour
    {
        private TrailRenderer _trail;
        private CancellationTokenSource _cts;

        private void Awake()
        {
            _trail = GetComponent<TrailRenderer>();
            Disable();
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }

        public void Enable()
        {
            // Cancel any pending enable first; a pooled projectile is disabled (not destroyed) on return,
            // so a queued enable would otherwise resume on the next-loaded instance.
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            EnableNextFrameAsync(_cts.Token).Forget();
        }

        public void Disable()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            _trail.emitting = false;
            _trail.Clear();
        }

        private async UniTaskVoid EnableNextFrameAsync(CancellationToken ct)
        {
            var canceled = await UniTask.Yield(ct).SuppressCancellationThrow();
            if (canceled)
            {
                return;
            }

            _trail.Clear();
            _trail.emitting = true;
        }
    }
}
