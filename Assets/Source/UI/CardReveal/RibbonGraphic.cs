using UnityEngine;
using UnityEngine.UI;

namespace BalloonParty.UI.CardReveal
{
    /// <summary>
    ///     A strip of ribbon filling its rect: swallowtail-notched at both ends, with a wave running along it that
    ///     the driver moves on by setting <see cref="WaveTime" />. The strip shades by the wave's slope, a face
    ///     tilted up catching the light, so the folds read as satin. <see cref="WaveAt" /> is the same wave, for
    ///     text riding on the ribbon to bend with it.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class RibbonGraphic : MaskableGraphic
    {
        [Tooltip("Columns along the strip: more bends the wave more smoothly.")]
        [SerializeField] [Range(2, 64)] private int _segments = 24;

        [Tooltip("How deep the notch at each end is, as a share of the ribbon's height.")]
        [SerializeField] [Range(0f, 1.5f)] private float _notch = 0.45f;

        [Tooltip("How far the wave lifts the ribbon, in canvas units.")]
        [SerializeField] [Min(0f)] private float _waveHeight = 1.6f;

        [Tooltip("The wave's length along the ribbon, in canvas units.")]
        [SerializeField] [Min(1f)] private float _waveLength = 46f;

        [Tooltip("How fast the wave runs along the ribbon, in waves a second.")]
        [SerializeField] private float _waveSpeed = 0.9f;

        [Tooltip("How much darker a fold turned down is than one turned up to the light.")]
        [SerializeField] [Range(0f, 1f)] private float _shading = 0.28f;

        private float _waveTime;

        /// <summary>Seconds the wave has run: set each frame by whatever flies the ribbon.</summary>
        internal float WaveTime
        {
            get => _waveTime;
            set
            {
                _waveTime = value;
                SetVerticesDirty();
            }
        }

        /// <summary>How far the wave lifts the ribbon at <paramref name="x" /> (from the rect's centre).</summary>
        internal float WaveAt(float x)
        {
            return Wave(x, _waveTime, _waveHeight, _waveLength, _waveSpeed);
        }

        /// <summary>The wave's height at <paramref name="x" />, <paramref name="time" /> seconds in.</summary>
        internal static float Wave(float x, float time, float height, float length, float speed)
        {
            return height * Mathf.Sin(Phase(x, time, length, speed));
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = GetPixelAdjustedRect();
            var halfHeight = rect.height * 0.5f;
            var centreY = rect.center.y;
            var notch = Mathf.Min(rect.height * _notch, rect.width * 0.25f);

            // Three rows: the top and bottom edges run the full width, the middle starts and ends a notch in, so
            // each end is cut in a V.
            for (var i = 0; i <= _segments; i++)
            {
                var t = (float)i / _segments;
                var edgeX = Mathf.Lerp(rect.xMin, rect.xMax, t);
                var middleX = Mathf.Lerp(rect.xMin + notch, rect.xMax - notch, t);
                AddVertex(vh, edgeX, centreY + halfHeight, rect, 1f);
                AddVertex(vh, middleX, centreY, rect, 1.06f);
                AddVertex(vh, edgeX, centreY - halfHeight, rect, 1f);
            }

            for (var i = 0; i < _segments; i++)
            {
                var a = i * 3;
                var b = a + 3;
                vh.AddTriangle(a, b, b + 1);
                vh.AddTriangle(a, b + 1, a + 1);
                vh.AddTriangle(a + 1, b + 1, b + 2);
                vh.AddTriangle(a + 1, b + 2, a + 2);
            }
        }

        private static float Phase(float x, float time, float length, float speed)
        {
            return (x / length - time * speed) * 2f * Mathf.PI;
        }

        // The middle row is a touch brighter, the sheen along a satin ribbon's centre.
        private void AddVertex(VertexHelper vh, float x, float y, Rect rect, float sheen)
        {
            var slope = Mathf.Cos(Phase(x - rect.center.x, _waveTime, _waveLength, _waveSpeed));
            var light = (1f - _shading * (0.5f - 0.5f * slope)) * sheen;
            var tinted = color;
            tinted.r = Mathf.Min(1f, tinted.r * light);
            tinted.g = Mathf.Min(1f, tinted.g * light);
            tinted.b = Mathf.Min(1f, tinted.b * light);
            var uv = new Vector2(Mathf.InverseLerp(rect.xMin, rect.xMax, x), Mathf.InverseLerp(rect.yMin, rect.yMax, y));
            vh.AddVert(new Vector3(x, y + WaveAt(x - rect.center.x), 0f), tinted, uv);
        }
    }
}
