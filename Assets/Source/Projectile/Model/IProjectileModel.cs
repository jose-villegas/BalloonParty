#region

using BalloonParty.Balloon.Model;
using UniRx;
using UnityEngine;

#endregion

namespace BalloonParty.Projectile.Model
{
    public interface IProjectileModel
    {
        IReadOnlyReactiveProperty<string> ColorName { get; }
        IReadOnlyReactiveProperty<int> ShieldsRemaining { get; }
        Vector3 Direction { get; }
        float Speed { get; }
        bool IsFree { get; }
        int ColorPopCount { get; }
        IBalloonModel LastHitBalloon { get; }
    }
}

