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

        // Wall bounces since the last balloon contact — the cruise detector's counter.
        int ConsecutiveWallBounces { get; set; }

        // Shield count captured at cruise entry: the ramp's denominator, so speed climbs from base
        // toward the max as the remaining shields spend down from this snapshot.
        int CruiseStartShields { get; set; }

        // Seconds since the last cruise speed change (entry or bounce) — drives the per-tap
        // freeze-then-pickup animation envelope.
        float CruiseTapElapsed { get; set; }
        new Vector3 Direction { get; set; }
        new float Speed { get; set; }
        new bool IsFree { get; set; }
        new IBalloonModel LastHitBalloon { get; set; }

        void AddBuff(ProjectileBuff buff);
        void RemoveBuff(ProjectileBuff buff);
    }
}
