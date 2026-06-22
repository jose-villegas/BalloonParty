using System.Collections.Generic;
using BalloonParty.Display;
using BalloonParty.Shared.Extensions;
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

            // Keep the trail inside the orthographic frustum to avoid
            // TrailRenderer "Screen position out of view frustum" errors.
            FrameToBox(panTarget, trailPosition, trailPosition, trailPosition, dt);
        }

        // Like FollowTrail but for several trails at once: pans toward their centroid and clamps so the
        // whole bounding box stays in frustum (if the spread is wider than the view, just centres on it).
        public void FollowPoints(IReadOnlyList<Vector3> points, int count, float dt)
        {
            if (_camera == null || count <= 0)
            {
                return;
            }

            var center = points.Centroid(count);
            var bounds = points.Bounds(count);
            var panTarget = Vector3.Lerp(_basePosition, center, _panWeight);
            panTarget.z = _basePosition.z;

            FrameToBox(panTarget, center, bounds.min, bounds.max, dt);
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

        // Eases the camera toward panTarget, then clamps each axis so the box [min,max] stays in frustum
        // (centring on the box if it's wider than the view). A single tracked point passes it as min=max.
        private void FrameToBox(Vector3 panTarget, Vector3 center, Vector3 min, Vector3 max, float dt)
        {
            var camPos = Vector3.Lerp(_camera.transform.position, panTarget, _followSpeed * dt);
            var halfH = _camera.orthographicSize;
            var halfW = halfH * _camera.aspect;
            camPos.x = VectorMathExtensions.ClampToWindow(camPos.x, min.x, max.x, halfW, FrustumPadding, center.x);
            camPos.y = VectorMathExtensions.ClampToWindow(camPos.y, min.y, max.y, halfH, FrustumPadding, center.y);
            _camera.transform.position = camPos;
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
