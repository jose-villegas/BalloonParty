using DG.Tweening;
using UnityEngine;

namespace BalloonParty.Display
{
    /// <summary>
    ///     Camera-shake View: owns the shake/recoil animation and the additive <see cref="LateUpdate" />
    ///     compositor on the (persistent) camera. Pure presentation — <see cref="CameraShakeController" />
    ///     decides WHEN to shake (message-driven, cinematic-gated). Split out of the former
    ///     <c>CameraShakeService</c> so the component can ride the persistent camera while its gameplay
    ///     triggers stay in the run scope.
    /// </summary>
    internal class CameraShakeView : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private float _duration = 0.25f;
        [SerializeField] private float _strength = 0.18f;
        [SerializeField] private int _vibrato = 14;

        [Header("Fire recoil")]
        [SerializeField] private float _recoilStrength = 0.08f;
        [SerializeField] private float _recoilDuration = 0.15f;
        [SerializeField] private int _recoilVibrato = 8;

        private Vector3 _offset;
        private Vector3 _applied;
        private Tween _shakeTween;

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
            _shakeTween?.Kill();
        }

        internal void Shake()
        {
            if (_camera == null)
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
        internal void Recoil(Vector3 fireDirection)
        {
            if (_camera == null)
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
