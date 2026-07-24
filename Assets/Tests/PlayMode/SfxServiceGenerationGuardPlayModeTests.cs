using System;
using System.Reflection;
using BalloonParty.Audio;
using BalloonParty.Audio.Configuration;
using BalloonParty.Audio.View;
using BalloonParty.Shared;
using BalloonParty.Shared.Pool;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BalloonParty.Tests.PlayMode
{
    /// <summary>
    ///     Exercises SfxService's slot-generation guard, the one piece of novel, safety-critical logic
    ///     in Step 4: when the global/per-id voice cap forces a slot to be stolen and replayed in place,
    ///     a Stop() carrying the ORIGINAL (now-stale) handle must no-op rather than tearing down the
    ///     newer play sitting in the same slot. Reaching the "steal in place" branch needs a real,
    ///     non-null-clip AudioSourceVoice.Play() so the voice does not complete synchronously — per
    ///     AudioSourceVoiceTests' own EditMode/PlayMode split, a real AudioSource.Play() is PlayMode
    ///     territory, so this is a standalone PlayMode fixture (its own PoolManager/prefab/bank) rather
    ///     than one riding the shared Game scene, whose real content would make the forced steal
    ///     fragile to author downstream.
    /// </summary>
    public class SfxServiceGenerationGuardPlayModeTests
    {
        private GameObject _voiceGo;
        private GameObject _poolContainer;
        private AudioClip _clip;
        private PoolManager _poolManager;
        private SoundBankConfiguration _bank;
        private SfxService _service;

        [SetUp]
        public void SetUp()
        {
            _clip = AudioClip.Create("voice-clip", 4410, 1, 44100, false);

            _voiceGo = new GameObject("VoicePrefab");
            var source = _voiceGo.AddComponent<AudioSource>();
            var voice = _voiceGo.AddComponent<AudioSourceVoice>();
            SetField(voice, "_source", source);
            _voiceGo.SetActive(false);

            _poolContainer = new GameObject("PoolContainer");
            _poolManager = new PoolManager();
            _poolManager.Register(AudioPoolKeys.VoicePoolKey, new SimplePoolChannel<AudioSourceVoice>(voice),
                _poolContainer.transform);

            var entry = new SfxEntry();
            SetField(entry, "_clips", new[] { _clip });
            SetField(entry, "_maxConcurrentVoices", 1);

            _bank = ScriptableObject.CreateInstance<SoundBankConfiguration>();
            var entries = new SfxEntry[Enum.GetValues(typeof(GameSoundId)).Length];
            entries[(int)GameSoundId.BalloonPop] = entry;
            SetField(_bank, "_entries", entries);
            SetField(_bank, "_globalVoiceCap", 1);

            var flightConfig = new StubFlightConfig(new Vector4(5f, 5f, -5f, -5f));

            var limiter = new VoiceLimiter(_bank.GlobalVoiceCap);
            var throttle = new SfxThrottleGate(() => 0f, coalesceWindowSeconds: 1f, maxBurstPerWindow: 10);
            var picker = new VariationPicker(new System.Random(1), Array.Empty<int>(), melodicRootSemitone: 0);

            _service = new SfxService(_bank, _poolManager, new NullAudioMixerRouter(), flightConfig, limiter,
                throttle, picker);
        }

        [TearDown]
        public void TearDown()
        {
            // Cancels any in-flight scheduled return before the objects it targets are destroyed.
            _service.Dispose();

            Object.DestroyImmediate(_poolContainer);
            Object.DestroyImmediate(_voiceGo);
            Object.DestroyImmediate(_bank);
            Object.DestroyImmediate(_clip);
        }

        [Test]
        public void Stop_StaleHandleAfterSlotSteal_DoesNotTearDownTheReusedVoice()
        {
            var staleHandle = _service.Play(GameSoundId.BalloonPop, null);
            // GlobalVoiceCap=1 and MaxConcurrentVoices=1 force this second Play to steal slot 0 in place.
            var currentHandle = _service.Play(GameSoundId.BalloonPop, null);

            _service.Stop(staleHandle);

            var slot = GetSlot(currentHandle.VoiceId);
            Assert.IsNotNull(GetSlotField(slot, "Voice"),
                "Stop(stale handle) released the voice the newer handle owns.");
            Assert.AreEqual(currentHandle.Generation, GetSlotField(slot, "Generation"),
                "Stop(stale handle) mutated the slot generation.");
        }

        [Test]
        public void Stop_CurrentHandleAfterSlotSteal_StillStopsIt()
        {
            _service.Play(GameSoundId.BalloonPop, null);
            var currentHandle = _service.Play(GameSoundId.BalloonPop, null);

            _service.Stop(currentHandle);

            var slot = GetSlot(currentHandle.VoiceId);
            Assert.IsNull(GetSlotField(slot, "Voice"),
                "Stop(current handle) should still release the voice it legitimately owns.");
        }

        private object GetSlot(int voiceId)
        {
            var slots = (Array)typeof(SfxService)
                .GetField("_slots", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(_service);
            return slots.GetValue(voiceId);
        }

        private static object GetSlotField(object slot, string fieldName)
        {
            return slot.GetType().GetField(fieldName)!.GetValue(slot);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            target.GetType()
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(target, value);
        }

        // Hand-rolled rather than NSubstitute: the PlayMode test assembly's asmdef does not reference
        // NSubstitute (only the EditMode one does), and only LimitsClockwise is read by SfxService.
        private sealed class StubFlightConfig : IProjectileFlightConfig
        {
            public int ProjectileStartingShields => 0;
            public float ProjectileSpeed => 0f;
            public float ProjectileLoadDuration => 0f;
            public Vector4 LimitsClockwise { get; }
            public int CruiseWallBounceThreshold => 0;
            public float CruiseSpeedPerShield => 0f;
            public float MaxCruiseSpeedMultiplier => 0f;
            public AnimationCurve CruiseTapCurve => null;
            public float CruiseTapEaseDuration => 0f;
            public bool SweepEnabled => false;
            public int SweepTapThreshold => 0;
            public int CruisePiercingTapThreshold => 0;
            public float PierceDischargeTimeScale => 0f;
            public float PierceDischargeTimeScaleDuration => 0f;
            public AnimationCurve LastShieldApproachCurve => null;
            public float LastShieldApproachDuration => 0f;
            public AnimationCurve LastShieldTimeScaleCurve => null;
            public float ShieldTrailDuration => 0f;

            public StubFlightConfig(Vector4 limitsClockwise)
            {
                LimitsClockwise = limitsClockwise;
            }
        }
    }
}
