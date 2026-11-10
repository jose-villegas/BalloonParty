using System;
using TMPro;
using UnityEngine;

namespace BalloonParty.Configuration.HeadsUp
{
    /// <summary>
    ///     How one kind of heads-up looks, so each kind reads at a glance: its colour, its size against the
    ///     line's own font size, how much it grows as its count escalates (a longer chain, more pickups) and
    ///     its weight. Words inside the text can carry their own rich-text tags on top (an italic verb).
    /// </summary>
    /// <remarks>
    ///     Growth is logarithmic and capped: equal ratios read as equal steps (Weber–Fechner), so doubling the
    ///     count adds the same visible step, and a long chain tops out instead of filling the screen.
    /// </remarks>
    [Serializable]
    public struct HeadsUpStyle
    {
        [SerializeField] private Color _color;

        [Tooltip("Font size against the line's own at a count of 1: 1 = as built.")]
        [SerializeField] [Min(0.1f)] private float _size;

        [Tooltip("Growth per doubling of the count: size × (1 + growth × log2(count)). 0 = the same every time.")]
        [SerializeField] [Min(0f)] private float _growth;

        [Tooltip("The largest it grows to, against the line's own font size.")]
        [SerializeField] [Min(0.1f)] private float _maxSize;

        [SerializeField] private FontStyles _fontStyle;

        public Color Color => _color;
        public FontStyles FontStyle => _fontStyle;

        public HeadsUpStyle(Color color, float size, float growth, float maxSize, FontStyles fontStyle)
        {
            _color = color;
            _size = size;
            _growth = growth;
            _maxSize = maxSize;
            _fontStyle = fontStyle;
        }

        /// <summary>The size against the line's own for an escalating <paramref name="count" /> (1 = the first).</summary>
        public float SizeAt(int count)
        {
            return HeadsUpSizing.SizeAt(_size, _growth, _maxSize, count);
        }
    }

    /// <summary>The escalation maths behind <see cref="HeadsUpStyle.SizeAt" />, kept free of TMP types.</summary>
    internal static class HeadsUpSizing
    {
        /// <summary>
        ///     <paramref name="size" /> × (1 + <paramref name="growth" /> × log2(<paramref name="count" />)), capped at
        ///     <paramref name="maxSize" /> (never below <paramref name="size" />); a count below 1 counts as 1.
        /// </summary>
        internal static float SizeAt(float size, float growth, float maxSize, int count)
        {
            var grown = size * (1f + growth * Mathf.Log(Mathf.Max(1, count), 2f));
            return Mathf.Min(Mathf.Max(size, maxSize), grown);
        }
    }
}
