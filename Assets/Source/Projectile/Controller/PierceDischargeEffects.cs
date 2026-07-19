using System;
using System.Threading;
using BalloonParty.Configuration.Effects;
using BalloonParty.Shared;
using BalloonParty.Shared.Disturbance;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pause;
using Cysharp.Threading.Tasks;
using MessagePipe;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Projectile.Controller
{
    /// <summary>
    ///     The shared pierce-discharge feel: an outward shockwave stamp at the shattered line plus a brief
    ///     slow-mo dip, played whenever a piercing shot discharges (cruise-earned or Snipe — off
    ///     <see cref="PierceDischargedMessage" />). The rainbow bloom is separate (see SnipeDischargeBloom).
    /// </summary>
    internal sealed class PierceDischargeEffects : IStartable, IDisposable
    {
        private readonly ISubscriber<PierceDischargedMessage> _dischargedSubscriber;
        private readonly DisturbanceFieldService _disturbanceField;
        private readonly TimeScaleService _timeScale;
        private readonly IGameConfiguration _config;

        private IDisposable _subscription;
        private CancellationTokenSource _dipCts;

        internal PierceDischargeEffects(
            ISubscriber<PierceDischargedMessage> dischargedSubscriber,
            DisturbanceFieldService disturbanceField,
            TimeScaleService timeScale,
            IGameConfiguration config)
        {
            _dischargedSubscriber = dischargedSubscriber;
            _disturbanceField = disturbanceField;
            _timeScale = timeScale;
            _config = config;
        }

        public void Start()
        {
            _subscription = _dischargedSubscriber.Subscribe(OnDischarged);
        }

        public void Dispose()
        {
            LifecycleHelper.CancelAndDispose(ref _dipCts);
            _subscription?.Dispose();
            // A dip cancelled above (or mid-flight) skips its own Release, so drop the claim here.
            _timeScale.Release(TimeScaleSource.PierceDischarge);
        }

        private void OnDischarged(PierceDischargedMessage msg)
        {
            _disturbanceField.Stamp(StampSource.PierceDischarge, msg.Center, Vector2.zero);

            if (_config.PierceDischargeTimeScale < 1f && _config.PierceDischargeTimeScaleDuration > 0f)
            {
                // Cancel-and-restart so a fresh discharge always gets the full dip; only the LATEST dip's
                // continuation releases the claim (a cancelled one bows out without releasing).
                LifecycleHelper.CancelAndDispose(ref _dipCts);
                _dipCts = new CancellationTokenSource();
                SlowMoDipAsync(_dipCts.Token).Forget();
            }
        }

        // The delay ignores time scale — it counts real seconds while the world is slowed.
        private async UniTaskVoid SlowMoDipAsync(CancellationToken ct)
        {
            _timeScale.Claim(TimeScaleSource.PierceDischarge, _config.PierceDischargeTimeScale);
            var canceled = await UniTask.Delay(
                TimeSpan.FromSeconds(_config.PierceDischargeTimeScaleDuration),
                ignoreTimeScale: true,
                cancellationToken: ct).SuppressCancellationThrow();

            // A newer discharge (or teardown) cancelled us — it now owns the claim, so don't release it.
            if (!canceled)
            {
                _timeScale.Release(TimeScaleSource.PierceDischarge);
            }
        }
    }
}
