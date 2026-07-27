using System;
using System.Collections.Generic;
using BalloonParty.Audio.Configuration;
using BalloonParty.Audio.View;
using BalloonParty.Game.Run;
using BalloonParty.Shared;
using BalloonParty.Shared.Diagnostics;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Pool;
using UnityEngine;
using VContainer;

namespace BalloonParty.Audio
{
    internal sealed class SfxService : ISoundPlayer, IMelodicContext, IRunResettable, IDisposable
    {
        private const float BurstSpreadSemitones = 0.7f;
        private const float BurstVolumeFalloff = 0.25f;

        private readonly ISoundBankConfiguration _bank;
        private readonly PoolManager _poolManager;
        private readonly IAudioMixerRouter _mixerRouter;
        private readonly VoiceLimiter _limiter;
        private readonly SfxThrottleGate _throttle;
        private readonly VariationPicker _picker;
        private readonly System.Random _rng;
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
            _rng = new System.Random();
            _slots = new VoiceSlot[bank.GlobalVoiceCap];
            _onVoiceComplete = OnVoiceComplete;

            var walls = new WallLimits(flightConfig.LimitsClockwise);
            _panLeft = walls.Left;
            _panRight = walls.Right;
        }

        public SoundHandle Play(GameSoundId id, Vector3? position, int? melodicStreak = null, int semitoneOffset = 0, float volumeScale = 1f)
        {
            if (!_bank.TryGet(id, out var entry))
            {
                return SoundHandle.None;
            }

            // A single-instance loop that's already sounding ignores the retrigger and hands back the
            // live handle, rather than starting or stealing-to-restart a second voice.
            if (entry.Loop && entry.SingleInstance && TryGetActiveHandle(id, out var active))
            {
                return active;
            }

            if (!_throttle.TryPass(id, entry.CooldownSeconds, out var burstIndex))
            {
                return SoundHandle.None;
            }

            Log.Assert(entry.HasClips, "Audio", $"SfxEntry '{id}' resolved with no clips.");

            // Unison mode: fire all clips as layered voices in a single Play call.
            if (entry.ClipPickMode == ClipPickMode.Unison && entry.Clips.Count > 1)
            {
                var handle = PlayUnison(id, entry, position, burstIndex, semitoneOffset, volumeScale, melodicStreak);
                PlayLayers(id, entry, position, semitoneOffset, volumeScale, melodicStreak);
                return handle;
            }

            var pan = ComputePan(position);
            var context = new PickContext(melodicStreak ?? _currentStreak, _currentSemitone, burstIndex, pan);
            var playback = _picker.Pick(id, entry, in context);

            if (semitoneOffset != 0)
            {
                var offsetPitch = (playback.MelodicSemitone + semitoneOffset).SemitonesToPitchMultiplier();
                playback = new VoicePlayback(playback.Clip, offsetPitch, playback.Volume, playback.Pan, playback.MelodicSemitone);
            }

            if (volumeScale != 1f)
            {
                playback = new VoicePlayback(playback.Clip, playback.Pitch, playback.Volume * volumeScale, playback.Pan, playback.MelodicSemitone);
            }

            // Only the ambient pop walk establishes the key that Tension entries rub against. A caller
            // that overrides the streak (the shield-loss walk) plays its note without hijacking that key.
            if (melodicStreak == null && entry.MelodicMode is MelodicMode.ScaleWalkUp
                or MelodicMode.ScaleWalkDown)
            {
                _currentSemitone = playback.MelodicSemitone;
            }

            var primaryHandle = AllocateAndPlay(id, entry, in playback);
            PlayLayers(id, entry, position, semitoneOffset, volumeScale, melodicStreak);
            return primaryHandle;
        }

