using System;
using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Audio.Configuration
{
    [Serializable]
    internal class SfxEntry
    {
        [SerializeField] private SfxChannel _channel = SfxChannel.Gameplay;
        [SerializeField] private AudioClip[] _clips = Array.Empty<AudioClip>();

        [Tooltip("Random pitch multiplier range (x = min, y = max). 1..1 = no variation. Ignored when MelodicMode is not None.")]
        [SerializeField] private Vector2 _pitchRange = Vector2.one;

        [Tooltip("Random linear volume range (x = min, y = max), 0..1.")]
        [SerializeField] private Vector2 _volumeRange = Vector2.one;

        [Tooltip("Wall-clock seconds before this id may retrigger. 0 = no cooldown.")]
        [SerializeField] [Min(0f)] private float _cooldownSeconds;

        [Tooltip("Max concurrent voices for this id; further requests steal/drop by priority.")]
        [SerializeField] [Min(1)] private int _maxConcurrentVoices = 4;

        [Tooltip("Higher = more important. Stingers (LevelUp/GameOver) sit high so pop spam can't starve them.")]
        [SerializeField] [Range(0, 256)] private int _priority = 128;

        [Tooltip("Sustained loop (cruise). Play returns a SoundHandle the caller must Stop.")]
        [SerializeField] private bool _loop;

        [Tooltip("Derive a subtle stereo pan from world-X. spatialBlend stays 0 (no rolloff).")]
        [SerializeField] private bool _pan2D = true;

        [Tooltip("None = plain variation. ScaleWalk = streak-driven pentatonic degree (simple-balloon pop). " +
                 "Tension = fixed dissonant offset against the current pop key (deflect rub / wall-hit drop).")]
        [SerializeField] private MelodicMode _melodicMode = MelodicMode.None;

        [Tooltip("Semitone offset against the current pop degree when MelodicMode = Tension. " +
                 "e.g. deflect = +1 (minor-2nd rub), wall hit = -2 (dropped-it step).")]
        [SerializeField] private int _tensionSemitones;

        public SfxChannel Channel => _channel;
        public IReadOnlyList<AudioClip> Clips => _clips;
        public Vector2 PitchRange => _pitchRange;
        public Vector2 VolumeRange => _volumeRange;
        public float CooldownSeconds => _cooldownSeconds;
        public int MaxConcurrentVoices => _maxConcurrentVoices;
        public int Priority => _priority;
        public bool Loop => _loop;
        public bool Pan2D => _pan2D;
        public MelodicMode MelodicMode => _melodicMode;
        public int TensionSemitones => _tensionSemitones;
        public bool HasClips => _clips is { Length: > 0 };

#if UNITY_EDITOR
        [Tooltip("Editor-only: description handed to an ISfxProvider to auto-fill empty clip slots (Phase 3). Never read at runtime.")]
        [SerializeField] [TextArea] private string _fetchPrompt;

        internal string FetchPrompt => _fetchPrompt;
#endif
    }

    // Append only — serialized by ordinal on SfxEntry. Never reorder or insert.
    internal enum MelodicMode
    {
        None,
        ScaleWalk,
        Tension
    }
}
