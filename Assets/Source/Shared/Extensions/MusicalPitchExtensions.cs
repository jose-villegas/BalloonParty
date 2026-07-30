using System;
using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Shared.Extensions
{
    /// <summary>Equal-temperament pitch math: a semitone offset as an <c>AudioSource.pitch</c> multiplier.</summary>
    internal static class MusicalPitchExtensions
    {
        internal const int WholeToneSemitones = 2;
        internal const int TritoneSemitones = 6;

        /// <summary>Major-chord root semitone offsets used for per-colour pitch colouring (I, III, V, VII).</summary>
        internal static readonly int[] ColorRootSemitones = { 0, 4, 7, 11 };

        /// <summary>
        ///     Returns the semitone offset for a colour's root note, looked up by position in the
        ///     palette's progress-colour list. Colours beyond the table repeat the last entry.
        /// </summary>
        internal static int ColorRootOffset(IReadOnlyList<string> progressColorNames, string color)
        {
            for (var i = 0; i < progressColorNames.Count; i++)
            {
                if (string.Equals(progressColorNames[i], color, StringComparison.Ordinal))
                {
                    return i < ColorRootSemitones.Length
                        ? ColorRootSemitones[i]
                        : ColorRootSemitones[ColorRootSemitones.Length - 1];
                }
            }

            return 0;
        }

        internal static float SemitonesToPitchMultiplier(this float semitones)
        {
            return Mathf.Pow(2f, semitones / 12f);
        }

        internal static float SemitonesToPitchMultiplier(this int semitones)
        {
            return ((float)semitones).SemitonesToPitchMultiplier();
        }

        /// <summary>
        ///     Pitch multiplier for a tritone interval shifted by octaves. +1 = first tritone up (6st),
        ///     +2 = same tritone an octave higher (6+12 = 18st), -1 = first tritone down (-6st),
        ///     -2 = same tritone an octave lower (-6-12 = -18st), etc.
        /// </summary>
        internal static float Tritone(int steps)
        {
            var sign = steps >= 0 ? 1 : -1;
            var octaves = (Mathf.Abs(steps) - 1) * 12;
            var semitones = sign * (TritoneSemitones + octaves);
            return semitones.SemitonesToPitchMultiplier();
        }
    }
}
