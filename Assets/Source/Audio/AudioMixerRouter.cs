using BalloonParty.Audio.Configuration;
using BalloonParty.Shared.Diagnostics;
using UnityEngine.Audio;
using VContainer;

namespace BalloonParty.Audio
{
    // Adapter over an AudioMixer asset — the one non-View type allowed to touch UnityEngine.Audio.
    // Degrades to the Null router's behaviour (master output, no-op duck) when the mixer/params are
    // unassigned, so it drops in and activates ducking the moment the asset is authored.
    internal sealed class AudioMixerRouter : IAudioMixerRouter
    {
        private const float UnduckedVolumeDb = 0f;

        private readonly IAudioMixerSettings _settings;

        [Inject]
        public AudioMixerRouter(IAudioMixerSettings settings)
        {
            _settings = settings;
        }

        public AudioMixerGroup GroupFor(SfxChannel channel)
        {
            return _settings.GroupFor(channel);
        }

        public void SetChannelDucked(SfxChannel channel, bool ducked)
        {
            var mixer = _settings.Mixer;
            if (mixer == null)
            {
                return;
            }

            var param = _settings.ExposedVolumeParamFor(channel);
            if (string.IsNullOrEmpty(param))
            {
                return;
            }

            // SetFloat runs on its own line (not inside Log.Assert) so the release-stripped
            // assert can't strip the duck itself. It returns false for a mistyped exposed param.
            var applied = mixer.SetFloat(param, ducked ? _settings.DuckVolumeDb : UnduckedVolumeDb);
            Log.Assert(applied, "Audio", $"AudioMixer has no exposed param '{param}' for channel {channel}.");
        }
    }
}
