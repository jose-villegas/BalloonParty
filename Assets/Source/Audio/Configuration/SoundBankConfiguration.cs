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
        [Tooltip("Positive scale as note offsets from the root. Pentatonic (no adjacent semitones) so pops never clash.")]
        [MusicalNote]
        [SerializeField] private int[] _melodicScale = { 0, 2, 4, 7, 9 };

        [Tooltip("Root note applied to every degree (transposes the key).")]
        [MusicalNote]
        [SerializeField] private int _melodicRootSemitone;

        [Tooltip("Ceiling for the ScaleWalkCapped climb, in octaves. Bounds the pitch so a long streak " +
                 "can't run away into a squeak; keep it low (1-2). Only affects ScaleWalkCapped.")]
        [SerializeField] [Min(1)] private int _melodicMaxOctaves = 2;

        [Tooltip("ScaleWalkCapped net climb per yoyo cycle, in scale steps. Each cycle rises one scale " +
                 "octave then dips back down, advancing this many steps. 0 = loop within one octave (no " +
                 "net climb); equal to the scale length = plain climb with no dip. 1-2 keeps it tonal.")]
        [SerializeField] [Min(0)] private int _melodicSkipSteps = 1;

        [Header("Voices")]
        [Tooltip("Global concurrent-voice cap, and the pooled-voice prewarm count. Keep under Android's real-voice budget.")]
        [SerializeField] [Min(1)] private int _globalVoiceCap = 16;

        public IReadOnlyList<int> MelodicScale => _melodicScale;
        public int MelodicRootSemitone => _melodicRootSemitone;
        public int MelodicMaxOctaves => _melodicMaxOctaves;
        public int MelodicSkipSteps => _melodicSkipSteps;
        public int GlobalVoiceCap => _globalVoiceCap;

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
    }
}
