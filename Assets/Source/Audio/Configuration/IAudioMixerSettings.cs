using UnityEngine.Audio;

namespace BalloonParty.Audio.Configuration
{
    internal interface IAudioMixerSettings
    {
        AudioMixer Mixer { get; }
        float DuckVolumeDb { get; }
        AudioMixerGroup GroupFor(SfxChannel channel);
        string ExposedVolumeParamFor(SfxChannel channel);
    }
}
