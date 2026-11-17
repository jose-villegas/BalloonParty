using UnityEngine;

namespace BalloonParty.UI.Score
{
    /// <summary>
    ///     Draws one score shape as a single mesh: the shape's baked wire mesh with the ink shader
    ///     (<c>BalloonParty/Sprite/ShapeInk</c>), which places the pens and paints the ink behind them from the
    ///     numbers written here each frame. Stands in for a pooled trail per pen. Built and pooled in code by
    ///     <c>ShapeInkPool</c>; it has no prefab.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    internal sealed class ShapeInkView : MonoBehaviour
    {
        // The widest the baked mesh reaches from its centre, in its own units (the waving sphere's swell).
        private const float MeshReach = 1.5f;

        // Room in the bounds for the drop shadow, which is drawn off to one side of the ink, in world units.
        private const float ShadowRoom = 0.2f;

        private static readonly int InkColorId = Shader.PropertyToID("_InkColor");
        private static readonly int InkPenId = Shader.PropertyToID("_InkPen");
        private static readonly int InkSizeId = Shader.PropertyToID("_InkSize");
        private static readonly int InkWaveId = Shader.PropertyToID("_InkWave");

        private MeshFilter _filter;
        private MeshRenderer _renderer;
        private MaterialPropertyBlock _block;
        private float _ribbonHalfWidth;

        private void Awake()
        {
            _filter = GetComponent<MeshFilter>();
            _renderer = GetComponent<MeshRenderer>();
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            _renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            _block = new MaterialPropertyBlock();
        }

        /// <summary>
        ///     Takes the shape to draw. <paramref name="layers" /> are the materials for the mesh's submeshes, in
        ///     the order they draw: the shadow, the far half, the near half. <paramref name="size" /> is the mesh ribbon's half-width (x) and the ink's
        ///     half-width at its widest (y) and at the pen (z), in world units. The ink sorts where
        ///     <paramref name="sortLike" />'s ribbon does.
        /// </summary>
        internal void Show(Mesh mesh, Material[] layers, Color color, Vector3 size, FlyingTrail sortLike)
        {
            _filter.sharedMesh = mesh;
            _renderer.sharedMaterials = layers;
            sortLike.CopyRibbonSorting(_renderer);
            _ribbonHalfWidth = size.x;
            _block.SetColor(InkColorId, color);
            _block.SetVector(InkSizeId, size);
            gameObject.SetActive(true);
        }

        internal void Apply(in ShapeInkFrame frame)
        {
            transform.SetPositionAndRotation(frame.Center, frame.Rotation);
            transform.localScale = Vector3.one * frame.Radius;
            _block.SetVector(InkPenId, new Vector4(frame.Travelled, frame.Coverage, frame.Radius, frame.Fade));
            _block.SetVector(InkWaveId, new Vector4(frame.WaveAmplitude, frame.WaveClock, 0f, 0f));
            _renderer.SetPropertyBlock(_block);

            // The ribbon keeps its world width however small the shape is, so the mesh's own bounds, which
            // shrink with it, would let a shape at the screen's edge be culled while its ink still shows.
            var reach = frame.Radius * MeshReach + _ribbonHalfWidth + ShadowRoom;
            _renderer.bounds = new Bounds(frame.Center, Vector3.one * (2f * reach));
        }

        internal void Hide()
        {
            gameObject.SetActive(false);
            _filter.sharedMesh = null;
        }
    }
}
