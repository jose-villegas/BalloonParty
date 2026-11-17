using System;
using BalloonParty.Configuration.Effects;
using BalloonParty.Projectile;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Display
{
    /// <summary>
    ///     Feeds the clearing mask from the balls in flight. Run-scoped, so it binds itself to the persistent
    ///     <see cref="EffectsClearingMaskView" /> for the run and unbinds when the run's scope goes.
    /// </summary>
    internal sealed class EffectsClearingController : IStartable, IDisposable, IEffectsClearingSource
    {
        private readonly IProjectileRegistry _registry;
        private readonly EffectsClearingMaskView _mask;
        private readonly EffectsClearingStamper _stamper;

        private IDisposable _retiredSubscription;

        public EffectsClearingController(
            IProjectileRegistry registry, EffectsClearingMaskView mask, IEffectsClearingSettings settings)
        {
            _registry = registry;
            _mask = mask;
            _stamper = new EffectsClearingStamper(settings);
        }

        void IStartable.Start()
        {
            _retiredSubscription = _registry.Retired.Subscribe(_stamper.Forget);
            _mask.Bind(this);
        }

        void IDisposable.Dispose()
        {
            _retiredSubscription?.Dispose();
            if (_mask != null)
            {
                _mask.Unbind(this);
            }
        }

        bool IEffectsClearingSource.AnyInFlight => _registry.InFlightCount > 0;

        int IEffectsClearingSource.FillStamps(in ClearingView view, float deltaTime, EffectsClearingStamps stamps)
        {
            return _stamper.Fill(_registry, view, deltaTime, stamps);
        }
    }
}
