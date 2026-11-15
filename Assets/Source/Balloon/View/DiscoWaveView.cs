using BalloonParty.Configuration.Balloons;
using BalloonParty.Shared.Diagnostics;
using UnityEngine;
using VContainer;

namespace BalloonParty.Balloon.View
{
    /// <summary>
    ///     Draws the disco ball's wave: a quad on the <c>BalloonParty/Sprite/DiscoWave</c> material, centred on the
    ///     pop and sized to the disc each frame, so the shader only runs over the ground the disc covers. The shader
    ///     draws the rainbow edge and the wash from the radius and progress pushed here.
    /// </summary>
    internal sealed class DiscoWaveView : MonoBehaviour, IDiscoWaveView
    {
        // World units past the disc's front, so the antialiased edge never meets the quad's border.
        private const float EdgeMargin = 0.05f;

        private static readonly int RadiusId = Shader.PropertyToID("_Radius");
        private static readonly int ProgressId = Shader.PropertyToID("_Progress");

        private Mesh _quad;
        private MeshRenderer _renderer;
        private MaterialPropertyBlock _block;

        private void OnDestroy()
        {
            if (_quad != null)
            {
                Destroy(_quad);
            }
        }

        // Built here, not in Awake: the settings arrive with the injection, after AddComponent's Awake.
        [Inject]
        internal void Construct(IBalloonsConfiguration balloons)
        {
            var settings = balloons.DiscoWave;
            _quad = BuildQuad();
            gameObject.AddComponent<MeshFilter>().sharedMesh = _quad;
            _renderer = gameObject.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = settings.DiscMaterial;
            if (settings.DiscMaterial == null)
            {
                Log.Warn("DiscoWaveView", "No disc material on BalloonsConfiguration's Disco Ball Wave: the wave draws nothing.");
            }

            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.sortingLayerName = settings.SortingLayerName;
            _renderer.sortingOrder = settings.SortingOrder;
            _renderer.enabled = false;
            _block = new MaterialPropertyBlock();
        }

        public void Show(Vector3 center)
        {
            transform.position = center;
            SetRadius(0f, 0f);
            _renderer.enabled = true;
        }

        public void SetRadius(float radius, float progress)
        {
            var size = (radius + EdgeMargin) * 2f;
            transform.localScale = new Vector3(size, size, 1f);
            _block.SetFloat(RadiusId, radius);
            _block.SetFloat(ProgressId, progress);
            _renderer.SetPropertyBlock(_block);
        }

        public void Hide()
        {
            _renderer.enabled = false;
        }

        private static Mesh BuildQuad()
        {
            var mesh = new Mesh { name = "DiscoWaveQuad" };
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
