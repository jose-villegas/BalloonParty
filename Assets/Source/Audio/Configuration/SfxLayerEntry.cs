using System;
using System.Collections.Generic;
using BalloonParty.Audio;
using UnityEngine;

namespace BalloonParty.Audio.Configuration
{
    /// <summary>
    ///     A flattened layer entry without its own nested layers, breaking the recursive serialization
    ///     that <see cref="SfxEntry"/> would create if layers were the same type.
    /// </summary>
    [Serializable]
    internal class SfxLayerEntry
    {
        [SerializeField] private AudioClip[] _clips = Array.Empty<AudioClip>();
        [SerializeField] private ClipPickMode _clipPickMode = ClipPickMode.Random;
        [SerializeField] private ClipWrapMode _clipWrapMode = ClipWrapMode.Loop;
        [SerializeField] private Vector2 _pitchRange = Vector2.one;
        [SerializeField] private Vector2 _volumeRange = Vector2.one;
        [SerializeField] private MelodicMode _melodicMode = MelodicMode.None;
        [SerializeField] [Min(1)] private int _melodicMaxOctaves = 2;
        [SerializeField] [Min(0)] private int _melodicSkipSteps = 1;
        [SerializeField] private int _tensionSemitones;

        public IReadOnlyList<AudioClip> Clips => _clips;
        public ClipPickMode ClipPickMode => _clipPickMode;
        public ClipWrapMode ClipWrapMode => _clipWrapMode;
        public Vector2 PitchRange => _pitchRange;
        public Vector2 VolumeRange => _volumeRange;
        public MelodicMode MelodicMode => _melodicMode;
        public int MelodicMaxOctaves => _melodicMaxOctaves;
        public int MelodicSkipSteps => _melodicSkipSteps;
        public int TensionSemitones => _tensionSemitones;
        public bool HasClips => _clips is { Length: > 0 };
    }
}
