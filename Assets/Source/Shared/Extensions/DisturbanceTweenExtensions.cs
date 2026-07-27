using BalloonParty.Configuration;
using BalloonParty.Shared.Disturbance;
using DG.Tweening;
using UnityEngine;
using BalloonParty.Configuration.Effects;

namespace BalloonParty.Shared.Extensions
{
    internal static class DisturbanceTweenExtensions
    {
        private const int MaxStepsPerFrame = 8;

        internal static T StampDisturbanceAlongPath<T>(
            this T tween, Transform target, DisturbanceFieldService field, StampSource source)
            where T : Tween
        {
            var profile = field.GetProfile(source);
            var lastStampPos = target.position;
            tween.OnUpdate(() => lastStampPos = StampStep(target, field, profile, lastStampPos));
            return tween;
        }

        // The pure gate (seam for tests): how many whole steps of travel have accrued since the last stamp,
        // with the anchor advanced by exactly that many — NOT snapped to the current point. Snapping left
        // consecutive deposits as far apart as the frame happened to travel, so an eased path scattered a
        // sparse trail while it was fast and laid a solid one only once it slowed. At one step per radius the
        // stamp falloffs sum to a flat ridge, but only if every step is actually placed; an isolated deposit
        // is a steep lone bump, and the speck field reads that gradient at high gain.
        // Frame-rate independent: N sub-step frames deposit the same as one frame of equal total travel.
        internal static int GateStampSteps(
            Vector3 currentPos, Vector3 lastStampPos, float spacing, out Vector3 newAnchor, out Vector2 direction)
        {
            var delta = currentPos - lastStampPos;

            if (spacing <= 0f || delta.sqrMagnitude < spacing * spacing)
            {
                newAnchor = lastStampPos;
                direction = Vector2.zero;
                return 0;
            }

            direction = new Vector2(delta.x, delta.y).normalized;

            // Capped so a teleporting or very fast target can't monopolise the shared per-frame stamp batch;
            // the leftover distance is dropped rather than carried, which only shows on motion this gentle
            // path stamping never sees.
            var steps = Mathf.Min(Mathf.FloorToInt(delta.magnitude / spacing), MaxStepsPerFrame);
            newAnchor = lastStampPos + delta.normalized * (spacing * steps);
            return steps;
        }

        // Distance-gated, not per-frame: OnUpdate fires every rendered frame, so stamping each call scaled the
        // wake with frame rate (~2x too much at 120Hz). Gating on distance travelled ties it to the path. A hop
        // shorter than one step leaves no wake — deliberate: a barely-moving target shouldn't stir the field.
        private static Vector3 StampStep(
            Transform target, DisturbanceFieldService field, StampProfile profile, Vector3 lastStampPos)
        {
            var pos = target.position;
            var scale = target.localScale.x * target.localScale.x;
            var spacing = (profile.Spacing > 0f ? profile.Spacing : profile.Radius) * scale;

            var steps = GateStampSteps(pos, lastStampPos, spacing, out var anchor, out var dir);

            if (steps <= 0)
            {
                return anchor;
            }

            var step = (anchor - lastStampPos) / steps;

            for (var i = 1; i <= steps; i++)
            {
                field.Stamp(
                    lastStampPos + step * i, profile.Radius * scale, profile.Strength * scale, dir,
                    profile.Duration);
            }

            return anchor;
        }
    }
}
