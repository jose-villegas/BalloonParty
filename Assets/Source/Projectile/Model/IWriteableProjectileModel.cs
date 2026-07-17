using BalloonParty.Balloon.Model;
using UniRx;
using UnityEngine;

namespace BalloonParty.Projectile.Model
{
    public interface IWriteableProjectileModel : IProjectileModel
    {
        new ReactiveProperty<string> ColorName { get; }
        new ReactiveProperty<int> ShieldsRemaining { get; }
        new ReactiveProperty<bool> IsCruising { get; }
        new ReactiveProperty<bool> IsPiercing { get; }
        new ReactiveProperty<bool> IsLastShieldApproach { get; }

        // The mutable flight bookkeeping (the read interface exposes it read-only).
        new ProjectileFlightState Flight { get; }

        new Vector3 Direction { get; set; }
        new float Speed { get; set; }
        new bool IsFree { get; set; }
        new IBalloonModel LastHitBalloon { get; set; }

        void AddBuff(ProjectileBuff buff);
        void RemoveBuff(ProjectileBuff buff);
    }
}
