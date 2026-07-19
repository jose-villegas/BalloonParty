using System.Threading;
using BalloonParty.Shared.Extensions;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace BalloonParty.Projectile.View
{
    [RequireComponent(typeof(TrailRenderer))]
    public class ProjectileTrail : MonoBehaviour
    {
        [Tooltip("Multiplies the trail's lifetime the instant a piercing shot discharges — the lance " +
                 "streaks longer as it shatters the plowed toughs, then eases back over the restore time.")]
        [SerializeField] [Min(1f)] private float _dischargeTimeMultiplier = 2f;

        [Tooltip("Seconds the boosted trail lifetime takes to lerp back to normal after a discharge.")]
        [SerializeField] [Min(0f)] private float _dischargeRestoreDuration = 0.4f;

        private TrailRenderer _trail;
        private CancellationTokenSource _cts;
        private CancellationTokenSource _boostCts;
        private float _baseTime;

        private void Awake()
        {
            _trail = GetComponent<TrailRenderer>();
            _baseTime = _trail.time;
            Disable();
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _boostCts?.Cancel();
            _boostCts?.Dispose();
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
            LifecycleHelper.CancelAndDispose(ref _cts);
            LifecycleHelper.CancelAndDispose(ref _boostCts);
            _trail.emitting = false;
            _trail.Clear();
        }

        // The discharge flourish: snap the trail longer as a piercing shot shatters the toughs it
        // plowed, then ease it back to the base lifetime over the restore duration.
        public void Boost()
        {
            LifecycleHelper.CancelAndDispose(ref _boostCts);
            _boostCts = new CancellationTokenSource();
            BoostAsync(_boostCts.Token).Forget();
        }

        private async UniTaskVoid EnableNextFrameAsync(CancellationToken ct)
        {
            var canceled = await UniTask.Yield(ct).SuppressCancellationThrow();
            if (canceled)
            {
                return;
            }

            // A pooled instance may carry a discharge-boosted lifetime from its previous flight — restore
            // the base before emitting so each shot starts at its normal trail length.
            _trail.time = _baseTime;
            _trail.Clear();
            _trail.emitting = true;
        }

        private async UniTaskVoid BoostAsync(CancellationToken ct)
        {
            var boosted = _baseTime * _dischargeTimeMultiplier;
            _trail.time = boosted;

            var elapsed = 0f;
            while (elapsed < _dischargeRestoreDuration)
            {
                var canceled = await UniTask.Yield(PlayerLoopTiming.Update, ct).SuppressCancellationThrow();
                if (canceled)
                {
                    return;
                }

                elapsed += Time.deltaTime;
                _trail.time = Mathf.Lerp(boosted, _baseTime, elapsed / _dischargeRestoreDuration);
            }

            _trail.time = _baseTime;
        }
    }
}
