using UnityEngine;

namespace BalloonParty.Audio
{
    internal readonly struct VoicePlayback
    {
        public readonly AudioClip Clip;
        public readonly float Pitch;
        public readonly float Volume;
        public readonly float Pan;
        public readonly int MelodicSemitone;

        public VoicePlayback(AudioClip clip, float pitch, float volume, float pan, int melodicSemitone)
        {
            Clip = clip;
            Pitch = pitch;
            Volume = volume;
            Pan = pan;
            MelodicSemitone = melodicSemitone;
        }
    }
}
