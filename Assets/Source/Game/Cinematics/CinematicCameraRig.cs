using BalloonParty.Configuration;
using BalloonParty.Display;
using BalloonParty.Shared.Extensions;
using DG.Tweening;
using UnityEngine;
using BalloonParty.Configuration.Cinematics;

namespace BalloonParty.Game.Cinematics
{
    /// <summary>
    ///     The shared cinematic camera driver — all tweens run in unscaled time since segments warp Time.timeScale.
    /// </summary>
    internal class CinematicCameraRig
    {
        private const float FrustumPadding = 0.1f;

        private readonly Camera _camera;
        private readonly OrthogonalSizeCameraController _orthoController;

        private float _baseOrthoSize;
        private Vector3 _basePosition;
        private bool _hasBaseState;
        private Tween _tween;

        public bool HasCamera => _camera != null;

        public CinematicCameraRig(CinematicCameraView view, OrthogonalSizeCameraController orthoController)
        {
            _camera = view != null ? view.Camera : null;
            _orthoController = orthoController;
        }

        public void PreparePanIn(CameraRigCinematicSettings segment)
        {
            if (_hasBaseState)
            {
                Restore();
            }

            KillTween();
            EnableOrtho(false);
            CaptureBaseState();

            if (_camera != null)
            {
                _tween = DOTween.To(
                        () => _camera.orthographicSize,
                        x => _camera.orthographicSize = x,
                        _baseOrthoSize - segment.ZoomAmount,
                        segment.TimeScaleCurve.Duration())
                    .SetEase(Ease.OutQuad)
                    .SetUpdate(true);
            }
        }

        public void PrepareRestore(float duration)
        {
            if (_camera == null)
            {
                return;
            }

            var moveTween = _camera.transform.DOMove(_basePosition, duration)
                .SetEase(Ease.InOutQuad)
                .SetUpdate(true);

            var sizeTween = DOTween.To(
                    () => _camera.orthographicSize,
                    x => _camera.orthographicSize = x,
                    _baseOrthoSize,
                    duration)
                .SetEase(Ease.InOutQuad)
                .SetUpdate(true);

            _tween = DOTween.Sequence().SetUpdate(true).Join(moveTween).Join(sizeTween);
        }

        /// <summary>
        ///     Standalone tweened return to base framing, owning its own completion unlike <see cref="PrepareRestore" />.
        /// </summary>
        public void RestoreTweened(float duration)
        {
            // No captured base state means nothing to un-zoom to — just leave the controller enabled.
            if (_camera == null || !_hasBaseState)
            {
                EnableOrtho(true);
                return;
            }

            KillTween();
            PrepareRestore(duration);
            _tween.OnComplete(() =>
            {
                Restore();
                _tween = null;
            });
        }

        /// <summary>
        ///     Pans toward the focus, keeping it in frustum; a single-point focus is hard-clamped after easing, a spread before.
        /// </summary>
        public void Frame(ICinematicFocus focus, CameraRigCinematicSettings segment, float dt)
        {
            if (_camera == null || !focus.TryGetFocus(out var center, out var min, out var max))
            {
                return;
            }

            var panTarget = Vector3.Lerp(_basePosition, center, segment.PanWeight);
            panTarget.z = _basePosition.z;

            var halfH = _camera.orthographicSize;
            var halfW = halfH * _camera.aspect;
            var isSpread = (max - min).sqrMagnitude > Mathf.Epsilon;

            if (isSpread)
            {
                panTarget.x = VectorMathExtensions.ClampToWindow(panTarget.x, min.x, max.x, halfW, FrustumPadding, center.x);
                panTarget.y = VectorMathExtensions.ClampToWindow(panTarget.y, min.y, max.y, halfH, FrustumPadding, center.y);
                _camera.transform.position = Vector3.Lerp(_camera.transform.position, panTarget, segment.FollowSpeed * dt);
                return;
            }

            var camPos = Vector3.Lerp(_camera.transform.position, panTarget, segment.FollowSpeed * dt);
            camPos.x = VectorMathExtensions.ClampToWindow(camPos.x, min.x, max.x, halfW, FrustumPadding, center.x);
            camPos.y = VectorMathExtensions.ClampToWindow(camPos.y, min.y, max.y, halfH, FrustumPadding, center.y);
            _camera.transform.position = camPos;
        }

        public void Restore()
        {
            if (_camera != null)
            {
                _camera.transform.position = _basePosition;
                _camera.orthographicSize = _baseOrthoSize;
            }

            EnableOrtho(true);
        }

        public void KillTween()
        {
            LifecycleHelper.KillAndClear(ref _tween);
        }

        public void EnableOrtho(bool enabled)
        {
            if (_orthoController != null)
            {
                _orthoController.enabled = enabled;
            }
        }

        private void CaptureBaseState()
        {
            if (_camera != null)
            {
                _baseOrthoSize = _camera.orthographicSize;
                _basePosition = _camera.transform.position;
                _hasBaseState = true;
            }
        }
    }
}
