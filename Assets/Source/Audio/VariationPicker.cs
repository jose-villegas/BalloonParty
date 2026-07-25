using System.Collections.Generic;
using BalloonParty.Audio.Configuration;
using BalloonParty.Shared.Extensions;
using UnityEngine;

namespace BalloonParty.Audio
{
    internal sealed class VariationPicker
    {
        private const float BurstSpreadSemitones = 0.7f;
        private const float BurstVolumeFalloff = 0.25f;

        private readonly System.Random _rng;
        private readonly IReadOnlyList<int> _scale;
        private readonly int _root;
        private readonly int _maxOctaves;
        private readonly int _skipSteps;
        private readonly int[] _lastClipIndex;

        public VariationPicker(System.Random rng, IReadOnlyList<int> melodicScale, int melodicRootSemitone,
            int melodicMaxOctaves = 2, int melodicSkipSteps = 1)
        {
            _rng = rng;
            _scale = melodicScale;
            _root = melodicRootSemitone;
            _maxOctaves = melodicMaxOctaves;
            _skipSteps = melodicSkipSteps;
            _lastClipIndex = new int[SoundIds.Count];
            Reset();
        }

        public VoicePlayback Pick(GameSoundId id, SfxEntry entry, in PickContext ctx)
        {
            var clip = entry.Clips[SelectClipIndex(id, entry)];

            float pitch;
            var melodicSemitone = 0;
            switch (entry.MelodicMode)
            {
                case MelodicMode.ScaleWalk when _scale.Count > 0:
                    melodicSemitone = ResolveScaleWalkSemitone(ctx.Streak);
                    pitch = melodicSemitone.SemitonesToPitchMultiplier();
                    break;
                case MelodicMode.ScaleWalkCapped when _scale.Count > 0:
                    melodicSemitone = _root + ResolveCappedScaleWalkOffset(ctx.Streak);
                    pitch = melodicSemitone.SemitonesToPitchMultiplier();
                    break;
                case MelodicMode.ScaleWalkCappedDown when _scale.Count > 0:
                    melodicSemitone = _root - ResolveCappedScaleWalkOffset(ctx.Streak);
                    pitch = melodicSemitone.SemitonesToPitchMultiplier();
                    break;
                case MelodicMode.Tension:
                    melodicSemitone = ctx.CurrentSemitone + entry.TensionSemitones;
                    pitch = melodicSemitone.SemitonesToPitchMultiplier();
                    break;
                default:
                    pitch = RandomRange(entry.PitchRange);
                    break;
            }

            var volume = RandomRange(entry.VolumeRange);

            if (ctx.BurstIndex > 0)
            {
                pitch *= (ctx.BurstIndex * BurstSpreadSemitones).SemitonesToPitchMultiplier();
                volume *= 1f / (1f + ctx.BurstIndex * BurstVolumeFalloff);
            }

            var pan = entry.Pan2D ? ctx.NormalizedPan : 0f;
            return new VoicePlayback(clip, pitch, volume, pan, melodicSemitone);
        }

        public void Reset()
        {
            for (var i = 0; i < _lastClipIndex.Length; i++)
            {
                _lastClipIndex[i] = -1;
            }
        }

        private int ResolveScaleWalkSemitone(int streak)
        {
            // Unbounded climb: each streak step is the next scale degree, rolling up one octave per lap.
            var degree = Mathf.Max(0, streak);
            var steps = _scale.Count;
            return _root + _scale[degree % steps] + 12 * (degree / steps);
        }

        private int ResolveCappedScaleWalkOffset(int streak)
        {
            // Magnitude (>= 0) of a net-climbing yoyo: each cycle ascends one scale octave then dips back
            // down, advancing _skipSteps scale steps per cycle so it trends further from the root (the
            // reward) without the jarring octave jump a hard reset gives. _skipSteps == 0 loops within one
            // octave (no net drift); _skipSteps == the scale length removes the dip entirely (a plain
            // ramp). The drift is ceilinged by _maxOctaves so a runaway streak can't squeak away. A step
            // back down the pentatonic is a whole tone/third, so every dip stays tonal. ScaleWalkCapped
            // adds this above the root; ScaleWalkCappedDown mirrors it below.
            var degree = Mathf.Max(0, streak);
            var steps = _scale.Count;
            var up = steps;
            var skip = Mathf.Clamp(_skipSteps, 0, up);
            var down = up - skip;
            var cycleLen = Mathf.Max(1, up + down);
            var windowTop = steps * Mathf.Max(1, _maxOctaves) - 1;
            var driftCap = Mathf.Max(0, windowTop - up);

            var baseStep = Mathf.Min(degree / cycleLen * skip, driftCap);
            var phase = degree % cycleLen;
            var rise = phase <= up ? phase : up - (phase - up);
            var position = Mathf.Clamp(baseStep + rise, 0, windowTop);
            return _scale[position % steps] + 12 * (position / steps);
        }

        private int SelectClipIndex(GameSoundId id, SfxEntry entry)
        {
            var count = entry.Clips.Count;
            if (count <= 1)
            {
                return 0;
            }

            var ordinal = (int)id;
            var last = _lastClipIndex[ordinal];

            // Pick uniformly from the count-1 clips other than the last one played (no
            // immediate repeat). On the first play (last < 0) the full range is fair game.
            var index = _rng.Next(last >= 0 ? count - 1 : count);
            if (last >= 0 && index >= last)
            {
                index++;
            }

            _lastClipIndex[ordinal] = index;
            return index;
        }

        private float RandomRange(Vector2 range)
        {
            return Mathf.Lerp(range.x, range.y, (float)_rng.NextDouble());
        }
    }
}
