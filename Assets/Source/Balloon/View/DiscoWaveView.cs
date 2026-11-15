using BalloonParty.Configuration.Balloons;
using BalloonParty.Shared.Diagnostics;
using UnityEngine;
using VContainer;

namespace BalloonParty.Balloon.View
{
    /// <summary>
    ///     A placeholder for the disco ball's wave (PLAN-DiscoBalloon phase 3): a plain ring at the disc's edge. Phase 4
    ///     replaces it with the rainbow disc shader.
    /// </summary>
    internal sealed class DiscoWaveView : MonoBehaviour, IDiscoWaveView
    {
        private const int Segments = 96;

        private static readonly Color RingColor = new(1f, 1f, 1f, 0.8f);

        private readonly Vector3[] _unitCircle = new Vector3[Segments];
        private readonly Vector3[] _points = new Vector3[Segments];

        private LineRenderer _ring;
        private Vector3 _center;

        // Built here, not in Awake: the settings arrive with the injection, after AddComponent's Awake.
        [Inject]
        internal void Construct(IBalloonsConfiguration balloons)
        {
            var settings = balloons.DiscoWave;
            _ring = gameObject.AddComponent<LineRenderer>();
            _ring.useWorldSpace = true;
            _ring.loop = true;
            _ring.positionCount = Segments;
            _ring.widthMultiplier = settings.RingWidth;
            _ring.startColor = RingColor;
            _ring.endColor = RingColor;
            _ring.sharedMaterial = settings.RingMaterial;
            if (settings.RingMaterial == null)
            {
                Log.Warn("DiscoWaveView", "No ring material on BalloonsConfiguration's Disco Ball Wave: the wave draws nothing.");
            }

            _ring.sortingLayerName = settings.SortingLayerName;
            _ring.sortingOrder = settings.SortingOrder;
            for (var i = 0; i < Segments; i++)
            {
                var angle = i * Mathf.PI * 2f / Segments;
                _unitCircle[i] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            }

            _ring.enabled = false;
        }

        public void Show(Vector3 center)
        {
            _center = center;
            SetRadius(0f);
            _ring.enabled = true;
        }

        public void SetRadius(float radius)
        {
            for (var i = 0; i < Segments; i++)
            {
                _points[i] = _center + _unitCircle[i] * radius;
            }

            _ring.SetPositions(_points);
        }

        public void Hide()
        {
            _ring.enabled = false;
        }
    }
}
