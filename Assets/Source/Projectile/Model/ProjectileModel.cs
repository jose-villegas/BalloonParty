using BalloonParty.Balloon.Model;
using UniRx;
using UnityEngine;

namespace BalloonParty.Projectile.Model
{
    public class ProjectileModel : IWriteableProjectileModel
    {
        public ReactiveProperty<string> ColorName { get; } = new(null);
        public ReactiveProperty<int> ShieldsRemaining { get; } = new(0);

        public Vector3 Direction { get; set; }
        public float Speed { get; set; }
        public bool IsFree { get; set; }
        public int ColorPopCount { get; set; }
        public IBalloonModel LastHitBalloon { get; set; }

        IReadOnlyReactiveProperty<string> IProjectileModel.ColorName => ColorName;
        IReadOnlyReactiveProperty<int> IProjectileModel.ShieldsRemaining => ShieldsRemaining;
    }
}
