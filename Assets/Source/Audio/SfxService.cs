using System;
using BalloonParty.Audio.Configuration;
using BalloonParty.Audio.View;
using BalloonParty.Game.Run;
using BalloonParty.Shared;
using BalloonParty.Shared.Diagnostics;
using BalloonParty.Shared.Pool;
using UnityEngine;
using VContainer;

namespace BalloonParty.Audio
{
    internal sealed class SfxService : ISoundPlayer, IMelodicContext, IRunResettable, IDisposable
    {
        private readonly ISoundBankConfiguration _bank;
        private readonly PoolManager _poolManager;
        private readonly IAudioMixerRouter _mixerRouter;
        private readonly VoiceLimiter _limiter;
        private readonly SfxThrottleGate _throttle;
        private readonly VariationPicker _picker;
        private readonly VoiceSlot[] _slots;
        private readonly Action<AudioSourceVoice> _onVoiceComplete;
        private readonly float _panLeft;
        private readonly float _panRight;

        private int _currentStreak;
        private int _currentSemitone;

        public int ResetOrder => RunResetOrder.Quiesce;

        [Inject]
        internal SfxService(ISoundBankConfiguration bank, PoolManager poolManager, IAudioMixerRouter mixerRouter,
            IProjectileFlightConfig flightConfig, VoiceLimiter limiter, SfxThrottleGate throttle, VariationPicker picker)
        {
            _bank = bank;
            _poolManager = poolManager;
            _mixerRouter = mixerRouter;
            _limiter = limiter;
            _throttle = throttle;
            _picker = picker;
            _slots = new VoiceSlot[bank.GlobalVoiceCap];
            _onVoiceComplete = OnVoiceComplete;

            var walls = new WallLimits(flightConfig.LimitsClockwise);
            _panLeft = walls.Left;
            _panRight = walls.Right;
        }

        public SoundHandle Play(GameSoundId id, Vector3? position)
        {
            if (!_bank.TryGet(id, out var entry))
            {
                return SoundHandle.None;
            }

            if (!_throttle.TryPass(id, entry.CooldownSeconds, out var burstIndex))
            {
                return SoundHandle.None;
            }

            Log.Assert(entry.HasClips, "Audio", $"SfxEntry '{id}' resolved with no clips.");

            var pan = ComputePan(position);
            var context = new PickContext(_currentStreak, _currentSemitone, burstIndex, pan);
            var playback = _picker.Pick(id, entry, in context);

            if (entry.MelodicMode == MelodicMode.ScaleWalk)
            {
                _currentSemitone = playback.MelodicSemitone;
            }

            if (!_limiter.TryAcquire(id, entry.MaxConcurrentVoices, entry.Priority, out var voiceId, out var stolenVoiceId))
            {
                return SoundHandle.None;
            }

            AudioSourceVoice voice;
            if (stolenVoiceId >= 0 && _slots[voiceId].Voice != null)
            {
                voice = _slots[voiceId].Voice;
            }
            else
            {
                voice = _poolManager.Get<AudioSourceVoice>(AudioPoolKeys.VoicePoolKey);
            }

            var generation = NextGeneration(voiceId);
            _slots[voiceId].Voice = voice;
            _slots[voiceId].Channel = entry.Channel;

            voice.SetOutputGroup(_mixerRouter.GroupFor(entry.Channel));
            voice.Play(in playback, entry.Loop, _onVoiceComplete);

            return new SoundHandle(voiceId, generation);
        }

        public void Stop(SoundHandle handle)
        {
            if (!handle.IsValid)
            {
                return;
            }

            var voiceId = handle.VoiceId;
            if (voiceId < 0 || voiceId >= _slots.Length)
            {
                return;
            }

            // Stale handle: the slot was stolen and reused since this handle was minted.
            if (_slots[voiceId].Generation != handle.Generation)
            {
                return;
            }

            StopSlot(voiceId);
        }

        public void SetStreak(int streak)
        {
            _currentStreak = streak;
        }

        public void ResetRun(int generation)
        {
            StopAllVoices();
            _picker.Reset();
            _throttle.Reset();
            _currentStreak = 0;
            _currentSemitone = 0;
        }

        public void Dispose()
        {
            StopAllVoices();
        }

        internal void StopChannel(SfxChannel channel)
        {
            for (var i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].Voice != null && _slots[i].Channel == channel)
                {
                    StopSlot(i);
                }
            }
        }

        private void OnVoiceComplete(AudioSourceVoice voice)
        {
            var voiceId = IndexOfSlot(voice);
            if (voiceId < 0)
            {
                return;
            }

            _limiter.Release(voiceId);
            _poolManager.Return(AudioPoolKeys.VoicePoolKey, voice);
            _slots[voiceId].Voice = null;
        }

        private int IndexOfSlot(AudioSourceVoice voice)
        {
            for (var i = 0; i < _slots.Length; i++)
            {
                if (ReferenceEquals(_slots[i].Voice, voice))
                {
                    return i;
                }
            }

            return -1;
        }

        private uint NextGeneration(int voiceId)
        {
            var next = _slots[voiceId].Generation + 1u;
            if (next == 0u)
            {
                next = 1u;
            }

            _slots[voiceId].Generation = next;
            return next;
        }

        private float ComputePan(Vector3? position)
        {
            // World-X → [-1, 1]. The Pan2D gate lives in VariationPicker, the single place
            // that decides whether an entry actually uses this pan.
            if (!position.HasValue)
            {
                return 0f;
            }

            var t = Mathf.InverseLerp(_panLeft, _panRight, position.Value.x);
            return Mathf.Clamp(t * 2f - 1f, -1f, 1f);
        }

        private void StopSlot(int voiceId)
        {
            var voice = _slots[voiceId].Voice;
            if (voice == null)
            {
                return;
            }

            voice.Stop();
            _limiter.Release(voiceId);
            _poolManager.Return(AudioPoolKeys.VoicePoolKey, voice);
            _slots[voiceId].Voice = null;
        }

        private void StopAllVoices()
        {
            for (var i = 0; i < _slots.Length; i++)
            {
                var voice = _slots[i].Voice;
                if (voice == null)
                {
                    continue;
                }

                voice.Stop();
                _poolManager.Return(AudioPoolKeys.VoicePoolKey, voice);
                _slots[i].Voice = null;
            }

            _limiter.Clear();
        }

        private struct VoiceSlot
        {
            public AudioSourceVoice Voice;
            public uint Generation;
            public SfxChannel Channel;
        }
    }
}
