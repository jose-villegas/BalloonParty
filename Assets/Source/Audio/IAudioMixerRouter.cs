using BalloonParty.Audio.Configuration;
using UnityEngine.Audio;

namespace BalloonParty.Audio
{
    internal interface IAudioMixerRouter
    {
        AudioMixerGroup GroupFor(SfxChannel channel);
        void SetChannelDucked(SfxChannel channel, bool ducked);
    }
}
