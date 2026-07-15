using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using UniRx;
using UnityEngine;

namespace BalloonParty.Projectile.Model
{
    internal class ProjectileModel : IWriteableProjectileModel
    {
        private readonly List<ProjectileBuff> _buffs = new();

        public ReactiveProperty<string> ColorName { get; } = new(null);
        public ReactiveProperty<int> ShieldsRemaining { get; } = new(0);

        public Vector3 Direction { get; set; }
        public float Speed { get; set; }
        public bool IsFree { get; set; }
        public IBalloonModel LastHitBalloon { get; set; }

        IReadOnlyReactiveProperty<string> IProjectileModel.ColorName => ColorName;
        IReadOnlyReactiveProperty<int> IProjectileModel.ShieldsRemaining => ShieldsRemaining;

        public bool HasBuff(ProjectileBuffId id)
        {
            for (var i = 0; i < _buffs.Count; i++)
            {
                if (_buffs[i].Id == id)
                {
                    return true;
                }
            }

            return false;
        }

        public float GetBuffFactor(ProjectileBuffId id, float defaultValue = 0f)
        {
            for (var i = 0; i < _buffs.Count; i++)
            {
                if (_buffs[i].Id == id)
                {
                    return _buffs[i].Factor;
                }
            }

            return defaultValue;
        }

        public void AddBuff(ProjectileBuff buff)
        {
            if (!_buffs.Contains(buff))
            {
                _buffs.Add(buff);
            }
        }

        public void RemoveBuff(ProjectileBuff buff)
        {
            _buffs.Remove(buff);
        }
    }
}
