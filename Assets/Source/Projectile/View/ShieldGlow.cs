using UnityEngine;

namespace BalloonParty.Projectile.View
{
    /// <summary>
    ///     How far past white the shield field's colour reaches for a shield count: 1 (the palette colour) for
    ///     one shield or none, then <c>perShield</c> more for each further shield, up to <c>max</c>.
    /// </summary>
    internal static class ShieldGlow
    {
        internal static float For(int shieldCount, float perShield, float max)
        {
            var glow = 1f + Mathf.Max(shieldCount - 1, 0) * Mathf.Max(perShield, 0f);
            return Mathf.Clamp(glow, 1f, Mathf.Max(max, 1f));
        }
    }
}
