using UnityEngine;

namespace BalloonParty.Balloon.Type
{
    /// <summary>
    ///     The SoapBubbleCluster shader's per-instance properties and the drift every soap bubble shares: the cluster
    ///     (<see cref="SoapBubbleClusterVariant" />) and the foam coat round a balloon
    ///     (<see cref="BalloonParty.Balloon.View.FoamCoatOverlay" />) draw with the same material, so a coat floats and
    ///     spins as a cluster does.
    /// </summary>
    internal static class SoapBubbleShader
    {
        /// <summary>The float speed a soap bubble drifts at unless its prefab sets its own.</summary>
        internal const float DefaultFloatSpeed = 0.8f;

        /// <summary>The shader's own <c>_BubbleRadius</c> default, for a material that does not set one.</summary>
        internal const float DefaultBubbleRadius = 0.17f;

        private const float MinSpinDegrees = 5f;
        private const float MaxSpinDegrees = 12f;

        // How far the farthest bubble's centre sits from the quad's centre in the shader's layout for each count
        // (kLayout1 to kLayout6), in quad widths.
        private static readonly float[] LayoutCentreReach = { 0f, 0.110f, 0.127f, 0.1556f, 0.187f, 0.190f };

        internal static readonly int BubbleCountId = Shader.PropertyToID("_BubbleCount");
        internal static readonly int BubbleRadiusId = Shader.PropertyToID("_BubbleRadius");
        internal static readonly int SpriteScaleId = Shader.PropertyToID("_SpriteScale");
        internal static readonly int TimeOffsetId = Shader.PropertyToID("_TimeOffset");
        internal static readonly int FloatSpeedId = Shader.PropertyToID("_FloatSpeed");
        internal static readonly int RotationId = Shader.PropertyToID("_Rotation");
        internal static readonly int RotationSpeedId = Shader.PropertyToID("_RotationSpeed");
        internal static readonly int FlushId = Shader.PropertyToID("_Flush");

        /// <summary>
        ///     The <c>_TimeOffset</c> that keeps a bubble's float where it is when its speed changes at
        ///     <paramref name="time" /> (the shader's <c>_Time.y</c>). The shader floats on
        ///     <c>_Time.y * _FloatSpeed + _TimeOffset</c>, so a new speed alone would jump the bubbles to another phase.
        /// </summary>
        internal static float RebaseTimeOffset(float time, float offset, float fromSpeed, float toSpeed)
        {
            return offset + (time * (fromSpeed - toSpeed));
        }

        /// <summary>
        ///     Writes a lone bubble's look into <paramref name="block" />: one bubble at the quad's centre, at the default
        ///     float speed, from a random point in its float and a random angle and spin, so lone bubbles drawn side by
        ///     side do not move in step.
        /// </summary>
        internal static void SetLoneBubble(MaterialPropertyBlock block)
        {
            block.SetFloat(BubbleCountId, 1f);
            block.SetFloat(SpriteScaleId, 1f);
            block.SetFloat(TimeOffsetId, Random.value * 100f);
            block.SetFloat(FloatSpeedId, DefaultFloatSpeed);
            block.SetFloat(RotationId, RandomRotation());
            block.SetFloat(RotationSpeedId, RandomSpinRadians());
        }

        /// <summary>
        ///     The uniform scale at which a lone bubble drawn with <paramref name="material" /> on
        ///     <paramref name="sprite" /> has a world radius of <paramref name="radius" />. The shader draws it at
        ///     <c>_BubbleRadius</c> quad widths (at a <c>_SpriteScale</c> of 1), so the scale follows from the two.
        /// </summary>
        internal static float ScaleForRadius(Material material, Sprite sprite, float radius)
        {
            return radius / Mathf.Max(1e-4f, BubbleRadiusOf(material) * sprite.bounds.size.x);
        }

        /// <summary>
        ///     How far the shader's layout for <paramref name="count" /> bubbles reaches from the quad's centre to the
        ///     outer edge of its farthest bubble, in bubble radii (<paramref name="bubbleRadius" />, in quad widths): 1
        ///     for a lone bubble, about 2.1 for the ring of six at the default radius. Counts outside 1 to 6 read as the
        ///     nearest; the per-bubble size variance is left out.
        /// </summary>
        internal static float LayoutReach(int count, float bubbleRadius)
        {
            var radius = Mathf.Max(1e-4f, bubbleRadius);
            var index = Mathf.Clamp(count, 1, LayoutCentreReach.Length) - 1;
            return (LayoutCentreReach[index] + radius) / radius;
        }

        /// <summary>The bubble radius <paramref name="material" /> draws with, in quad widths.</summary>
        internal static float BubbleRadiusOf(Material material)
        {
            return material != null && material.HasProperty(BubbleRadiusId)
                ? material.GetFloat(BubbleRadiusId)
                : DefaultBubbleRadius;
        }

        /// <summary>A random spin for one bubble, in radians a second, either way round.</summary>
        internal static float RandomSpinRadians()
        {
            var spin = Random.Range(MinSpinDegrees, MaxSpinDegrees) * Mathf.Deg2Rad;
            return Random.value < 0.5f ? spin : -spin;
        }

        /// <summary>A random starting angle, in radians.</summary>
        internal static float RandomRotation()
        {
            return Random.Range(0f, Mathf.PI * 2f);
        }
    }
}
