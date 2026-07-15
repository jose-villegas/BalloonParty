using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using UniRx;
using UnityEngine;

namespace BalloonParty.Projectile.Model
{
    internal class ProjectileModel : IWriteableProjectileModel
    {
        private readonly List<IProjectileBuff> _buffs = new();

        public ReactiveProperty<string> ColorName { get; } = new(null);
        public ReactiveProperty<int> ShieldsRemaining { get; } = new(0);

        public Vector3 Direction { get; set; }
        public float Speed { get; set; }
        public bool IsFree { get; set; }
        public IBalloonModel LastHitBalloon { get; set; }

        IReadOnlyReactiveProperty<string> IProjectileModel.ColorName => ColorName;
        IReadOnlyReactiveProperty<int> IProjectileModel.ShieldsRemaining => ShieldsRemaining;

        public bool HasBuff<T>()
            where T : IProjectileBuff
        {
            for (var i = 0; i < _buffs.Count; i++)
            {
                if (_buffs[i] is T)
                {
                    return true;
                }
            }

            return false;
        }

        public T GetBuff<T>()
            where T : class, IProjectileBuff
        {
            for (var i = 0; i < _buffs.Count; i++)
            {
                if (_buffs[i] is T match)
                {
                    return match;
                }
            }

            return null;
        }

        public void AddBuff(IProjectileBuff buff)
        {
            if (!_buffs.Contains(buff))
            {
                _buffs.Add(buff);
            }
        }

        public void RemoveBuff(IProjectileBuff buff)
        {
            _buffs.Remove(buff);
        }
    }
}
