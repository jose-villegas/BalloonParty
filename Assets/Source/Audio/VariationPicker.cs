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
        private readonly int[] _lastClipIndex;
        private readonly int[] _sequencePosition;
        private readonly bool[] _sequenceForward;

        public VariationPicker(System.Random rng, IReadOnlyList<int> melodicScale, int melodicRootSemitone)
        {
            _rng = rng;
            _scale = melodicScale;
            _root = melodicRootSemitone;
            _lastClipIndex = new int[SoundIds.Count];
            _sequencePosition = new int[SoundIds.Count];
            _sequenceForward = new bool[SoundIds.Count];
            Reset();
        }

        public VoicePlayback Pick(GameSoundId id, SfxEntry entry, in PickContext ctx)
        {
            var clip = entry.Clips[SelectClipIndex(id, entry)];

            float pitch;
            var melodicSemitone = 0;
            switch (entry.MelodicMode)
            {
                case MelodicMode.ScaleWalkUp when _scale.Count > 0:
                    melodicSemitone = _root + ResolveWalkOffset(ctx.Streak, entry.MelodicMaxOctaves, entry.MelodicSkipSteps);
                    pitch = melodicSemitone.SemitonesToPitchMultiplier();
                    break;
                case MelodicMode.ScaleWalkDown when _scale.Count > 0:
                    melodicSemitone = _root - ResolveWalkOffset(ctx.Streak, entry.MelodicMaxOctaves, entry.MelodicSkipSteps);
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
                _sequencePosition[i] = -1;
                _sequenceForward[i] = true;
            }
        }

        private int ResolveWalkOffset(int streak, int maxOctaves, int skipSteps)
        {
            // Magnitude (>= 0) of a net-climbing yoyo: each cycle ascends one scale octave then dips back
            // down, advancing skipSteps scale steps per cycle so it trends further from the root (the
            // reward) without the jarring octave jump a hard reset gives. skipSteps == 0 loops within one
            // octave (no net drift); skipSteps == the scale length removes the dip entirely (a plain
            // ramp). The drift is ceilinged by maxOctaves so a runaway streak can't squeak away. A step
            // back down the pentatonic is a whole tone/third, so every dip stays tonal. ScaleWalkUp adds
            // this above the root; ScaleWalkDown mirrors it below.
            var degree = Mathf.Max(0, streak);
            var steps = _scale.Count;
            var up = steps;
            var skip = Mathf.Clamp(skipSteps, 0, up);
            var down = up - skip;
            var cycleLen = Mathf.Max(1, up + down);
            var windowTop = steps * Mathf.Max(1, maxOctaves) - 1;
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

            switch (entry.ClipPickMode)
            {
                case ClipPickMode.Incremental:
                    return AdvanceSequence(ordinal, count, forward: true, entry.ClipWrapMode);
                case ClipPickMode.Decrease:
                    return AdvanceSequence(ordinal, count, forward: false, entry.ClipWrapMode);
                case ClipPickMode.Unison:
                case ClipPickMode.Random:
                default:
                    return SelectRandom(ordinal, count);
            }
        }

        // Unison mode: returns the clip index for a specific layer (0..clipCount-1).
        internal int SelectClipForLayer(int layer, SfxEntry entry)
        {
            return Mathf.Clamp(layer, 0, entry.Clips.Count - 1);
        }

        private int SelectRandom(int ordinal, int count)
        {
            var last = _lastClipIndex[ordinal];
            var index = _rng.Next(last >= 0 ? count - 1 : count);
            if (last >= 0 && index >= last)
            {
                index++;
            }

            _lastClipIndex[ordinal] = index;
            return index;
        }

        private int AdvanceSequence(int ordinal, int count, bool forward, ClipWrapMode wrapMode)
        {
            var pos = _sequencePosition[ordinal];
            var isForward = _sequenceForward[ordinal];

            // First call: initialize position based on direction.
            if (pos < 0)
            {
                pos = forward ? 0 : count - 1;
                isForward = true;
            }

            // On Decrease mode, invert the direction.
            if (!forward)
            {
                isForward = !isForward;
            }

            var result = Mathf.Clamp(pos, 0, count - 1);

            // Advance for next call.
            var next = isForward ? pos + 1 : pos - 1;

            switch (wrapMode)
            {
                case ClipWrapMode.Loop:
                    next = ((next % count) + count) % count;
                    break;
                case ClipWrapMode.Clamp:
                    next = Mathf.Clamp(next, 0, count - 1);
                    break;
                case ClipWrapMode.PingPong:
                    if (next >= count)
                    {
                        next = count - 2;
                        isForward = !isForward;
                    }
                    else if (next < 0)
                    {
                        next = 1;
                        isForward = !isForward;
                    }

                    break;
            }

            _sequencePosition[ordinal] = next;
            // Store the canonical direction (undo the Decrease inversion for storage).
            _sequenceForward[ordinal] = forward ? isForward : !isForward;
            return result;
        }

        private float RandomRange(Vector2 range)
        {
            return Mathf.Lerp(range.x, range.y, (float)_rng.NextDouble());
        }
    }
}
