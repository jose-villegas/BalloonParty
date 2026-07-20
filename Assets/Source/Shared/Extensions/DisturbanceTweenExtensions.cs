using BalloonParty.Configuration;
using BalloonParty.Shared.Disturbance;
using DG.Tweening;
using UnityEngine;
using BalloonParty.Configuration.Effects;

namespace BalloonParty.Shared.Extensions
{
    internal static class DisturbanceTweenExtensions
    {
        internal static T StampDisturbanceAlongPath<T>(
            this T tween, Transform target, DisturbanceFieldService field, StampSource source)
            where T : Tween
        {
            var profile = field.GetProfile(source);
            var lastStampPos = target.position;
            tween.OnUpdate(() => lastStampPos = StampStep(target, field, profile, lastStampPos));
            return tween;
        }

        // The pure gate (seam for tests): true once travel since the last stamp clears one step, handing back
        // the new anchor (the current point) and the heading. Frame-rate independent — N sub-step frames stamp
        // the same as one step of equal total travel. A frame that covers several steps still stamps once and
        // snaps the anchor forward (no back-fill), which only under-stamps if a target outruns `spacing` in a
        // frame — not the case for the gentle spawn/balance paths this serves.
        internal static bool TryGateStamp(
            Vector3 currentPos, Vector3 lastStampPos, float spacing, out Vector3 newAnchor, out Vector2 direction)
        {
            var delta = currentPos - lastStampPos;

            if (spacing <= 0f || delta.sqrMagnitude < spacing * spacing)
            {
                newAnchor = lastStampPos;
                direction = Vector2.zero;
                return false;
            }

            newAnchor = currentPos;
            direction = new Vector2(delta.x, delta.y).normalized;
            return true;
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

            if (!TryGateStamp(pos, lastStampPos, spacing, out var anchor, out var dir))
            {
                return anchor;
            }

            field.Stamp(pos, profile.Radius * scale, profile.Strength * scale, dir, profile.Duration);
            return anchor;
        }
    }
}
