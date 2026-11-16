using BalloonParty.Shared.Diagnostics;
using UnityEngine;

namespace BalloonParty.Balloon.View
{
    /// <summary>
    ///     A disc grown from a pop across the board, drawn as a quad centred on the pop and sized to the disc each
    ///     frame, so the shader only runs over the ground the disc covers. Its material reads the radius and the
    ///     progress pushed here. The disco ball's wave and the nuke's blast each draw one.
    /// </summary>
    internal abstract class GrowingDiscView : MonoBehaviour, IGrowingDiscView
    {
        // World units past the disc's front, so the antialiased edge never meets the quad's border.
        private const float EdgeMargin = 0.05f;

        private static readonly int RadiusId = Shader.PropertyToID("_Radius");
        private static readonly int ProgressId = Shader.PropertyToID("_Progress");

        private Mesh _quad;
        private MeshRenderer _renderer;
        private MaterialPropertyBlock _block;

        protected virtual void OnDestroy()
        {
            if (_quad != null)
            {
                Destroy(_quad);
            }
        }

        public virtual void Show(Vector3 center)
        {
            transform.position = center;
            SetRadius(0f, 0f);
            _renderer.enabled = true;
        }

        public virtual void SetRadius(float radius, float progress)
        {
            var size = (radius + EdgeMargin) * 2f;
            transform.localScale = new Vector3(size, size, 1f);
            _block.SetFloat(RadiusId, radius);
            _block.SetFloat(ProgressId, progress);
            _renderer.SetPropertyBlock(_block);
        }

        public virtual void Hide()
        {
            _renderer.enabled = false;
        }

        // Called from the subclass's injection, not Awake: the settings arrive after AddComponent's Awake.
        protected void Build(Material material, string sortingLayerName, int sortingOrder, string owner)
        {
            _quad = BuildQuad(owner);
            gameObject.AddComponent<MeshFilter>().sharedMesh = _quad;
            _renderer = gameObject.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = material;
            if (material == null)
            {
                Log.Warn(owner, "No disc material on BalloonsConfiguration: the disc draws nothing.");
            }

            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.sortingLayerName = sortingLayerName;
            _renderer.sortingOrder = sortingOrder;
            _renderer.enabled = false;
            _block = new MaterialPropertyBlock();
        }

        private static Mesh BuildQuad(string owner)
        {
            var mesh = new Mesh { name = owner + "Quad" };
            mesh.SetVertices(new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f),
            });
            mesh.SetUVs(0, new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) });
            mesh.SetTriangles(new[] { 0, 2, 1, 2, 3, 1 }, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
