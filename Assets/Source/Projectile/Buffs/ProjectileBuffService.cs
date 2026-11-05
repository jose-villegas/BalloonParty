using System;
using System.Collections.Generic;
using BalloonParty.Projectile.Model;
using UniRx;
using VContainer.Unity;

namespace BalloonParty.Projectile.Buffs
{
    /// <summary>
    ///     Owns projectile-buff storage and lifecycle: applies buffs onto a shot and drops one the first
    ///     time its end-condition's <c>Expired</c> fires, or when the registry retires its shot. Knows nothing about any
    ///     buff's effect or end-condition — it only observes the exposed signal.
    /// </summary>
    internal sealed class ProjectileBuffService : IProjectileBuffs, IStartable, IDisposable
    {
        private readonly IProjectileRegistry _projectiles;
        private readonly Dictionary<ProjectileBuff, Entry> _entries = new();
        private readonly List<ProjectileBuff> _dropBuffer = new();
        private readonly CompositeDisposable _subscriptions = new();

        internal ProjectileBuffService(IProjectileRegistry projectiles)
        {
            _projectiles = projectiles;
        }

        public void Start()
        {
            _projectiles.Retired.Subscribe(DropBuffsOf).AddTo(_subscriptions);
        }

        public void Apply(IProjectileModel projectile, ProjectileBuff buff)
        {
            if (projectile is not IWriteableProjectileModel target || !_projectiles.IsAlive(projectile))
            {
                buff.Dispose();
                return;
            }

            target.AddBuff(buff);

            // Recorded before subscribing: an already-expired condition fires inside Subscribe, and
            // Remove must find the entry to undo the add.
            var expiry = new UniRx.SingleAssignmentDisposable();
            _entries[buff] = new Entry(target, expiry);
            expiry.Disposable = buff.EndCondition.Expired
                .Where(expired => expired)
                .Take(1)
                .Subscribe(_ => Remove(buff));
        }

        public void Dispose()
        {
            _dropBuffer.Clear();
            _dropBuffer.AddRange(_entries.Keys);
            RemoveBuffered();
            _subscriptions.Dispose();
        }

        // On retire, after the shot's Destroyed dispatch, so every subscriber of it still sees its buffs.
        private void DropBuffsOf(IProjectileModel projectile)
        {
            _dropBuffer.Clear();
            foreach (var pair in _entries)
            {
                if (pair.Value.Target == projectile)
                {
                    _dropBuffer.Add(pair.Key);
                }
            }

            RemoveBuffered();
        }

        private void RemoveBuffered()
        {
            foreach (var buff in _dropBuffer)
            {
                Remove(buff);
            }

            _dropBuffer.Clear();
        }

        private void Remove(ProjectileBuff buff)
        {
            if (!_entries.TryGetValue(buff, out var entry))
            {
                return;
            }

            _entries.Remove(buff);
            entry.Expiry.Dispose();
            entry.Target.RemoveBuff(buff);
            buff.Dispose();
        }

        private readonly struct Entry
        {
            internal readonly IWriteableProjectileModel Target;
            internal readonly IDisposable Expiry;

            internal Entry(IWriteableProjectileModel target, IDisposable expiry)
            {
                Target = target;
                Expiry = expiry;
            }
        }
    }
}
