using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BalloonParty.UI.ClothRibbon
{
    /// <summary>
    ///     Draws a <see cref="RibbonCloth" />'s line as a thin strip through its points. The cloth is simulated in
    ///     this graphic's own local space.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class SpringLineGraphic : MaskableGraphic
    {
        [Tooltip("The line's thickness, in canvas units.")]
        [SerializeField] [Min(0.05f)] private float _width = 0.7f;

        private readonly List<Vector2> _points = new();

        internal void Draw(RibbonCloth cloth)
        {
            _points.Clear();
            for (var i = 0; i < cloth.LinePoints; i++)
            {
                _points.Add(cloth.LinePoint(i));
            }

            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_points.Count < 2)
            {
                return;
            }

            var half = _width * 0.5f;
            Color32 tint = color;
            for (var i = 0; i < _points.Count; i++)
            {
                var ahead = _points[Mathf.Min(i + 1, _points.Count - 1)];
                var behind = _points[Mathf.Max(i - 1, 0)];
                var along = ahead - behind;
                var side = along.sqrMagnitude > 1e-10f ? new Vector2(-along.y, along.x).normalized * half : Vector2.right * half;
                vh.AddVert(_points[i] + side, tint, Vector4.zero);
                vh.AddVert(_points[i] - side, tint, Vector4.zero);
                if (i == 0)
                {
                    continue;
                }

                var v = 2 * i;
                vh.AddTriangle(v - 2, v, v + 1);
                vh.AddTriangle(v - 2, v + 1, v - 1);
            }
        }
    }
}
