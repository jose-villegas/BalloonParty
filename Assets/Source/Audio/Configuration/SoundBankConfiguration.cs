using System;
using System.Collections.Generic;
using BalloonParty.Audio;
using BalloonParty.Shared;
using UnityEngine;

namespace BalloonParty.Audio.Configuration
{
    [CreateAssetMenu(menuName = "Configuration/Sound Bank Configuration", fileName = "SoundBankConfiguration")]
    internal class SoundBankConfiguration : ScriptableObject, ISoundBankConfiguration
    {
        [Tooltip("One entry per GameSoundId, indexed by ordinal. An entry with no clips is a silent no-op.")]
        [EnumIndexed(typeof(GameSoundId))]
        [SerializeField] private SfxEntry[] _entries;

        [Header("Melodic pops")]
        [Tooltip("Positive scale as semitone offsets from the root. Pentatonic (no adjacent semitones) so pops never clash.")]
        [SerializeField] private int[] _melodicScale = { 0, 2, 4, 7, 9 };

        [Tooltip("Root semitone offset applied to every degree (transposes the key).")]
        [SerializeField] private int _melodicRootSemitone;

        public IReadOnlyList<int> MelodicScale => _melodicScale;
        public int MelodicRootSemitone => _melodicRootSemitone;

        public bool TryGet(GameSoundId id, out SfxEntry entry)
        {
            var index = (int)id;
            if (_entries != null && index >= 0 && index < _entries.Length)
            {
                var candidate = _entries[index];
                if (candidate != null && candidate.HasClips)
                {
                    entry = candidate;
                    return true;
                }
            }

            entry = null;
            return false;
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            // Self-heals an asset saved before a new GameSoundId was appended.
            var count = Enum.GetValues(typeof(GameSoundId)).Length;
            if (_entries == null || _entries.Length != count)
            {
                Array.Resize(ref _entries, count);
            }
#endif
        }
    }
}
