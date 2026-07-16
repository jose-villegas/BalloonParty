using UniRx;
using UnityEngine;

namespace BalloonParty.Shared.SceneLight
{
    /// <summary>
    ///     One light a caller owns and toggles on/off in <see cref="SceneLightFieldService"/> (see @ref
    ///     plan_lighting "Milestone 3"). Lights are STATE, not events: register it to turn it on, dispose
    ///     the registration to turn it off, and mutate these reactive properties live — the field watches
    ///     them and only re-renders when one changes. There is no decay of its own; a fade is just the
    ///     caller animating <see cref="Intensity"/> (the field's R magnitude).
    /// </summary>
    internal sealed class Light
    {
        internal const float DefaultRadius = 1.5f;
        internal const float DefaultIntensity = 2f;
        internal const float DefaultFalloffPower = 2f;

        // World-space start of the lit region. For a point light this is the centre; for a segment/area
        // light (e.g. a laser beam) it's one end — see <see cref="EndPosition"/>.
        internal readonly ReactiveProperty<Vector3> Position;
        // World-space far end of the lit region. Equal to <see cref="Position"/> for a point light; set it
        // apart to make a capsule (segment) light — a long rectangle with the falloff decaying from the
        // axis out to <see cref="Radius"/> on each side.
        internal readonly ReactiveProperty<Vector3> EndPosition;
        // Perpendicular reach: the disc radius for a point light, the beam half-width for a segment.
        // For a segment this is the half-width at <see cref="Position"/>; see <see cref="EndRadius"/>.
        internal readonly ReactiveProperty<float> Radius;
        // Half-width at <see cref="EndPosition"/> — set it apart from <see cref="Radius"/> to taper the
        // beam (wide→thin or any combo); the width lerps along the axis. Equal = uniform capsule.
        internal readonly ReactiveProperty<float> EndRadius;
        // Peak magnitude added into the field's R along the axis, before the accumulate cap.
        internal readonly ReactiveProperty<float> Intensity;
        // Radial falloff exponent (1 - dist/radius)^power: 1 = linear cone, higher = tighter, longer tail.
        internal readonly ReactiveProperty<float> FalloffPower;
        // Palette colour index tagged into the field's A channel; -1 = no colour (use the key light).
        internal readonly ReactiveProperty<int> PaletteIndex;

        internal Light(Vector3 position, float radius = DefaultRadius, float intensity = DefaultIntensity,
            int paletteIndex = -1, float falloffPower = DefaultFalloffPower)
        {
            Position = new ReactiveProperty<Vector3>(position);
            EndPosition = new ReactiveProperty<Vector3>(position);
            Radius = new ReactiveProperty<float>(radius);
            EndRadius = new ReactiveProperty<float>(radius);
            Intensity = new ReactiveProperty<float>(intensity);
            FalloffPower = new ReactiveProperty<float>(falloffPower);
            PaletteIndex = new ReactiveProperty<int>(paletteIndex);
        }

        /// <summary>A capsule (segment) light between two world points — a beam with <paramref name="radius"/>
        /// half-width, the intensity decaying from the axis out to the sides. Pass <paramref name="endRadius"/>
        /// to taper the beam from <paramref name="radius"/> at the start to it at the end.</summary>
        internal static Light Segment(Vector3 start, Vector3 end, float radius = DefaultRadius,
            float intensity = DefaultIntensity, int paletteIndex = -1, float falloffPower = DefaultFalloffPower,
            float? endRadius = null)
        {
            var light = new Light(start, radius, intensity, paletteIndex, falloffPower);
            light.EndPosition.Value = end;
            light.EndRadius.Value = endRadius ?? radius;
            return light;
        }
    }
}
