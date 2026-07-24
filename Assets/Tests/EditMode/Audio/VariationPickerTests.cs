using System.Collections.Generic;
using System.Reflection;
using BalloonParty.Audio;
using BalloonParty.Audio.Configuration;
using BalloonParty.Shared.Extensions;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Audio
{
    [TestFixture]
    public class VariationPickerTests
    {
        private static readonly int[] PentatonicScale = { 0, 2, 4, 7, 9 };

        private readonly List<AudioClip> _clips = new List<AudioClip>();

        [TearDown]
        public void TearDown()
        {
            foreach (var clip in _clips)
            {
                Object.DestroyImmediate(clip);
            }

            _clips.Clear();
        }

        [Test]
        public void Pick_PlainMode_PitchAndVolumeLandWithinConfiguredRanges()
        {
            var pitchRange = new Vector2(0.8f, 1.2f);
            var volumeRange = new Vector2(0.5f, 1f);
            var entry = CreateEntry(pitchRange, volumeRange, new[] { CreateClip() });
            var picker = new VariationPicker(new System.Random(1), PentatonicScale, melodicRootSemitone: 0);
            var ctx = new PickContext(streak: 0, currentSemitone: 0, burstIndex: 0, normalizedPan: 0f);

            for (var i = 0; i < 300; i++)
            {
                var playback = picker.Pick(GameSoundId.BalloonPop, entry, ctx);

                Assert.GreaterOrEqual(playback.Pitch, pitchRange.x);
                Assert.LessOrEqual(playback.Pitch, pitchRange.y);
                Assert.GreaterOrEqual(playback.Volume, volumeRange.x);
                Assert.LessOrEqual(playback.Volume, volumeRange.y);
            }
        }

        [Test]
        public void Pick_ScaleWalkMode_StreakZero_ReturnsRootPitch()
        {
            var entry = CreateEntry(Vector2.one, Vector2.one, new[] { CreateClip() }, MelodicMode.ScaleWalk);
            var picker = new VariationPicker(new System.Random(1), PentatonicScale, melodicRootSemitone: 0);
            var ctx = new PickContext(streak: 0, currentSemitone: 0, burstIndex: 0, normalizedPan: 0f);

            var playback = picker.Pick(GameSoundId.BalloonPop, entry, ctx);

            Assert.AreEqual(0, playback.MelodicSemitone);
            Assert.AreEqual((0).SemitonesToPitchMultiplier(), playback.Pitch, 0.0001f);
        }

        [Test]
        public void Pick_ScaleWalkMode_StreakRollsOverOctave_DoublesPitch()
        {
            // 5 scale degrees; streak 5 wraps back to degree 0 one octave up (root + 12).
            var entry = CreateEntry(Vector2.one, Vector2.one, new[] { CreateClip() }, MelodicMode.ScaleWalk);
            var picker = new VariationPicker(new System.Random(1), PentatonicScale, melodicRootSemitone: 0);
            var ctx = new PickContext(streak: 5, currentSemitone: 0, burstIndex: 0, normalizedPan: 0f);

            var playback = picker.Pick(GameSoundId.BalloonPop, entry, ctx);

            Assert.AreEqual(12, playback.MelodicSemitone);
            Assert.AreEqual(2f, playback.Pitch, 0.0001f);
        }

        [Test]
        public void Pick_ScaleWalkMode_MidStreak_MapsToScaleDegree()
        {
            // Degree 2 of {0,2,4,7,9} is 4 semitones, no octave rollover.
            var entry = CreateEntry(Vector2.one, Vector2.one, new[] { CreateClip() }, MelodicMode.ScaleWalk);
            var picker = new VariationPicker(new System.Random(1), PentatonicScale, melodicRootSemitone: 0);
            var ctx = new PickContext(streak: 2, currentSemitone: 0, burstIndex: 0, normalizedPan: 0f);

            var playback = picker.Pick(GameSoundId.BalloonPop, entry, ctx);

            Assert.AreEqual(4, playback.MelodicSemitone);
            Assert.AreEqual((4).SemitonesToPitchMultiplier(), playback.Pitch, 0.0001f);
        }

        [Test]
        public void Pick_ScaleWalkMode_EmptyScale_FallsBackToPlainPitchWithoutThrowing()
        {
            // Guards ResolveScaleWalkSemitone's degree % scale.Count from a divide-by-zero if a
            // sound is authored as ScaleWalk without a configured scale.
            var entry = CreateEntry(Vector2.one, Vector2.one, new[] { CreateClip() }, MelodicMode.ScaleWalk);
            var picker = new VariationPicker(new System.Random(1), System.Array.Empty<int>(), melodicRootSemitone: 0);
            var ctx = new PickContext(streak: 5, currentSemitone: 0, burstIndex: 0, normalizedPan: 0f);

            VoicePlayback playback = default;
            Assert.DoesNotThrow(() => playback = picker.Pick(GameSoundId.BalloonPop, entry, ctx));
            Assert.AreEqual(0, playback.MelodicSemitone);
            Assert.AreEqual(1f, playback.Pitch, 0.0001f);
        }

        [Test]
        public void Pick_TensionMode_AddsTensionSemitonesToCurrentSemitone()
        {
            var entry = CreateEntry(Vector2.one, Vector2.one, new[] { CreateClip() }, MelodicMode.Tension, tensionSemitones: 3);
            var picker = new VariationPicker(new System.Random(1), PentatonicScale, melodicRootSemitone: 0);
            var ctx = new PickContext(streak: 0, currentSemitone: 5, burstIndex: 0, normalizedPan: 0f);

            var playback = picker.Pick(GameSoundId.BalloonPop, entry, ctx);

            Assert.AreEqual(8, playback.MelodicSemitone);
            Assert.AreEqual((8).SemitonesToPitchMultiplier(), playback.Pitch, 0.0001f);
        }

        [Test]
        public void Pick_BurstIndexGreaterThanZero_IncreasesPitchAndReducesVolume()
        {
            var entry = CreateEntry(Vector2.one, Vector2.one, new[] { CreateClip() });
            var picker = new VariationPicker(new System.Random(1), PentatonicScale, melodicRootSemitone: 0);
            var noBurst = new PickContext(streak: 0, currentSemitone: 0, burstIndex: 0, normalizedPan: 0f);
            var burst = new PickContext(streak: 0, currentSemitone: 0, burstIndex: 3, normalizedPan: 0f);

            var basePlayback = picker.Pick(GameSoundId.BalloonPop, entry, noBurst);
            var burstPlayback = picker.Pick(GameSoundId.BalloonPop, entry, burst);

            Assert.Greater(burstPlayback.Pitch, basePlayback.Pitch);
            Assert.Less(burstPlayback.Volume, basePlayback.Volume);
        }

        [Test]
        public void Pick_MultiClipEntry_NeverRepeatsSameClipConsecutively()
        {
            var clips = new[] { CreateClip(), CreateClip(), CreateClip() };
            var entry = CreateEntry(Vector2.one, Vector2.one, clips);
            var picker = new VariationPicker(new System.Random(7), PentatonicScale, melodicRootSemitone: 0);
            var ctx = new PickContext(streak: 0, currentSemitone: 0, burstIndex: 0, normalizedPan: 0f);

            AudioClip previous = null;
            for (var i = 0; i < 200; i++)
            {
                var playback = picker.Pick(GameSoundId.BalloonPop, entry, ctx);

                if (previous != null)
                {
                    Assert.AreNotSame(previous, playback.Clip);
                }

                previous = playback.Clip;
            }
        }

        [Test]
        public void Pick_Pan2DFalse_PanIsZeroRegardlessOfContext()
        {
            var entry = CreateEntry(Vector2.one, Vector2.one, new[] { CreateClip() }, pan2D: false);
            var picker = new VariationPicker(new System.Random(1), PentatonicScale, melodicRootSemitone: 0);
            var ctx = new PickContext(streak: 0, currentSemitone: 0, burstIndex: 0, normalizedPan: 0.75f);

            var playback = picker.Pick(GameSoundId.BalloonPop, entry, ctx);

            Assert.AreEqual(0f, playback.Pan);
        }

        [Test]
        public void Pick_Pan2DTrue_PanEqualsNormalizedPan()
        {
            var entry = CreateEntry(Vector2.one, Vector2.one, new[] { CreateClip() }, pan2D: true);
            var picker = new VariationPicker(new System.Random(1), PentatonicScale, melodicRootSemitone: 0);
            var ctx = new PickContext(streak: 0, currentSemitone: 0, burstIndex: 0, normalizedPan: 0.75f);

            var playback = picker.Pick(GameSoundId.BalloonPop, entry, ctx);

            Assert.AreEqual(0.75f, playback.Pan);
        }

        private AudioClip CreateClip()
        {
            var clip = AudioClip.Create($"clip{_clips.Count}", 1, 1, 44100, false);
            _clips.Add(clip);
            return clip;
        }

        private static SfxEntry CreateEntry(
            Vector2 pitchRange,
            Vector2 volumeRange,
            AudioClip[] clips,
            MelodicMode melodicMode = MelodicMode.None,
            int tensionSemitones = 0,
            bool pan2D = true)
        {
            var entry = new SfxEntry();
            SetField(entry, "_pitchRange", pitchRange);
            SetField(entry, "_volumeRange", volumeRange);
            SetField(entry, "_clips", clips);
            SetField(entry, "_melodicMode", melodicMode);
            SetField(entry, "_tensionSemitones", tensionSemitones);
            SetField(entry, "_pan2D", pan2D);
            return entry;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            target.GetType()
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(target, value);
        }
    }
}
