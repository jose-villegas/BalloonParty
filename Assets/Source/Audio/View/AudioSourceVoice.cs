using System;
using System.Threading;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Pool;
using Cysharp.Threading.Tasks;
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
            _source.Stop();
            _source.clip = null;
            _onComplete = null;
            LifecycleHelper.CancelAndDispose(ref _returnCts);
        }

        internal void SetOutputGroup(AudioMixerGroup group)
        {
            _source.outputAudioMixerGroup = group;
        }

        internal void Play(in VoicePlayback playback, bool loop, Action<AudioSourceVoice> onComplete)
        {
            // Kill any return still pending from a prior play on this voice (e.g. a stolen
            // slot replayed in place) before starting the new one.
            LifecycleHelper.CancelAndDispose(ref _returnCts);
            _onComplete = onComplete;

            if (playback.Clip == null)
            {
                InvokeComplete();
                return;
            }

            _source.clip = playback.Clip;
            _source.pitch = playback.Pitch;
            _source.volume = playback.Volume;
            _source.panStereo = playback.Pan;
            _source.spatialBlend = 0f;
            _source.loop = loop;
            _source.Play();

            if (!loop)
            {
                _returnCts = new CancellationTokenSource();
                ScheduleReturnAsync(playback.Clip.length, playback.Pitch, _returnCts.Token).Forget();
            }
        }

        internal void Stop()
        {
            _source.Stop();
            LifecycleHelper.CancelAndDispose(ref _returnCts);
            _onComplete = null;
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
