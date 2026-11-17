using UnityEngine;

namespace BalloonParty.UI.Score
{
    /// <summary>Where a score shape is this frame and how far its pens have come, for <see cref="ShapeInkView" />.</summary>
    internal readonly struct ShapeInkFrame
    {
        internal readonly Vector3 Center;
        internal readonly Quaternion Rotation;

        // World units per unit of the shape's own space: the fitted radius times the travel curve's scale.
        internal readonly float Radius;

        // How far every pen has moved along its walk since the launch, in the shape's own units.
        internal readonly float Travelled;

        // The ink's length as a share of the gap between two pens.
        internal readonly float Coverage;

        // 1 while the shape flies; falls to 0 as a snapped shape fades.
        internal readonly float Fade;

        internal readonly float WaveAmplitude;
        internal readonly float WaveClock;

        internal ShapeInkFrame(
            Vector3 center, Quaternion rotation, float radius, float travelled, float coverage, float fade,
            float waveAmplitude, float waveClock)
        {
            Center = center;
            Rotation = rotation;
            Radius = radius;
            Travelled = travelled;
            Coverage = coverage;
            Fade = fade;
            WaveAmplitude = waveAmplitude;
            WaveClock = waveClock;
        }
    }
}
