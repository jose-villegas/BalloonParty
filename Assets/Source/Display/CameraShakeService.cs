using System;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Messages;
using DG.Tweening;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace BalloonParty.Display
{
    /// <summary>Punches the gameplay camera on every <see cref="SpawnBlockedMessage"/>.</summary>
    internal class CameraShakeService : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private float _duration = 0.25f;
        [SerializeField] private float _strength = 0.18f;
        [SerializeField] private int _vibrato = 14;

        [Header("Fire recoil")]
        [SerializeField] private float _recoilStrength = 0.08f;
        [SerializeField] private float _recoilDuration = 0.15f;
        [SerializeField] private int _recoilVibrato = 8;

        [Inject] private ICinematicState _cinematic;

        private Vector3 _offset;
        private Vector3 _applied;
        private Tween _shakeTween;
        private IDisposable _subscription;
        private IDisposable _recoilSubscription;

        private void Awake()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
            }
        }

        private void LateUpdate()
        {
            // Additive delta so it composes with other camera writers instead of overriding them.
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
            _recoilSubscription?.Dispose();
            _shakeTween?.Kill();
        }

        [Inject]
        public void Construct(
            ISubscriber<SpawnBlockedMessage> spawnBlockedSubscriber,
            ISubscriber<ProjectileFiredMessage> firedSubscriber)
        {
            _subscription = spawnBlockedSubscriber.Subscribe(_ => Shake());
            _recoilSubscription = firedSubscriber.Subscribe(msg => Recoil(msg.Direction));
        }

        private void Shake()
        {
            if (_camera == null || _cinematic.Has(CinematicTraits.BlocksShake))
            {
                return;
            }

            // Reset to zero so back-to-back launches each land a full punch.
            _shakeTween?.Kill();
            _offset = Vector3.zero;
            _shakeTween = DOTween.Shake(() => _offset, v => _offset = v, _duration, _strength, _vibrato)
                .SetUpdate(true)
                .OnComplete(() => _offset = Vector3.zero);
        }

        // A directional kick opposite the fire heading — the camera recoil when a shot is fired.
        private void Recoil(Vector3 fireDirection)
        {
            if (_camera == null || _cinematic.Has(CinematicTraits.BlocksShake))
            {
                return;
            }

            _shakeTween?.Kill();
            _offset = Vector3.zero;
            var punch = -fireDirection.normalized * _recoilStrength;
            _shakeTween = DOTween.Punch(() => _offset, v => _offset = v, punch, _recoilDuration, _recoilVibrato)
                .SetUpdate(true)
                .OnComplete(() => _offset = Vector3.zero);
        }
    }
}
