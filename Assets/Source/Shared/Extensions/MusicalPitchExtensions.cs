using UnityEngine;

namespace BalloonParty.Shared.Extensions
{
    /// <summary>Equal-temperament pitch math: a semitone offset as an <c>AudioSource.pitch</c> multiplier.</summary>
    internal static class MusicalPitchExtensions
    {
        internal const int WholeToneSemitones = 2;
        internal const int TritoneSemitones = 6;

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
