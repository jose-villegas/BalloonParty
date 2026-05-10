#region

using BalloonParty.Balloon.Model;
using UniRx;
using UnityEngine;

#endregion

namespace BalloonParty.Projectile.Model
{
    public class ProjectileModel
    {
        public int ColorPopCount;
        public Vector3 Direction;
        public bool IsFree;
        public BalloonModel LastHitBalloon;
        public float Speed;
        public ReactiveProperty<string> ColorName { get; } = new(null);
        public ReactiveProperty<int> ShieldsRemaining { get; } = new(0);
    }
}
