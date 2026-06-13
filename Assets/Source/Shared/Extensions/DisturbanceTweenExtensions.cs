using BalloonParty.Configuration;
using BalloonParty.Shared.Disturbance;
using DG.Tweening;
using UnityEngine;

namespace BalloonParty.Shared.Extensions
{
    internal static class DisturbanceTweenExtensions
    {
        internal static T StampDisturbanceAlongPath<T>(
            this T tween, Transform target, DisturbanceFieldService field, StampSource source)
            where T : Tween
        {
            var profile = field.GetProfile(source);
            var lastPos = target.position;
            tween.OnUpdate(() => lastPos = StampStep(target, field, profile, lastPos));
            return tween;
        }

        private static Vector3 StampStep(
            Transform target, DisturbanceFieldService field, StampProfile profile, Vector3 lastPos)
        {
            var pos = target.position;
            var delta = pos - lastPos;
            var dir = new Vector2(delta.x, delta.y).normalized;
            var scale = target.localScale.x * target.localScale.x;
            field.Stamp(pos, profile.Radius * scale, profile.Strength * scale, dir, profile.Duration);
            return pos;
        }
    }
}
