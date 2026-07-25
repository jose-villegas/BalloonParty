using System;
using BalloonParty.Shared;
using UnityEngine;
using UnityEngine.Audio;

namespace BalloonParty.Audio.Configuration
{
    [CreateAssetMenu(menuName = "Configuration/Audio Mixer Settings", fileName = "AudioMixerSettings")]
    internal class AudioMixerSettings : ScriptableObject, IAudioMixerSettings
    {
        [Tooltip("The AudioMixer whose exposed volume params get ducked. Unassigned → routing/ducking no-op (master output).")]
        [SerializeField] private AudioMixer _mixer;

        [Tooltip("One output group per SfxChannel, indexed by ordinal. Unassigned slot → that channel routes to master.")]
        [EnumIndexed(typeof(SfxChannel))]
        [SerializeField] private AudioMixerGroup[] _groups;

        [Tooltip("Exposed mixer volume parameter per SfxChannel, used to duck. Empty slot → that channel cannot duck.")]
        [EnumIndexed(typeof(SfxChannel))]
        [SerializeField] private string[] _exposedVolumeParams;

        [Tooltip("Volume (dB) a ducked channel drops to while ducked. 0 = unchanged, -80 = muted.")]
        [SerializeField] [Range(-80f, 0f)] private float _duckVolumeDb = -12f;

        public AudioMixer Mixer => _mixer;
        public float DuckVolumeDb => _duckVolumeDb;

        private void OnValidate()
        {
#if UNITY_EDITOR
            // Self-heals arrays saved before a new SfxChannel was appended.
            var count = Enum.GetValues(typeof(SfxChannel)).Length;
            if (_groups == null || _groups.Length != count)
            {
                Array.Resize(ref _groups, count);
            }

            if (_exposedVolumeParams == null || _exposedVolumeParams.Length != count)
            {
                Array.Resize(ref _exposedVolumeParams, count);
            }
#endif
        }

        public AudioMixerGroup GroupFor(SfxChannel channel)
        {
            var index = (int)channel;
            if (_groups != null && index >= 0 && index < _groups.Length)
            {
                return _groups[index];
            }

            return null;
        }

        public string ExposedVolumeParamFor(SfxChannel channel)
        {
            var index = (int)channel;
            if (_exposedVolumeParams != null && index >= 0 && index < _exposedVolumeParams.Length)
            {
                return _exposedVolumeParams[index];
            }

            return null;
        }
    }
}
