using BalloonParty.Display;
using DG.Tweening;
using UnityEngine;

namespace BalloonParty.Game.Cinematics
{
    /// <summary>
    ///     Owns the level-up cinematic camera: captures the gameplay framing, zooms in and pans to follow
    ///     the tipping trail while keeping it inside the orthographic frustum, then tweens everything back
    ///     on restore. The <see cref="OrthogonalSizeCameraController" /> is disabled for the duration so it
    ///     doesn't fight these moves. All tweens run in unscaled time (the game is paused during pan-in).
    /// </summary>
    internal class CinematicCameraRig
    {
        private const float FrustumPadding = 0.1f;

        private readonly Camera _camera;
        private readonly OrthogonalSizeCameraController _orthoController;
        private readonly float _zoomAmount;
        private readonly float _panWeight;
        private readonly float _followSpeed;

        private float _baseOrthoSize;
        private Vector3 _basePosition;
        private bool _hasBaseState;
        private Tween _tween;

        public CinematicCameraRig(
            Camera camera,
            OrthogonalSizeCameraController orthoController,
            float zoomAmount,
            float panWeight,
            float followSpeed)
        {
            _camera = camera;
            _orthoController = orthoController;
            _zoomAmount = zoomAmount;
            _panWeight = panWeight;
            _followSpeed = followSpeed;
        }

        public bool HasCamera => _camera != null;

        public void PreparePanIn(float zoomDuration)
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
                        _baseOrthoSize - _zoomAmount,
                        zoomDuration)
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

        public void FollowTrail(Vector3 trailPosition, float dt)
        {
            if (_camera == null)
            {
                return;
            }

            var panTarget = Vector3.Lerp(_basePosition, trailPosition, _panWeight);
            panTarget.z = _basePosition.z;

            var camPos = Vector3.Lerp(_camera.transform.position, panTarget, _followSpeed * dt);

            // Keep the trail inside the orthographic frustum to avoid
            // TrailRenderer "Screen position out of view frustum" errors.
            var halfH = _camera.orthographicSize;
            var halfW = halfH * _camera.aspect;
            camPos.x = Mathf.Clamp(camPos.x, trailPosition.x - halfW + FrustumPadding, trailPosition.x + halfW - FrustumPadding);
            camPos.y = Mathf.Clamp(camPos.y, trailPosition.y - halfH + FrustumPadding, trailPosition.y + halfH - FrustumPadding);

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
            _tween?.Kill();
            _tween = null;
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
