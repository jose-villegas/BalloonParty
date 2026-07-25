using UnityEngine;

namespace BalloonParty.Shared
{
    /// <summary>
    ///     Renders an <c>int</c> semitone field as a musical-note dropdown (C, C#, D, …) instead of a
    ///     raw number. Values outside [<see cref="MinSemitone" />, <see cref="MaxSemitone" />] fall back
    ///     to a plain int field so nothing is clamped away.
    /// </summary>
    public class MusicalNoteAttribute : PropertyAttribute
    {
        public readonly int MinSemitone;
        public readonly int MaxSemitone;

        public MusicalNoteAttribute(int minSemitone = 0, int maxSemitone = 24)
        {
            MinSemitone = minSemitone;
            MaxSemitone = maxSemitone;
        }
    }
}
