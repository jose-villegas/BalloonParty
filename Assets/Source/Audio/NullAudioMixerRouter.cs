using BalloonParty.Audio.Configuration;
using UnityEngine.Audio;

namespace BalloonParty.Audio
{
    // Stand-in until Step 6 wires the real AudioMixer. A null group routes voices to the
    // mixer's default output, so audio plays; ducking is a no-op.
    internal sealed class NullAudioMixerRouter : IAudioMixerRouter
    {
        public AudioMixerGroup GroupFor(SfxChannel channel)
        {
            return null;
        }

        public void SetChannelDucked(SfxChannel channel, bool ducked)
        {
        }
    }
}
