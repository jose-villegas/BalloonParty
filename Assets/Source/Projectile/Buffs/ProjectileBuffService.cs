using System;
using System.Collections.Generic;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared.Messages;
using MessagePipe;
using UniRx;
using VContainer.Unity;

namespace BalloonParty.Projectile.Buffs
{
    /// <summary>
    ///     Owns projectile-buff storage and lifecycle: applies buffs onto the active projectile and drops
    ///     one the first time its end-condition's <c>Expired</c> fires. Knows nothing about any buff's
    ///     effect or end-condition — it only observes the exposed signal.
    /// </summary>
    internal sealed class ProjectileBuffService : IProjectileBuffs, IStartable, IDisposable
    {
        private readonly ISubscriber<ProjectileLoadedMessage> _loadedSubscriber;
        private readonly Dictionary<ProjectileBuff, IDisposable> _expiries = new();
        private readonly CompositeDisposable _subscriptions = new();

        private IWriteableProjectileModel _active;

        internal ProjectileBuffService(ISubscriber<ProjectileLoadedMessage> loadedSubscriber)
        {
            _loadedSubscriber = loadedSubscriber;
        }

        public void Start()
        {
            _loadedSubscriber.Subscribe(OnProjectileLoaded).AddTo(_subscriptions);
        }

        public void Apply(ProjectileBuff buff)
        {
            if (_active == null)
            {
                buff.Dispose();
                return;
            }

            _active.AddBuff(buff);
            _expiries[buff] = buff.EndCondition.Expired
                .Where(expired => expired)
                .Take(1)
                .Subscribe(_ => Remove(buff));
        }

        public void Dispose()
        {
            ClearActiveBuffs();
            _subscriptions.Dispose();
        }

        // A fresh projectile carries no buffs; any left on the previous one are dropped so their
        // subscriptions never outlive it.
        private void OnProjectileLoaded(ProjectileLoadedMessage msg)
        {
            ClearActiveBuffs();
            _active = (IWriteableProjectileModel)msg.Model;
        }

        private void Remove(ProjectileBuff buff)
        {
            if (_expiries.TryGetValue(buff, out var expiry))
            {
                expiry.Dispose();
                _expiries.Remove(buff);
            }

            _active?.RemoveBuff(buff);
            buff.Dispose();
        }

        private void ClearActiveBuffs()
        {
            foreach (var pair in _expiries)
            {
                pair.Value.Dispose();
                _active?.RemoveBuff(pair.Key);
                pair.Key.Dispose();
            }

            _expiries.Clear();
        }
    }
}
