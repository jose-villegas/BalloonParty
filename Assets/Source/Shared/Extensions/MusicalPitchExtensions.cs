using UnityEngine;

namespace BalloonParty.Shared.Extensions
{
    /// <summary>Equal-temperament pitch math: a semitone offset as an <c>AudioSource.pitch</c> multiplier.</summary>
    internal static class MusicalPitchExtensions
    {
        internal static float SemitonesToPitchMultiplier(this float semitones)
        {
            return Mathf.Pow(2f, semitones / 12f);
        }

        internal static float SemitonesToPitchMultiplier(this int semitones)
        {
            return ((float)semitones).SemitonesToPitchMultiplier();
        }
    }
}
