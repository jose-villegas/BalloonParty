using BalloonParty.Balloon.Model;
using UniRx;
using UnityEngine;

namespace BalloonParty.Projectile.Model
{
    public interface IProjectileModel
    {
        IReadOnlyReactiveProperty<string> ColorName { get; }
        IReadOnlyReactiveProperty<int> ShieldsRemaining { get; }
        Vector3 Direction { get; }
        float Speed { get; }
        bool IsFree { get; }
        IBalloonModel LastHitBalloon { get; }
    }
}
