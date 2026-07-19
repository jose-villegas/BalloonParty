using System;
using BalloonParty.Shared.Messages;
using MessagePipe;
using UniRx;
using VContainer.Unity;

namespace BalloonParty.Projectile.Controller
{
    /// <summary>Whether the active shot is currently piercing.</summary>
    internal interface IActiveProjectilePierce
    {
        IReadOnlyReactiveProperty<bool> IsPiercing { get; }
    }

    /// <summary>
    ///     Mirrors the currently-loaded projectile's piercing state as one shared signal, so systems that
    ///     must hold off while a shot plows through the board (the level-up ceremony) can gate on it and
    ///     re-evaluate the moment the pierce ends — without each tracking the active projectile themselves.
    /// </summary>
    internal sealed class ActiveProjectilePierce : IActiveProjectilePierce, IStartable, IDisposable
    {
        private readonly ISubscriber<ProjectileLoadedMessage> _loadedSubscriber;
        private readonly ReactiveProperty<bool> _isPiercing = new(false);
        private readonly CompositeDisposable _subscriptions = new();

        private IDisposable _activeSubscription;

        public IReadOnlyReactiveProperty<bool> IsPiercing => _isPiercing;

        internal ActiveProjectilePierce(ISubscriber<ProjectileLoadedMessage> loadedSubscriber)
        {
            _loadedSubscriber = loadedSubscriber;
        }

        public void Start()
        {
            _loadedSubscriber.Subscribe(OnProjectileLoaded).AddTo(_subscriptions);
        }

        public void Dispose()
        {
            _activeSubscription?.Dispose();
            _subscriptions.Dispose();
            _isPiercing.Dispose();
        }

        // Track the freshly-loaded shot's piercing state; the previous shot's tracking is dropped so the
        // signal always follows the live projectile. ReactiveProperty dedupes, so a new (not-piercing)
        // shot after a piercing one that already ended emits nothing.
        private void OnProjectileLoaded(ProjectileLoadedMessage msg)
        {
            _activeSubscription?.Dispose();
            _activeSubscription = msg.Model.IsPiercing.Subscribe(piercing => _isPiercing.Value = piercing);
        }
    }
}
