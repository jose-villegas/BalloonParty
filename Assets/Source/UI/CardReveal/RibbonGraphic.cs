using UnityEngine;
using UnityEngine.UI;

namespace BalloonParty.UI.CardReveal
{
    /// <summary>
    ///     A strip of ribbon filling its rect: swallowtail-notched at both ends, with a wave running along it that
    ///     the driver moves on by setting <see cref="WaveTime" />. It is cel shaded in the scene's light by its
    ///     material (<c>BalloonParty/Sprite/CelRibbon</c>), from what the mesh tells it: how each fold turns, and
    ///     a copy of the strip for the drop shadow. <see cref="WaveAt" /> is the same wave, for text riding on the
    ///     ribbon to bend with it.
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

            // The shadow's copy first, so it draws under the ribbon; BalloonParty/Sprite/CelRibbon moves it.
            AddStrip(vh, true);
            AddStrip(vh, false);
        }

        private static float Phase(float x, float time, float length, float speed)
        {
            return (x / length - time * speed) * 2f * Mathf.PI;
        }

        // Three rows: the top and bottom edges run the full width, the middle starts and ends a notch in, so each
        // end is cut in a V.
        private void AddStrip(VertexHelper vh, bool shadow)
        {
            var rect = GetPixelAdjustedRect();
            var halfHeight = rect.height * 0.5f;
            var centreY = rect.center.y;
            var notch = Mathf.Min(rect.height * _notch, rect.width * 0.25f);
            var first = vh.currentVertCount;
            for (var i = 0; i <= _segments; i++)
            {
                var t = (float)i / _segments;
                var edgeX = Mathf.Lerp(rect.xMin, rect.xMax, t);
                var middleX = Mathf.Lerp(rect.xMin + notch, rect.xMax - notch, t);
                AddVertex(vh, edgeX, centreY + halfHeight, rect, shadow ? -1f : 0f);
                AddVertex(vh, middleX, centreY, rect, shadow ? -1f : 1f);
                AddVertex(vh, edgeX, centreY - halfHeight, rect, shadow ? -1f : 0f);
            }

            for (var i = 0; i < _segments; i++)
            {
                var a = first + i * 3;
                var b = a + 3;
                vh.AddTriangle(a, b, b + 1);
                vh.AddTriangle(a, b + 1, a + 1);
                vh.AddTriangle(a + 1, b + 1, b + 2);
                vh.AddTriangle(a + 1, b + 2, a + 2);
            }
        }

        // The UV carries the shading CelRibbon needs: x how far the fold here turns to the right (the wave's slope,
        // read as the ribbon twisting toward and away from the screen), y the row (1 the middle, 0 an edge, -1 the
        // shadow's copy).
        private void AddVertex(VertexHelper vh, float x, float y, Rect rect, float row)
        {
            var turn = -Mathf.Cos(Phase(x - rect.center.x, _waveTime, _waveLength, _waveSpeed));
            vh.AddVert(new Vector3(x, y + WaveAt(x - rect.center.x), 0f), color, new Vector2(turn, row));
        }
    }
}