        public void SetVolumeFactor(SoundHandle handle, float factor)
        {
            if (!handle.IsValid)
            {
                return;
            }

            var voiceId = handle.VoiceId;
            if (voiceId < 0 || voiceId >= _slots.Length || _slots[voiceId].Generation != handle.Generation)
            {
                return;
            }

            var voice = _slots[voiceId].Voice;
            if (voice == null || !_bank.TryGet(_slots[voiceId].Id, out var entry))
            {
                return;
            }

            // VolumeRange is the min→max envelope; the caller's 0-1 factor picks along it. Ramps over the
            // entry's FadeInSeconds so each step (e.g. a speed tap) glides rather than snaps.
            var target = Mathf.Lerp(entry.VolumeRange.x, entry.VolumeRange.y, Mathf.Clamp01(factor));
            voice.FadeVolumeTo(target, entry.FadeInSeconds);
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

            FadeOutSlot(voiceId);
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

        private SoundHandle PlayUnison(GameSoundId id, SfxEntry entry, Vector3? position, int burstIndex,
            int semitoneOffset, float volumeScale, int? melodicStreak)
        {
            var pan = ComputePan(position);
            var primaryHandle = SoundHandle.None;
            var clipCount = entry.Clips.Count;

            for (var layer = 0; layer < clipCount; layer++)
            {
                var clipIndex = _picker.SelectClipForLayer(layer, entry);
                var clip = entry.Clips[clipIndex];
                var layerBurstIndex = burstIndex + layer;

                var context = new PickContext(melodicStreak ?? _currentStreak, _currentSemitone, layerBurstIndex, pan);
                var pitch = entry.MelodicMode == MelodicMode.None
                    ? RandomRange(entry.PitchRange)
                    : _picker.Pick(id, entry, in context).Pitch;
                var volume = RandomRange(entry.VolumeRange);

                // Burst spread: subsequent layers get progressively spread pitch and reduced volume.
                if (layer > 0)
                {
                    pitch *= (layer * BurstSpreadSemitones).SemitonesToPitchMultiplier();
                    volume *= 1f / (1f + layer * BurstVolumeFalloff);
                }

                if (semitoneOffset != 0)
                {
                    pitch = semitoneOffset.SemitonesToPitchMultiplier();
                }

                if (volumeScale != 1f)
                {
                    volume *= volumeScale;
                }

                var playback = new VoicePlayback(clip, pitch, volume, pan, 0);
                var handle = AllocateAndPlay(id, entry, in playback);

                if (layer == 0)
                {
                    primaryHandle = handle;
                }
            }

            return primaryHandle;
        }

        private void PlayLayers(GameSoundId id, SfxEntry entry, Vector3? position,
            int semitoneOffset, float volumeScale, int? melodicStreak)
        {
            var layers = entry.Layers;
            if (layers == null || layers.Count == 0)
            {
                return;
            }

            for (var i = 0; i < layers.Count; i++)
            {
                var layer = layers[i];
                if (layer == null || !layer.HasClips)
                {
                    continue;
                }

                var pan = ComputePan(position);
                var ctx = new PickContext(melodicStreak ?? _currentStreak, _currentSemitone, 0, pan);
                var layerPlayback = _picker.PickLayer(id, layer, in ctx);

                if (semitoneOffset != 0)
                {
                    var offsetPitch = (layerPlayback.MelodicSemitone + semitoneOffset).SemitonesToPitchMultiplier();
                    layerPlayback = new VoicePlayback(layerPlayback.Clip, offsetPitch, layerPlayback.Volume, layerPlayback.Pan, layerPlayback.MelodicSemitone);
                }

                if (volumeScale != 1f)
                {
                    layerPlayback = new VoicePlayback(layerPlayback.Clip, layerPlayback.Pitch, layerPlayback.Volume * volumeScale, layerPlayback.Pan, layerPlayback.MelodicSemitone);
                }

                AllocateAndPlay(id, entry, in layerPlayback);
            }
        }

        private SoundHandle AllocateAndPlay(GameSoundId id, SfxEntry entry, in VoicePlayback playback)
        {
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
            _slots[voiceId].Id = id;

            voice.SetOutputGroup(_mixerRouter.GroupFor(entry.Channel));
            voice.Play(in playback, entry.Loop, entry.DelaySeconds, entry.FadeInSeconds,
                entry.MaxPlaySeconds, entry.FadeOutSeconds, _onVoiceComplete);

            StopVoicesFor(entry.StopsOnPlay, voiceId);

            return new SoundHandle(voiceId, generation);
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

        private bool TryGetActiveHandle(GameSoundId id, out SoundHandle handle)
        {
            for (var i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].Voice != null && _slots[i].Id == id)
                {
                    handle = new SoundHandle(i, _slots[i].Generation);
                    return true;
                }
            }

            handle = SoundHandle.None;
            return false;
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

        private float RandomRange(Vector2 range)
        {
            return Mathf.Lerp(range.x, range.y, (float)_rng.NextDouble());
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

        // Stops a voice with its entry's fade-out. The fade's completion runs OnVoiceComplete, which
        // releases the limiter and returns the voice — so, unlike StopSlot, this must not do that itself
        // (a zero fade completes synchronously through that same path).
        private void FadeOutSlot(int voiceId)
        {
            var voice = _slots[voiceId].Voice;
            if (voice == null)
            {
                return;
            }

            var fadeOut = _bank.TryGet(_slots[voiceId].Id, out var entry) ? entry.FadeOutSeconds : 0f;
            voice.FadeOutAndComplete(fadeOut);
        }

        // On play, an entry can silence still-active voices of other ids (each fading per its own entry).
        private void StopVoicesFor(IReadOnlyList<GameSoundId> ids, int exceptVoiceId)
        {
            if (ids == null || ids.Count == 0)
            {
                return;
            }

            for (var i = 0; i < _slots.Length; i++)
            {
                if (i == exceptVoiceId || _slots[i].Voice == null)
                {
                    continue;
                }

                for (var j = 0; j < ids.Count; j++)
                {
                    if (_slots[i].Id == ids[j])
                    {
                        FadeOutSlot(i);
                        break;
                    }
                }
            }
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
            public GameSoundId Id;
        }
    }
}
