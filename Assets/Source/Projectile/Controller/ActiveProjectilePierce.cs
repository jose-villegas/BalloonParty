using System;
using System.Collections.Generic;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared.Messages;
using MessagePipe;
using UniRx;
using VContainer.Unity;

namespace BalloonParty.Projectile.Controller
{
    /// <summary>Whether any live shot is currently piercing.</summary>
    internal interface IActiveProjectilePierce
    {
        IReadOnlyReactiveProperty<bool> IsPiercing { get; }
    }

    /// <summary>
    ///     Folds every live shot's piercing state into one shared signal, so systems that must hold off
    ///     while a shot plows through the board (the level-up ceremony) can gate on it and re-evaluate the
    ///     moment the last pierce ends — without each tracking the shots themselves.
    /// </summary>
    internal sealed class ActiveProjectilePierce : IActiveProjectilePierce, IStartable, IDisposable
    {
        private readonly IProjectileRegistry _projectiles;
        private readonly ISubscriber<ProjectileLoadedMessage> _loadedSubscriber;
        private readonly ReactiveProperty<bool> _isPiercing = new(false);
        private readonly CompositeDisposable _subscriptions = new();
        private readonly List<Tracked> _tracked = new();

        public IReadOnlyReactiveProperty<bool> IsPiercing => _isPiercing;

        internal ActiveProjectilePierce(
            IProjectileRegistry projectiles, ISubscriber<ProjectileLoadedMessage> loadedSubscriber)
        {
            _projectiles = projectiles;
            _loadedSubscriber = loadedSubscriber;
        }

        public void Start()
        {
            _loadedSubscriber.Subscribe(OnProjectileLoaded).AddTo(_subscriptions);
            _projectiles.Retired.Subscribe(OnRetired).AddTo(_subscriptions);
        }

        public void Dispose()
        {
            foreach (var tracked in _tracked)
            {
                tracked.Subscription.Dispose();
            }

            _tracked.Clear();
            _subscriptions.Dispose();
            _isPiercing.Dispose();
        }

        // ReactiveProperty dedupes, so a fresh (not-piercing) shot after a pierce that already ended
        // emits nothing.
        private void OnProjectileLoaded(ProjectileLoadedMessage msg)
        {
            if (msg.Model != null)
            {
                // Added before subscribing: the subscription fires at once and Refresh must see it.
                var index = _tracked.Count;
                _tracked.Add(new Tracked(msg.Model, Disposable.Empty));
                var subscription = msg.Model.IsPiercing.Subscribe(_ => Refresh());
                _tracked[index] = new Tracked(msg.Model, subscription);
            }

            Refresh();
        }

        // A dying shot stops piercing before it publishes Destroyed, so retiring it later changes the
        // fold only for a ball that left without dying (game over, reset).
        private void OnRetired(IProjectileModel projectile)
        {
            for (var i = _tracked.Count - 1; i >= 0; i--)
            {
                if (_tracked[i].Projectile == projectile)
                {
                    _tracked[i].Subscription.Dispose();
                    _tracked.RemoveAt(i);
                }
            }

            Refresh();
        }

        private void Refresh()
        {
            var any = false;
            foreach (var tracked in _tracked)
            {
                any |= tracked.Projectile.IsPiercing.Value;
            }

            _isPiercing.Value = any;
        }

        private readonly struct Tracked
        {
            internal readonly IProjectileModel Projectile;
            internal readonly IDisposable Subscription;

            internal Tracked(IProjectileModel projectile, IDisposable subscription)
            {
                Projectile = projectile;
                Subscription = subscription;
            }
        }
    }
}
