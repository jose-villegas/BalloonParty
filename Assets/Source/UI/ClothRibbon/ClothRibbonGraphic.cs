using UnityEngine;
using UnityEngine.UI;

namespace BalloonParty.UI.ClothRibbon
{
    /// <summary>
    ///     Draws a <see cref="RibbonCloth" />'s ribbon: its three rows of points as a strip, notched at both ends,
    ///     seen from whichever side faces the screen. It feeds <c>BalloonParty/Sprite/CelRibbon</c> what
    ///     <c>RibbonGraphic</c> does (how far each fold turns, the row, and a copy of the strip for the drop shadow),
    ///     the turn read from the cloth's normal. The cloth is simulated in this graphic's own local space.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class ClothRibbonGraphic : MaskableGraphic
    {
        private const int Rows = 3;

        [Tooltip("How strongly a fold's sideways lean shades it: the cloth's normal leans little for a deep fold.")]
        [SerializeField] [Min(0f)] private float _foldShading = 2.5f;

        private RibbonCloth _cloth;

        internal void Draw(RibbonCloth cloth)
        {
            _cloth = cloth;
            SetVerticesDirty();
        }

        /// <summary>
        ///     The shading's turn for a fold with unit normal <paramref name="normal" />: its lean to the right as seen
        ///     from the side facing the screen, scaled by <paramref name="gain" /> and kept to -1..1.
        /// </summary>
        internal static float Turn(Vector3 normal, float gain)
        {
            var seen = normal.z < 0f ? -normal : normal;
            return Mathf.Clamp(seen.x * gain, -1f, 1f);
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_cloth == null)
            {
                return;
            }

            // The shadow's copy first, so it draws under the ribbon; BalloonParty/Sprite/CelRibbon moves it.
            AddStrip(vh, true);
            AddStrip(vh, false);
        }

        private void AddStrip(VertexHelper vh, bool shadow)
        {
            var columns = _cloth.Columns;
            var first = vh.currentVertCount;
            Color32 tint = color;
            for (var column = 0; column < columns; column++)
            {
                var turn = Turn(_cloth.Normal((float)column / (columns - 1)), _foldShading);
                for (var row = 0; row < Rows; row++)
                {
                    var point = _cloth.ClothPoint(column, row);
                    var shading = shadow ? -1f : row == 1 ? 1f : 0f;
                    vh.AddVert(new Vector3(point.x, point.y, 0f), tint, new Vector2(turn, shading));
                }
            }

            for (var column = 0; column < columns - 1; column++)
            {
                var a = first + column * Rows;
                var b = a + Rows;
                vh.AddTriangle(a, b, b + 1);
                vh.AddTriangle(a, b + 1, a + 1);
                vh.AddTriangle(a + 1, b + 1, b + 2);
                vh.AddTriangle(a + 1, b + 2, a + 2);
            }
        }
    }
}
