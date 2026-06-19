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
    ///     Punches the gameplay camera on every <see cref="SpawnBlockedMessage"/> (a rejected
    ///     balloon), then restores it. Skipped while a cinematic is driving the camera so the two
    ///     never fight; the anchor is captured per-burst so overlapping shakes don't drift.
    /// </summary>
    internal class CameraShakeService : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private float _duration = 0.25f;
        [SerializeField] private float _strength = 0.18f;
        [SerializeField] private int _vibrato = 14;

        [Inject] private ICinematicState _cinematic;

        private Vector3 _anchor;
        private bool _isShaking;
        private IDisposable _subscription;

        private void Awake()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
            }
        }

        private void OnDestroy()
        {
            _subscription?.Dispose();

            if (_camera != null)
            {
                _camera.transform.DOKill();
            }
        }

        [Inject]
        public void Construct(ISubscriber<SpawnBlockedMessage> spawnBlockedSubscriber)
        {
            _subscription = spawnBlockedSubscriber.Subscribe(_ => Shake());
        }

        private void Shake()
        {
            if (_camera == null || _cinematic.IsPlaying)
            {
                return;
            }

            var cameraTransform = _camera.transform;

            if (!_isShaking)
            {
                _anchor = cameraTransform.position;
                _isShaking = true;
            }

            cameraTransform.DOKill();
            cameraTransform.position = _anchor;
            cameraTransform.DOShakePosition(_duration, _strength, _vibrato)
                .OnComplete(() =>
                {
                    cameraTransform.position = _anchor;
                    _isShaking = false;
                });
        }
    }
}
