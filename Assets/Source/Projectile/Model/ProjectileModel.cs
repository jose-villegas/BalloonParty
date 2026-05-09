using BalloonParty.Balloon.Model;
using UniRx;
using UnityEngine;

namespace BalloonParty.Projectile.Model
{
    public class ProjectileModel
    {
        public string ColorName;
        public int ColorPopCount;
        public Vector3 Direction;
        public bool IsFree;
        public BalloonModel LastHitBalloon;
        public float Speed;
        public ReactiveProperty<int> ShieldsRemaining { get; } = new(0);
    }
}