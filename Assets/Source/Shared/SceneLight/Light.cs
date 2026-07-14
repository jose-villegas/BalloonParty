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
        internal const float DefaultEdgeSoftness = 0.35f;

        // World-space centre of the lit disc.
        internal readonly ReactiveProperty<Vector3> Position;
        // World-space radius of the lit disc.
        internal readonly ReactiveProperty<float> Radius;
        // Peak magnitude added into the field's R at the centre, before the accumulate cap.
        internal readonly ReactiveProperty<float> Intensity;
        // Palette colour index tagged into the field's A channel; -1 = no colour (use the key light).
        internal readonly ReactiveProperty<int> PaletteIndex;
        // Fraction of the radius held at full magnitude before the edge falls off; 0 = soft to centre.
        // Authored once — it doesn't animate, so it isn't reactive.
        internal readonly float EdgeSoftness;

        internal Light(Vector3 position, float radius = DefaultRadius, float intensity = DefaultIntensity,
            int paletteIndex = -1, float edgeSoftness = DefaultEdgeSoftness)
        {
            Position = new ReactiveProperty<Vector3>(position);
            Radius = new ReactiveProperty<float>(radius);
            Intensity = new ReactiveProperty<float>(intensity);
            PaletteIndex = new ReactiveProperty<int>(paletteIndex);
            EdgeSoftness = edgeSoftness;
        }
    }
}
