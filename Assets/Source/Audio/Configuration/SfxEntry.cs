using System;
using System.Collections.Generic;
using BalloonParty.Audio;
using UnityEngine;

namespace BalloonParty.Audio.Configuration
{
    [Serializable]
    internal class SfxEntry
    {
        [SerializeField] private SfxChannel _channel = SfxChannel.Gameplay;
        [SerializeField] private AudioClip[] _clips = Array.Empty<AudioClip>();

        [Tooltip("How to select from the clip list. Random = no-repeat random, Unison = all clips " +
                 "layered simultaneously, Incremental = walk forward, Decrease = walk backward.")]
        [SerializeField] private ClipPickMode _clipPickMode = ClipPickMode.Random;

        [Tooltip("What happens when Incremental/Decrease reaches the boundary. Loop = wrap, Clamp = " +
                 "stay on last/first, PingPong = reverse direction. Ignored for Random/Unison.")]
        [SerializeField] private ClipWrapMode _clipWrapMode = ClipWrapMode.Loop;

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

        [Tooltip("Loop only: while a voice for this sound is already playing, ignore new Play calls (keep " +
                 "the one instance) instead of starting or restarting another. Play returns the live handle.")]
        [SerializeField] private bool _singleInstance;

        [Tooltip("Derive a subtle stereo pan from world-X. spatialBlend stays 0 (no rolloff).")]
        [SerializeField] private bool _pan2D = true;

        [Tooltip("Seconds between the Play call and the sound actually starting (DSP-scheduled, ignores " +
                 "timeScale). 0 = immediate. The voice is claimed up front, so a Stop during the delay " +
                 "cancels it cleanly.")]
        [SerializeField] [Min(0f)] private float _delaySeconds;

        [Tooltip("Seconds to ramp volume from 0 up to the target at play start. 0 = full volume at once. " +
                 "Applies to one-shots and loops.")]
        [SerializeField] [Min(0f)] private float _fadeInSeconds;

        [Tooltip("Seconds to ramp volume down to 0 when this sound is stopped (via Stop or another " +
                 "entry's Stops On Play). 0 = cut instantly. Scope resets always cut instantly.")]
        [SerializeField] [Min(0f)] private float _fadeOutSeconds;

        [Tooltip("Maximum seconds the voice plays before auto-triggering fadeout. 0 = play the full " +
                 "clip (default). Useful for clips with long tails you want to cut short.")]
        [SerializeField] [Min(0f)] private float _maxPlaySeconds;

        [Tooltip("When this sound plays, stop any active voices of these ids (each fading out per its own " +
                 "FadeOutSeconds). e.g. a resolve cue silencing a still-playing loop.")]
        [SerializeField] private GameSoundId[] _stopsOnPlay = Array.Empty<GameSoundId>();

        [Tooltip("None = plain variation. ScaleWalkUp = streak-driven net-climbing yoyo (rise a scale " +
                 "octave, dip MelodicSkipSteps back, repeat) ceilinged at MelodicMaxOctaves. ScaleWalkDown = " +
                 "the same yoyo mirrored below the root (dips down first, then works up). Tension = fixed " +
                 "dissonant offset against the current pop key.")]
        [SerializeField] private MelodicMode _melodicMode = MelodicMode.None;

        [Tooltip("Octaves the walk spans before it stops drifting. Bounds the pitch so a long " +
                 "streak can't run away into a squeak; keep it low (1-2). Only used by ScaleWalkUp/Down.")]
        [SerializeField] [Min(1)] private int _melodicMaxOctaves = 2;

        [Tooltip("Net climb per yoyo cycle, in scale steps. Each cycle rises one scale octave then dips " +
                 "back, advancing this many steps. 0 = loop within one octave (no net climb); equal to the " +
                 "scale length = plain climb, no dip. 1-2 stays tonal. Only used by ScaleWalkUp/Down.")]
        [SerializeField] [Min(0)] private int _melodicSkipSteps = 1;

        [Tooltip("Semitone offset against the current pop degree when MelodicMode = Tension. " +
                 "e.g. deflect = +1 (minor-2nd rub), wall hit = -2 (dropped-it step).")]
        [SerializeField] private int _tensionSemitones;

        [Tooltip("Additional layers fired simultaneously with this entry. Each layer is a flat entry " +
                 "with its own clips, pitch, volume, and pick mode (no recursive nesting).")]
        [SerializeField] private SfxLayerEntry[] _layers = Array.Empty<SfxLayerEntry>();

        public SfxChannel Channel => _channel;
        public IReadOnlyList<AudioClip> Clips => _clips;
        public ClipPickMode ClipPickMode => _clipPickMode;
        public ClipWrapMode ClipWrapMode => _clipWrapMode;
        public Vector2 PitchRange => _pitchRange;
        public Vector2 VolumeRange => _volumeRange;
        public float CooldownSeconds => _cooldownSeconds;
        public int MaxConcurrentVoices => _maxConcurrentVoices;
        public int Priority => _priority;
        public bool Loop => _loop;
        public bool SingleInstance => _singleInstance;
        public bool Pan2D => _pan2D;
        public float DelaySeconds => _delaySeconds;
        public float FadeInSeconds => _fadeInSeconds;
        public float FadeOutSeconds => _fadeOutSeconds;
        public float MaxPlaySeconds => _maxPlaySeconds;
        public IReadOnlyList<GameSoundId> StopsOnPlay => _stopsOnPlay;
        public MelodicMode MelodicMode => _melodicMode;
        public int MelodicMaxOctaves => _melodicMaxOctaves;
        public int MelodicSkipSteps => _melodicSkipSteps;
        public int TensionSemitones => _tensionSemitones;
        public IReadOnlyList<SfxLayerEntry> Layers => _layers;
        public bool HasClips => _clips is { Length: > 0 };

#if UNITY_EDITOR
        [Tooltip("Editor-only: description handed to an ISfxProvider to auto-fill empty clip slots (Phase 3). Never read at runtime.")]
        [HideInInspector]
        [SerializeField] [TextArea] private string _fetchPrompt;

        internal string FetchPrompt => _fetchPrompt;
#endif
    }

    // Serialized by ordinal on SfxEntry — reorder only while nothing authored depends on the old values.
    internal enum MelodicMode
    {
        None,
        ScaleWalkUp,
        ScaleWalkDown,
        Tension
    }
}
