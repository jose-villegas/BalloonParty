using UnityEngine;
using UnityEngine.UI;

namespace BalloonParty.UI.CardReveal
{
    /// <summary>
    ///     A pen stroke through a ribbon's line, bent by the same wave as the ribbon and its writing, so the strike
    ///     rides the folds with the letters. Lives under the ribbon's text, in the text's space (measured from the
    ///     ribbon's centre); <see cref="RibbonPiece" /> places it and keeps it moving.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class RibbonStrike : MaskableGraphic
    {
        [SerializeField] private RibbonGraphic _ribbon;

        [Tooltip("How thick the stroke is, in canvas units.")]
        [SerializeField] [Min(0.1f)] private float _thickness = 2.2f;

        [Tooltip("How far the stroke runs past the writing at each end, in canvas units.")]
        [SerializeField] [Min(0f)] private float _overhang = 3f;

        [Tooltip("How much higher the stroke ends than it starts, in canvas units, like a quick pen stroke.")]
        [SerializeField] private float _rise = 1.5f;

        [Tooltip("Where the stroke crosses the letters, from their baseline (0) to their top (1).")]
        [SerializeField] [Range(0f, 1f)] private float _height = 0.38f;

        [Tooltip("Columns along the stroke: more bends it with the wave more smoothly.")]
        [SerializeField] [Range(2, 64)] private int _segments = 24;

        private float _from;
        private float _to;
        private float _y;
        private float _dissolve;

        /// <summary>How far the ribbon has crumbled away (0 to 1): the stroke goes with it.</summary>
        internal float Dissolve
        {
            get => _dissolve;
            set
            {
                _dissolve = value;
                SetVerticesDirty();
            }
        }

        /// <summary>
        ///     Strikes from <paramref name="from" /> to <paramref name="to" /> across letters standing on
        ///     <paramref name="baseline" /> and reaching <paramref name="top" />.
        /// </summary>
        internal void Strike(float from, float to, float baseline, float top)
        {
            _from = from - _overhang;
            _to = to + _overhang;
            _y = Mathf.Lerp(baseline, top, _height);
            _dissolve = 0f;
            SetVerticesDirty();
        }

        /// <summary>The wave moved on: bend the stroke again.</summary>
        internal void Refresh()
        {
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_to <= _from || _ribbon == null)
            {
                return;
            }

            var halfWidth = _ribbon.rectTransform.rect.width * 0.5f;
            var halfThickness = _thickness * 0.5f;
            for (var i = 0; i <= _segments; i++)
            {
                var t = (float)i / _segments;
                var x = Mathf.Lerp(_from, _to, t);
                var y = _y + (t - 0.5f) * _rise + _ribbon.WaveAt(x);
                Color32 tint = color;
                tint.a = (byte)(tint.a * (1f - RibbonGraphic.DissolvedAt(x, halfWidth, _dissolve)));
                vh.AddVert(new Vector3(x, y + halfThickness, 0f), tint, Vector2.zero);
                vh.AddVert(new Vector3(x, y - halfThickness, 0f), tint, Vector2.zero);
            }

            for (var i = 0; i < _segments; i++)
            {
                var a = i * 2;
                vh.AddTriangle(a, a + 2, a + 3);
                vh.AddTriangle(a, a + 3, a + 1);
            }
        }
    }
}
