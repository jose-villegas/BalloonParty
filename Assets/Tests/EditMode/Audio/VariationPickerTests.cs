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
        public void Pick_ScaleWalkUp_StreakZero_ReturnsRootPitch()
        {
            var entry = CreateEntry(Vector2.one, Vector2.one, new[] { CreateClip() }, MelodicMode.ScaleWalkUp);
            var picker = new VariationPicker(new System.Random(1), PentatonicScale, melodicRootSemitone: 0);
            var ctx = new PickContext(streak: 0, currentSemitone: 0, burstIndex: 0, normalizedPan: 0f);

            var playback = picker.Pick(GameSoundId.BalloonPop, entry, ctx);

            Assert.AreEqual(0, playback.MelodicSemitone);
            Assert.AreEqual((0).SemitonesToPitchMultiplier(), playback.Pitch, 0.0001f);
        }

        [Test]
        public void Pick_ScaleWalkUp_FirstAscentPeaksAnOctaveUp_DoublesPitch()
        {
            // The first ascent tops out one octave up: streak 5 on a 5-note scale = root + 12.
            var entry = CreateEntry(Vector2.one, Vector2.one, new[] { CreateClip() }, MelodicMode.ScaleWalkUp);
            var picker = new VariationPicker(new System.Random(1), PentatonicScale, melodicRootSemitone: 0);
            var ctx = new PickContext(streak: 5, currentSemitone: 0, burstIndex: 0, normalizedPan: 0f);

            var playback = picker.Pick(GameSoundId.BalloonPop, entry, ctx);

            Assert.AreEqual(12, playback.MelodicSemitone);
            Assert.AreEqual(2f, playback.Pitch, 0.0001f);
        }

        [Test]
        public void Pick_ScaleWalkUp_ZeroSkip_LoopsWithinOneOctave()
        {
            // skipSteps 0 = down equals up, so each cycle climbs a full scale octave then falls all the
            // way back: a symmetric yoyo with no net drift. Streak 5 peaks one octave up (semitone 12);
            // streak 10 is back at the root; nothing ever leaves the [root, +1 octave] band.
            var entry = CreateEntry(Vector2.one, Vector2.one, new[] { CreateClip() }, MelodicMode.ScaleWalkUp,
                melodicMaxOctaves: 2, melodicSkipSteps: 0);
            var picker = new VariationPicker(new System.Random(1), PentatonicScale, melodicRootSemitone: 0);

            int Pick(int streak) => picker.Pick(GameSoundId.BalloonPop, entry,
                new PickContext(streak, currentSemitone: 0, burstIndex: 0, normalizedPan: 0f)).MelodicSemitone;

            Assert.AreEqual(12, Pick(5));
            Assert.AreEqual(0, Pick(10));
            for (var streak = 0; streak < 200; streak++)
            {
                Assert.That(Pick(streak), Is.InRange(0, 12));
            }
        }

        [Test]
        public void Pick_ScaleWalkUp_DefaultSkip_NetClimbsAcrossCycles()
        {
            // skipSteps 1 advances one scale step per yoyo cycle, so later cycles sit higher than earlier
            // ones. First peak (streak 5) is one octave = 12; the next cycle's peak (streak 14) is a step
            // higher = 14; and the trough rises too (streak 9 = 2, up from the streak-0 root).
            var entry = CreateEntry(Vector2.one, Vector2.one, new[] { CreateClip() }, MelodicMode.ScaleWalkUp,
                melodicMaxOctaves: 2, melodicSkipSteps: 1);
            var picker = new VariationPicker(new System.Random(1), PentatonicScale, melodicRootSemitone: 0);

            int Pick(int streak) => picker.Pick(GameSoundId.BalloonPop, entry,
                new PickContext(streak, currentSemitone: 0, burstIndex: 0, normalizedPan: 0f)).MelodicSemitone;

            Assert.AreEqual(0, Pick(0));
            Assert.AreEqual(12, Pick(5));
            Assert.AreEqual(2, Pick(9));
            Assert.AreEqual(14, Pick(14));
        }

        [Test]
        public void Pick_ScaleWalkUp_FullSkip_ClimbsWithoutDipping()
        {
            // skipSteps equal to the scale length = no descent leg, so the walk only ever climbs (until
            // the ceiling) — a plain ramp. Across the first window it never steps down.
            var entry = CreateEntry(Vector2.one, Vector2.one, new[] { CreateClip() }, MelodicMode.ScaleWalkUp,
                melodicMaxOctaves: 2, melodicSkipSteps: PentatonicScale.Length);
            var picker = new VariationPicker(new System.Random(1), PentatonicScale, melodicRootSemitone: 0);

            int Pick(int streak) => picker.Pick(GameSoundId.BalloonPop, entry,
                new PickContext(streak, currentSemitone: 0, burstIndex: 0, normalizedPan: 0f)).MelodicSemitone;

            var previous = Pick(0);
            for (var streak = 1; streak <= 9; streak++)
            {
                var current = Pick(streak);
                Assert.GreaterOrEqual(current, previous);
                previous = current;
            }
        }

        [Test]
        public void Pick_ScaleWalkUp_MaxOctavesShrinksTheCeiling()
        {
            // The ceiling scales with maxOctaves: at maxOctaves 1 the climb is clamped a full octave
            // lower than at maxOctaves 2. Streak 5 tops out at semitone 9 here vs 12 in the wider window.
            var entry = CreateEntry(Vector2.one, Vector2.one, new[] { CreateClip() }, MelodicMode.ScaleWalkUp,
                melodicMaxOctaves: 1, melodicSkipSteps: 1);
            var picker = new VariationPicker(new System.Random(1), PentatonicScale, melodicRootSemitone: 0);

            int Pick(int streak) => picker.Pick(GameSoundId.BalloonPop, entry,
                new PickContext(streak, currentSemitone: 0, burstIndex: 0, normalizedPan: 0f)).MelodicSemitone;

            Assert.AreEqual(9, Pick(5));
            for (var streak = 0; streak < 200; streak++)
            {
                Assert.That(Pick(streak), Is.InRange(0, 9));
            }
        }

        [Test]
        public void Pick_OneSharedPicker_HonoursEachEntrysOwnRange()
        {
            // The point of moving the range onto SfxEntry: a single picker must read each entry's own
            // MelodicMaxOctaves/MelodicSkipSteps, not a shared value. Same streak, same picker, two ranges.
            var wide = CreateEntry(Vector2.one, Vector2.one, new[] { CreateClip() }, MelodicMode.ScaleWalkUp,
                melodicMaxOctaves: 2, melodicSkipSteps: 1);
            var narrow = CreateEntry(Vector2.one, Vector2.one, new[] { CreateClip() }, MelodicMode.ScaleWalkUp,
                melodicMaxOctaves: 1, melodicSkipSteps: 1);
            var picker = new VariationPicker(new System.Random(1), PentatonicScale, melodicRootSemitone: 0);
            var ctx = new PickContext(streak: 5, currentSemitone: 0, burstIndex: 0, normalizedPan: 0f);

            Assert.AreEqual(12, picker.Pick(GameSoundId.BalloonPop, wide, ctx).MelodicSemitone);
            Assert.AreEqual(9, picker.Pick(GameSoundId.BalloonPop, narrow, ctx).MelodicSemitone);
        }

        [Test]
        public void Pick_ScaleWalkDown_MirrorsTheUpWalkBelowTheRoot()
        {
            // ScaleWalkDown is the exact mirror of ScaleWalkUp: same magnitude, negated. Where the up
            // walk peaks an octave ABOVE the root at streak 5 (+12), the down walk dips an octave BELOW
            // (-12), then works back up (streak 9 = -2), all measured from a non-zero root.
            var up = CreateEntry(Vector2.one, Vector2.one, new[] { CreateClip() }, MelodicMode.ScaleWalkUp,
                melodicMaxOctaves: 2, melodicSkipSteps: 1);
            var down = CreateEntry(Vector2.one, Vector2.one, new[] { CreateClip() }, MelodicMode.ScaleWalkDown,
                melodicMaxOctaves: 2, melodicSkipSteps: 1);
            var picker = new VariationPicker(new System.Random(1), PentatonicScale, melodicRootSemitone: 5);

            int PickUp(int streak) => picker.Pick(GameSoundId.BalloonPop, up,
                new PickContext(streak, currentSemitone: 0, burstIndex: 0, normalizedPan: 0f)).MelodicSemitone;
            int PickDown(int streak) => picker.Pick(GameSoundId.BalloonPop, down,
                new PickContext(streak, currentSemitone: 0, burstIndex: 0, normalizedPan: 0f)).MelodicSemitone;

            foreach (var streak in new[] { 0, 5, 9, 14, 100 })
            {
                Assert.AreEqual(2 * 5 - PickUp(streak), PickDown(streak));
            }

            Assert.AreEqual(5 - 12, PickDown(5));
            Assert.AreEqual(5 - 2, PickDown(9));
        }

        [Test]
        public void Pick_ScaleWalkUp_RunawayStreak_StaysBounded()
        {
            // The point of the cap: no runaway. The net-climbing yoyo never exceeds the window top, so a
            // massive streak stays within [root, top-of-window] = [0, pentatonic top + one octave = 21].
            var entry = CreateEntry(Vector2.one, Vector2.one, new[] { CreateClip() }, MelodicMode.ScaleWalkUp,
                melodicMaxOctaves: 2, melodicSkipSteps: 1);
            var picker = new VariationPicker(new System.Random(1), PentatonicScale, melodicRootSemitone: 0);
            const int ceiling = 9 + 12; // pentatonic top note, one octave up in a 2-octave window

            for (var streak = 0; streak < 500; streak++)
            {
                var ctx = new PickContext(streak, currentSemitone: 0, burstIndex: 0, normalizedPan: 0f);

                var playback = picker.Pick(GameSoundId.BalloonPop, entry, ctx);

                Assert.GreaterOrEqual(playback.MelodicSemitone, 0);
                Assert.LessOrEqual(playback.MelodicSemitone, ceiling);
            }
        }

        [Test]
        public void Pick_ScaleWalkUp_MidStreak_MapsToScaleDegree()
        {
            // Degree 2 of {0,2,4,7,9} is 4 semitones, still on the first ascent (no octave rollover).
            var entry = CreateEntry(Vector2.one, Vector2.one, new[] { CreateClip() }, MelodicMode.ScaleWalkUp);
            var picker = new VariationPicker(new System.Random(1), PentatonicScale, melodicRootSemitone: 0);
            var ctx = new PickContext(streak: 2, currentSemitone: 0, burstIndex: 0, normalizedPan: 0f);

            var playback = picker.Pick(GameSoundId.BalloonPop, entry, ctx);

            Assert.AreEqual(4, playback.MelodicSemitone);
            Assert.AreEqual((4).SemitonesToPitchMultiplier(), playback.Pitch, 0.0001f);
        }

        [Test]
        public void Pick_ScaleWalkUp_EmptyScale_FallsBackToPlainPitchWithoutThrowing()
        {
            // Guards the scale-walk offset's scale.Count divisor from a divide-by-zero if a sound is
            // authored as a walk mode without a configured scale.
            var entry = CreateEntry(Vector2.one, Vector2.one, new[] { CreateClip() }, MelodicMode.ScaleWalkUp);
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
            int melodicMaxOctaves = 2,
            int melodicSkipSteps = 1,
            int tensionSemitones = 0,
            bool pan2D = true)
        {
            var entry = new SfxEntry();
            SetField(entry, "_pitchRange", pitchRange);
            SetField(entry, "_volumeRange", volumeRange);
            SetField(entry, "_clips", clips);
            SetField(entry, "_melodicMode", melodicMode);
            SetField(entry, "_melodicMaxOctaves", melodicMaxOctaves);
            SetField(entry, "_melodicSkipSteps", melodicSkipSteps);
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
