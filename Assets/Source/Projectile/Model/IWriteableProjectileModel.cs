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

        // Wall bounces since the last balloon contact — the cruise detector's counter.
        int ConsecutiveWallBounces { get; set; }

        // Shield count captured at cruise entry: the ramp's denominator, so speed climbs from base
        // toward the max as the remaining shields spend down from this snapshot.
        int CruiseStartShields { get; set; }

        // Seconds since the last cruise speed change (entry or bounce) — drives the per-tap
        // freeze-then-pickup animation envelope.
        float CruiseTapElapsed { get; set; }

        // Multiplies the cruise speed, decaying as a PIERCING shot plows through tough (>1-hit)
        // actors — halved per such pierce, floored so total speed never drops below base, reset to 1
        // when a wall bounce ends the cruise. 1 = no decay.
        float CruisePierceSpeedScale { get; set; }

        // World position where the current flight segment began (last reflect/deflect, or the
        // muzzle) — the origin the last-shield ease traverses from.
        Vector3 SegmentStartPosition { get; set; }

        // Seconds elapsed on the current segment — the last-shield ease normalizes to it so the
        // doomed drift takes a fixed wall-clock time regardless of the segment's length.
        float SegmentElapsed { get; set; }
        new Vector3 Direction { get; set; }
        new float Speed { get; set; }
        new bool IsFree { get; set; }
        new IBalloonModel LastHitBalloon { get; set; }

        void AddBuff(ProjectileBuff buff);
        void RemoveBuff(ProjectileBuff buff);
    }
}
