using UnityEngine;

namespace BalloonParty.Audio
{
    internal interface ISoundPlayer
    {
        SoundHandle Play(GameSoundId id, Vector3? position, int? melodicStreak = null);

        // Ramps a playing voice's volume to lerp(VolumeRange, factor) over the entry's FadeInSeconds.
        // For continuously-driven loops (e.g. speed-tracking wind); no-op on a stale/ended handle.
        void SetVolumeFactor(SoundHandle handle, float factor);
        void Stop(SoundHandle handle);
    }
}
