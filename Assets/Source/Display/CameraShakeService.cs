using System;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Messages;
using DG.Tweening;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace BalloonParty.Display
{
    /// <summary>
    ///     Punches the gameplay camera on every <see cref="SpawnBlockedMessage"/> (a heart launched
    ///     for a rejected balloon). The shake is an <em>additive offset</em>, applied as a per-frame
    ///     delta in <c>LateUpdate</c> — it composes with whatever else drives the camera (the
    ///     heart-drain pan just absorbs it) instead of fighting over the absolute position, so every
    ///     launch punches, not only the first. Runs unscaled so the drain's slow-mo can't stretch it;
    ///     skipped only while a level-up cinematic owns the camera
    ///     (<see cref="ICinematicState.BlocksShake"/>).
    /// </summary>
    internal class CameraShakeService : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private float _duration = 0.25f;
        [SerializeField] private float _strength = 0.18f;
        [SerializeField] private int _vibrato = 14;

        [Inject] private ICinematicState _cinematic;

        private Vector3 _offset;
        private Vector3 _applied;
        private Tween _shakeTween;
        private IDisposable _subscription;

        private void Awake()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
            }
        }

        private void LateUpdate()
        {
            // Apply only the change since last frame, after every other camera writer has run — adding a
            // delta composes with the pan/follow, where writing an absolute position would override it.
            var delta = _offset - _applied;
            if (delta != Vector3.zero && _camera != null)
            {
                _camera.transform.position += delta;
            }

            _applied = _offset;
        }

        private void OnDestroy()
        {
            _subscription?.Dispose();
            _shakeTween?.Kill();
        }

        [Inject]
        public void Construct(ISubscriber<SpawnBlockedMessage> spawnBlockedSubscriber)
        {
            _subscription = spawnBlockedSubscriber.Subscribe(_ => Shake());
        }

        private void Shake()
        {
            if (_camera == null || _cinematic.BlocksShake)
            {
                return;
            }

            // Restart around zero on every launch; LateUpdate's delta removes any residual offset from a
            // killed mid-flight shake, so back-to-back launches each land a full punch.
            _shakeTween?.Kill();
            _offset = Vector3.zero;
            _shakeTween = DOTween.Shake(() => _offset, v => _offset = v, _duration, _strength, _vibrato)
                .SetUpdate(true)
                .OnComplete(() => _offset = Vector3.zero);
        }
    }
}
