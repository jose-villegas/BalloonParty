using UnityEngine;

namespace BalloonParty.Audio
{
    internal interface ISoundPlayer
    {
        SoundHandle Play(GameSoundId id, Vector3? position, int? melodicStreak = null, int semitoneOffset = 0, float volumeScale = 1f);

        // Ramps a playing voice's volume to lerp(VolumeRange, factor) over the entry's FadeInSeconds.
        // For continuously-driven loops (e.g. speed-tracking wind); no-op on a stale/ended handle.
        void SetVolumeFactor(SoundHandle handle, float factor);

        /// <summary>Lerps the pitch multiplier on a playing voice (1 = normal). No-op on stale handles.</summary>
        void SetPitch(SoundHandle handle, float pitch, float seconds = 0f);

        void Stop(SoundHandle handle);
    }
}
