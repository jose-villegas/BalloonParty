using UnityEngine;

namespace BalloonParty.Shared.Extensions
{
    internal static class ColorExtensions
    {
        internal static Color WithAlpha(this Color c, float alpha)
        {
            return new Color(c.r, c.g, c.b, alpha);
        }
    }
}

