using UnityEngine;

namespace BalloonParty.Display
{
    /// <summary>
    ///     One frame's stamps for the clearing mask, as the shader takes them: each a capsule in mask UV (<c>xy</c>
    ///     the start, <c>zw</c> the end), its radius in view heights, and how far it has faded (0 a full dome, 1 gone).
    /// </summary>
    internal sealed class EffectsClearingStamps
    {
        internal readonly Vector4[] Capsules = new Vector4[EffectsClearingStamper.MaxStamps];
        internal readonly float[] Radii = new float[EffectsClearingStamper.MaxStamps];
        internal readonly float[] Fades = new float[EffectsClearingStamper.MaxStamps];
    }
}
