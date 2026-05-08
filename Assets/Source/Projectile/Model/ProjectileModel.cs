using UniRx;
using UnityEngine;
using BalloonParty.Balloon.Model;

namespace BalloonParty.Projectile.Model
{
    public class ProjectileModel
    {
        public Vector3 Direction;
        public float Speed;
        public ReactiveProperty<int> ShieldsRemaining { get; } = new(0);
        public bool IsFree;

        public string ColorName;
        public int ColorPopCount;
        public BalloonModel LastHitBalloon;
    }
}

