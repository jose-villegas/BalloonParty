using BalloonParty.Balloon.Model;
using UniRx;
using UnityEngine;

namespace BalloonParty.Projectile.Model
{
    public interface IWriteableProjectileModel : IProjectileModel
    {
        new ReactiveProperty<string> ColorName { get; }
        new ReactiveProperty<int> ShieldsRemaining { get; }
        new Vector3 Direction { get; set; }
        new float Speed { get; set; }
        new bool IsFree { get; set; }
        new int ColorPopCount { get; set; }
        new IBalloonModel LastHitBalloon { get; set; }
    }
}
