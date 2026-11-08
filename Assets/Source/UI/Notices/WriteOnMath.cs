using UnityEngine;

namespace BalloonParty.UI.Notices
{
    /// <summary>
    ///     The reveal of <see cref="WriteOnText" />, in characters: the write head moves from 0 to
    ///     <see cref="EndHead" />, and each glyph corner is revealed as the head passes its position.
    /// </summary>
    internal static class WriteOnMath
    {
        /// <summary>
        ///     Where a glyph corner sits along the line. <paramref name="right" /> picks the glyph's right
        ///     edge; a top corner sits <paramref name="slant" /> earlier, so the reveal edge leans forward
        ///     like a pencil stroke.
        /// </summary>
        internal static float CornerPosition(int index, bool right, bool top, float slant)
        {
            return index + (right ? 1f : 0f) - (top ? slant : 0f);
        }

        /// <summary>
        ///     How revealed a corner at <paramref name="position" /> is with the head at
        ///     <paramref name="head" />: 0 until the head reaches it, 1 once the head is
        ///     <paramref name="softness" /> characters past it.
        /// </summary>
        internal static float RevealAlpha(float head, float position, float softness)
        {
            if (softness <= 0f)
            {
                return head >= position ? 1f : 0f;
            }

            return Mathf.Clamp01((head - position) / softness);
        }

        /// <summary>
        ///     Where the head starts so that it reaches the first character after <paramref name="delaySeconds" />:
        ///     behind 0 by the delay, in characters.
        /// </summary>
        internal static float StartHead(float delaySeconds, float secondsPerCharacter)
        {
            return secondsPerCharacter > 0f ? -Mathf.Max(0f, delaySeconds) / secondsPerCharacter : 0f;
        }

        /// <summary>The head position at which every corner of <paramref name="characterCount" /> characters is fully revealed.</summary>
        internal static float EndHead(int characterCount, float softness)
        {
            return Mathf.Max(0, characterCount) + Mathf.Max(0f, softness);
        }
    }
}
