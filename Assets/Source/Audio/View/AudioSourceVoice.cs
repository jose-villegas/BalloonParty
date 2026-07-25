using System;
using System.Threading;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Pool;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Audio;

namespace BalloonParty.Audio.View
{
    internal sealed class AudioSourceVoice : MonoBehaviour, IPoolable
    {
        private const float MinPitchMagnitude = 0.01f;

        [SerializeField] private AudioSource _source;

        private CancellationTokenSource _returnCts;
        private Action<AudioSourceVoice> _onComplete;

        public void OnSpawned()
        {
        }

        public void OnDespawned()
        {
            _source.DOKill();
            _source.Stop();
            _source.clip = null;
            _onComplete = null;
            LifecycleHelper.CancelAndDispose(ref _returnCts);
        }

        internal void SetOutputGroup(AudioMixerGroup group)
        {
            _source.outputAudioMixerGroup = group;
        }

        internal void Play(in VoicePlayback playback, bool loop, float fadeInSeconds, Action<AudioSourceVoice> onComplete)
        {
            // Kill any return still pending from a prior play on this voice (e.g. a stolen
            // slot replayed in place) — and any in-flight fade — before starting the new one.
            LifecycleHelper.CancelAndDispose(ref _returnCts);
            _source.DOKill();
            _onComplete = onComplete;

            if (playback.Clip == null)
            {
                InvokeComplete();
                return;
            }

            _source.clip = playback.Clip;
            _source.pitch = playback.Pitch;
            _source.panStereo = playback.Pan;
            _source.spatialBlend = 0f;
            _source.loop = loop;

            if (fadeInSeconds > 0f)
            {
                _source.volume = 0f;
                _source.DOFade(playback.Volume, fadeInSeconds).SetUpdate(true).SetLink(gameObject);
            }
            else
            {
                _source.volume = playback.Volume;
            }

            _source.Play();

            if (!loop)
            {
                _returnCts = new CancellationTokenSource();
                ScheduleReturnAsync(playback.Clip.length, playback.Pitch, _returnCts.Token).Forget();
            }
        }

        // Ramps volume to a new target over seconds (0 = snap). Kills any in-flight fade first, so a
        // stream of these (a live-driven loop) each supersedes the last.
        internal void FadeVolumeTo(float targetVolume, float seconds)
        {
            _source.DOKill();
            if (seconds <= 0f)
            {
                _source.volume = targetVolume;
                return;
            }

            _source.DOFade(targetVolume, seconds).SetUpdate(true).SetLink(gameObject);
        }

        internal void Stop()
        {
            _source.DOKill();
            _source.Stop();
            LifecycleHelper.CancelAndDispose(ref _returnCts);
            _onComplete = null;
        }

        // Ramps volume to 0 over fadeOutSeconds, then invokes the completion callback so the owner
        // returns the voice through the normal path. A zero/idle fade completes immediately. The pending
        // natural-return timer is cancelled so it can't also fire.
        internal void FadeOutAndComplete(float fadeOutSeconds)
        {
            LifecycleHelper.CancelAndDispose(ref _returnCts);
            _source.DOKill();

            if (fadeOutSeconds <= 0f || _source.clip == null || !_source.isPlaying)
            {
                InvokeComplete();
                return;
            }

            _source.DOFade(0f, fadeOutSeconds).SetUpdate(true).SetLink(gameObject).OnComplete(InvokeComplete);
        }

        private async UniTaskVoid ScheduleReturnAsync(float clipLength, float pitch, CancellationToken ct)
        {
            var seconds = clipLength / Mathf.Max(Mathf.Abs(pitch), MinPitchMagnitude);
            var canceled = await UniTask
                .Delay(TimeSpan.FromSeconds(seconds), ignoreTimeScale: true, cancellationToken: ct)
                .SuppressCancellationThrow();
            if (canceled)
            {
                return;
            }

            InvokeComplete();
        }

        private void InvokeComplete()
        {
            var callback = _onComplete;
            _onComplete = null;
            callback?.Invoke(this);
        }
    }
}
