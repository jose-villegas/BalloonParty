using BalloonParty.Configuration;
using UnityEngine;

namespace BalloonParty.Item.Preview
{
    /// <summary>
    ///     Single shared owner of "what is visible" for the range-preview figures — the camera-derived
    ///     world rect, expanded by a margin derived from <see cref="IItemPreviewConfig.DashLength" /> and
    ///     <see cref="IItemPreviewConfig.DashSpacing" />.
    ///     Both <see cref="ItemPreviewTicker" /> (culling pens) and <see cref="LaserRangePreview" />
    ///     (clipping beam lines) read this one instance, so the two agree on one notion of "visible"
    ///     instead of drifting apart.
    /// </summary>
    /// <remarks>
    ///     <see cref="Refresh" /> is idempotent per frame (guarded on <see cref="Time.frameCount" />), so
    ///     both <see cref="ItemRangePreviewController" /> and <see cref="ItemPreviewTicker" /> can call it
    ///     before reading without either depending on tickable ordering — the whole reason this lives as
    ///     a shared object rather than inside one of them.
    /// </remarks>
    internal sealed class ItemPreviewViewport
    {
        private readonly IItemPreviewConfig _config;

        // Lazily resolved rather than injected — Camera.main is already Unity's cached lookup, this local
        // cache just keeps it off the per-frame path (the established pattern — ThrowerView.cs:75,
        // CinematicCameraView.cs:13, CameraShakeView.cs:33). Not DI-registered:
        // RegisterComponentInHierarchy<Camera> risks binding SceneCaptureService's capture camera instead
        // of the main one, a risk not worth taking for a presentational read.
        private Camera _camera;
        private int _lastRefreshedFrame = -1;

        // IsActive false means "no main camera yet, or it isn't orthographic" — cull nothing / clip
        // nothing rather than guessing at maths that don't apply to a perspective camera.
        private bool _isActive;
        private float _minX;
        private float _maxX;
        private float _minY;
        private float _maxY;

        internal bool IsActive => _isActive;
        internal float MinX => _minX;
        internal float MaxX => _maxX;
        internal float MinY => _minY;
        internal float MaxY => _maxY;

        internal ItemPreviewViewport(IItemPreviewConfig config)
        {
            _config = config;
        }

        /// <summary>
        ///     Recomputes the visible rect from <see cref="Camera.main" />, at most once per frame — later
        ///     calls this frame are free, so every caller can refresh unconditionally before reading.
        /// </summary>
        internal void Refresh()
        {
            if (_lastRefreshedFrame == Time.frameCount)
            {
                return;
            }

            _lastRefreshedFrame = Time.frameCount;

            _camera ??= Camera.main;
            if (_camera == null || !_camera.orthographic)
            {
                _isActive = false;
                return;
            }

            // The margin is the stride (DashLength + DashSpacing), not DashLength alone: AdvanceDash now
            // pins the gap and lets the dash absorb each stroke's rounding error, so a capped figure's
            // slot — and with it a pen's travel within its dash — can exceed DashLength. The stride is
            // the widest a slot is ever intended to be, so it bounds a pen's travel in every case,
            // including a capped figure, and expanding the visible rect by exactly that much is still the
            // smallest expansion that can never clip a dash mid-stroke — no authored knob needed.
            var margin = _config.DashLength + _config.DashSpacing;
            var halfHeight = _camera.orthographicSize + margin;
            var halfWidth = (_camera.orthographicSize * _camera.aspect) + margin;
            var centre = _camera.transform.position;

            _isActive = true;
            _minX = centre.x - halfWidth;
            _maxX = centre.x + halfWidth;
            _minY = centre.y - halfHeight;
            _maxY = centre.y + halfHeight;
        }

        internal bool Contains(Vector3 position)
        {
            return position.x >= _minX && position.x <= _maxX && position.y >= _minY && position.y <= _maxY;
        }

        /// <summary>
        ///     Distance from <paramref name="origin" /> along <paramref name="direction" /> to the nearest
        ///     edge of the visible rect it exits through — a standard slab test, mirroring
        ///     <c>WallLimits.TryFindCrossing</c>'s own. False when the rect isn't active or the ray never
        ///     crosses an edge (a near-zero direction).
        /// </summary>
        internal bool TryFindExit(Vector3 origin, Vector3 direction, out float distance)
        {
            distance = 0f;
            if (!_isActive)
            {
                return false;
            }

            const float epsilon = 1e-6f;
            var horizontalT = float.PositiveInfinity;
            if (direction.x > epsilon)
            {
                horizontalT = (_maxX - origin.x) / direction.x;
            }
            else if (direction.x < -epsilon)
            {
                horizontalT = (_minX - origin.x) / direction.x;
            }

            var verticalT = float.PositiveInfinity;
            if (direction.y > epsilon)
            {
                verticalT = (_maxY - origin.y) / direction.y;
            }
            else if (direction.y < -epsilon)
            {
                verticalT = (_minY - origin.y) / direction.y;
            }

            var t = Mathf.Min(horizontalT, verticalT);
            if (float.IsInfinity(t) || t < 0f)
            {
                return false;
            }

            distance = t;
            return true;
        }
    }
}
