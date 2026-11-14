using System;
using System.Collections.Generic;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared.Messages;
using MessagePipe;
using UniRx;
using VContainer.Unity;

namespace BalloonParty.Projectile.Controller
{
    /// <summary>Whether any live shot is currently piercing, and which shot just started to.</summary>
    internal interface IActiveProjectilePierce
    {
        IReadOnlyReactiveProperty<bool> IsPiercing { get; }

        /// <summary>
        ///     A shot whose piercing went from off to on, once per arming. Unlike <see cref="IsPiercing" />
        ///     it fires again when a second shot arms while the first still pierces, and not when a banked
        ///     Snipe charge keeps a shot armed through its discharge.
        /// </summary>
        IObservable<IProjectileModel> PierceArmed { get; }
    }

    /// <summary>
    ///     Folds every live shot's piercing state into one shared signal, so systems that must hold off
    ///     while a shot plows through the board (the level-up ceremony) can gate on it and re-evaluate the
    ///     moment the last pierce ends — without each tracking the shots themselves. A ball armed while still
    ///     held in the thrower (a Snipe pickup, or the Sharpshooter card as it loads) counts only once it has
    ///     left: its arming is announced when it fires, so the pierce loop and the Pierce heads-up start with
    ///     the shot, not while it is aimed.
    /// </summary>
    internal sealed class ActiveProjectilePierce : IActiveProjectilePierce, IStartable, IDisposable
    {
        private readonly IProjectileRegistry _projectiles;
        private readonly ISubscriber<ProjectileLoadedMessage> _loadedSubscriber;
        private readonly ISubscriber<ProjectileFiredMessage> _firedSubscriber;
        private readonly ReactiveProperty<bool> _isPiercing = new(false);
        private readonly Subject<IProjectileModel> _pierceArmed = new();
        private readonly CompositeDisposable _subscriptions = new();
        private readonly List<Tracked> _tracked = new();

        public IReadOnlyReactiveProperty<bool> IsPiercing => _isPiercing;

        public IObservable<IProjectileModel> PierceArmed => _pierceArmed;

        internal ActiveProjectilePierce(
            IProjectileRegistry projectiles, ISubscriber<ProjectileLoadedMessage> loadedSubscriber,
            ISubscriber<ProjectileFiredMessage> firedSubscriber)
        {
            _projectiles = projectiles;
            _loadedSubscriber = loadedSubscriber;
            _firedSubscriber = firedSubscriber;
        }

        public void Start()
        {
            _loadedSubscriber.Subscribe(OnProjectileLoaded).AddTo(_subscriptions);
            _firedSubscriber.Subscribe(OnProjectileFired).AddTo(_subscriptions);
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
            _pierceArmed.OnCompleted();
            _pierceArmed.Dispose();
        }

        // ReactiveProperty dedupes, so a fresh (not-piercing) shot after a pierce that already ended
        // emits nothing.
        private void OnProjectileLoaded(ProjectileLoadedMessage msg)
        {
            var projectile = msg.Model;
            if (projectile != null)
            {
                // Added before subscribing: the subscription fires at once and Refresh must see it.
                var index = _tracked.Count;
                _tracked.Add(new Tracked(projectile, Disposable.Empty));
                var subscription = projectile.IsPiercing.Subscribe(piercing => OnPiercingChanged(projectile, piercing));
                _tracked[index] = new Tracked(projectile, subscription);
            }

            Refresh();
        }

        // A ball armed in the thrower arms, as far as the fold and its listeners know, the moment it leaves.
        private void OnProjectileFired(ProjectileFiredMessage msg)
        {
            var projectile = msg.Projectile;
            if (projectile == null || !projectile.IsPiercing.Value || !IsTracked(projectile))
            {
                return;
            }

            Refresh();
            _pierceArmed.OnNext(projectile);
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

        // The property dedupes, so every true it sends is an arming. The fold is refreshed first, so a
        // listener to the arming already reads the shot as piercing.
        private void OnPiercingChanged(IProjectileModel projectile, bool piercing)
        {
            Refresh();
            if (piercing && !IsHeld(projectile))
            {
                _pierceArmed.OnNext(projectile);
            }
        }

        private void Refresh()
        {
            var any = false;
            foreach (var tracked in _tracked)
            {
                any |= tracked.Projectile.IsPiercing.Value && !IsHeld(tracked.Projectile);
            }

            _isPiercing.Value = any;
        }

        private bool IsHeld(IProjectileModel projectile)
        {
            return projectile == _projectiles.Loaded;
        }

        private bool IsTracked(IProjectileModel projectile)
        {
            foreach (var tracked in _tracked)
            {
                if (tracked.Projectile == projectile)
                {
                    return true;
                }
            }

            return false;
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
