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
        public ReactiveProperty<bool> IsCruising { get; } = new(false);
        public ReactiveProperty<bool> IsPiercing { get; } = new(false);
        public ReactiveProperty<bool> IsLastShieldApproach { get; } = new(false);

        public Vector3 Direction { get; set; }
        public float Speed { get; set; }
        public bool IsFree { get; set; }
        public IBalloonModel LastHitBalloon { get; set; }
        public int ConsecutiveWallBounces { get; set; }
        public int CruiseStartShields { get; set; }
        public float CruiseTapElapsed { get; set; }
        public float CruisePierceSpeedScale { get; set; } = 1f;
        public Vector3 SegmentStartPosition { get; set; }
        public float SegmentElapsed { get; set; }

        IReadOnlyReactiveProperty<string> IProjectileModel.ColorName => ColorName;
        IReadOnlyReactiveProperty<int> IProjectileModel.ShieldsRemaining => ShieldsRemaining;
        IReadOnlyReactiveProperty<bool> IProjectileModel.IsCruising => IsCruising;
        IReadOnlyReactiveProperty<bool> IProjectileModel.IsPiercing => IsPiercing;
        IReadOnlyReactiveProperty<bool> IProjectileModel.IsLastShieldApproach => IsLastShieldApproach;

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

        public float ComputeBuffedValue(ProjectileBuffId id, float baseValue)
        {
            var flat = 0f;
            var additive = 0f;
            var multiplicative = 1f;
            var found = false;

            for (var i = 0; i < _buffs.Count; i++)
            {
                if (_buffs[i].Id != id)
                {
                    continue;
                }

                found = true;
                switch (_buffs[i].Op)
                {
                    case BuffModifierOp.Flat:
                        flat += _buffs[i].Value;
                        break;
                    case BuffModifierOp.Additive:
                        additive += _buffs[i].Value;
                        break;
                    case BuffModifierOp.Multiplicative:
                        multiplicative *= _buffs[i].Value;
                        break;
                }
            }

            return found ? (baseValue + flat) * (1f + additive) * multiplicative : baseValue;
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
