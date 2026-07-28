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
            DOTween.Kill(this);
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

        internal void Play(in VoicePlayback playback, bool loop, float delaySeconds, float fadeInSeconds,
            float maxPlaySeconds, float fadeOutSeconds, Action<AudioSourceVoice> onComplete)
        {
            // Kill any return still pending from a prior play on this voice (e.g. a stolen
            // slot replayed in place) — and any in-flight fade — before starting the new one.
            LifecycleHelper.CancelAndDispose(ref _returnCts);
            DOTween.Kill(this);
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
                _source.DOFade(playback.Volume, fadeInSeconds).SetDelay(delaySeconds).SetUpdate(true)
                    .SetLink(gameObject);
            }
            else
            {
                _source.volume = playback.Volume;
            }

            if (delaySeconds > 0f)
            {
                _source.PlayDelayed(delaySeconds);
            }
            else
            {
                _source.Play();
            }

            if (!loop)
            {
                _returnCts = new CancellationTokenSource();
                var duration = maxPlaySeconds > 0f
                    ? maxPlaySeconds
                    : playback.Clip.length / Mathf.Max(Mathf.Abs(playback.Pitch), MinPitchMagnitude);
                var useFadeOut = maxPlaySeconds > 0f;
                ScheduleReturnAsync(delaySeconds, duration, useFadeOut, fadeOutSeconds, _returnCts.Token).Forget();
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

        internal void SetPitch(float pitch, float seconds)
        {
            DOTween.Kill(this);
            if (seconds <= 0f)
            {
                _source.pitch = pitch;
                return;
            }

            DOTween.To(() => _source.pitch, x => _source.pitch = x, pitch, seconds)
                .SetId(this).SetUpdate(true).SetLink(gameObject);
        }

        internal void Stop()
        {
            DOTween.Kill(this);
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
            DOTween.Kill(this);
            _source.DOKill();

            if (fadeOutSeconds <= 0f || _source.clip == null || !_source.isPlaying)
            {
                InvokeComplete();
                return;
            }

            _source.DOFade(0f, fadeOutSeconds).SetUpdate(true).SetLink(gameObject).OnComplete(InvokeComplete);
        }

        private async UniTaskVoid ScheduleReturnAsync(float delaySeconds, float duration,
            bool fadeOut, float fadeOutSeconds, CancellationToken ct)
        {
            var seconds = delaySeconds + duration;
            var canceled = await UniTask
                .Delay(TimeSpan.FromSeconds(seconds), ignoreTimeScale: true, cancellationToken: ct)
                .SuppressCancellationThrow();
            if (canceled)
            {
                return;
            }

            if (fadeOut && fadeOutSeconds > 0f)
            {
                FadeOutAndComplete(fadeOutSeconds);
            }
            else
            {
                InvokeComplete();
            }
        }

        private void InvokeComplete()
        {
            var callback = _onComplete;
            _onComplete = null;
            callback?.Invoke(this);
        }
    }
}
