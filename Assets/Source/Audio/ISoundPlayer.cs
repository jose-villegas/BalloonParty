using UnityEngine;

namespace BalloonParty.Audio
{
    internal interface ISoundPlayer
    {
        SoundHandle Play(GameSoundId id, Vector3? position);
        void Stop(SoundHandle handle);
    }
}
