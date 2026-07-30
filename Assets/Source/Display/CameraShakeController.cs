using System;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Messages;
using MessagePipe;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Display
{
    /// <summary>
    ///     Decides WHEN the camera shakes: punches on <see cref="WaveDamageMessage" /> (once per heart lost)
    ///     and recoils on <see cref="ProjectileFiredMessage" />, both suppressed while a cinematic reports
    ///     <see cref="CinematicTraits.BlocksShake" />. Lives in the run scope so its subscriptions tear down
    ///     each run; drives the persistent <see cref="CameraShakeView" />, which it resolves from this scope
    ///     or its parent.
    /// </summary>
    internal sealed class CameraShakeController : IStartable, IDisposable
    {
        private readonly CameraShakeView _view;
        private readonly ICinematicState _cinematic;
        private readonly ISubscriber<WaveDamageMessage> _waveDamageSubscriber;
        private readonly ISubscriber<ProjectileFiredMessage> _firedSubscriber;

        private IDisposable _subscription;
        private IDisposable _recoilSubscription;

        public CameraShakeController(
            CameraShakeView view,
            ICinematicState cinematic,
            ISubscriber<WaveDamageMessage> waveDamageSubscriber,
            ISubscriber<ProjectileFiredMessage> firedSubscriber)
        {
            _view = view;
            _cinematic = cinematic;
            _waveDamageSubscriber = waveDamageSubscriber;
            _firedSubscriber = firedSubscriber;
        }

        void IStartable.Start()
        {
            _subscription = _waveDamageSubscriber.Subscribe(msg => Shake(msg.HeartsLost));
            _recoilSubscription = _firedSubscriber.Subscribe(msg => Recoil(msg.Direction));
        }

        void IDisposable.Dispose()
        {
            _subscription?.Dispose();
            _recoilSubscription?.Dispose();
        }

        private void Shake(int heartsLost)
        {
            if (_cinematic.Has(CinematicTraits.BlocksShake))
            {
                return;
            }

            for (var i = 0; i < heartsLost; i++)
            {
                _view.Shake();
            }
        }

        private void Recoil(Vector3 fireDirection)
        {
            if (_cinematic.Has(CinematicTraits.BlocksShake))
            {
                return;
            }

            _view.Recoil(fireDirection);
        }
    }
}
