using BalloonParty.Configuration;
using BalloonParty.Shared;
using BalloonParty.Shared.Diagnostics;
using UnityEngine;
using VContainer;

namespace BalloonParty.Scenario.View
{
    /// <summary>
    ///     Renders the painting field RT as animated smoke trails behind the cloud backdrop.
    ///     Place this on a GameObject with a <see cref="MeshRenderer" /> + <see cref="MeshFilter" />;
    ///     it creates a viewport-sized quad at startup and assigns the display material. The sorting
    ///     order is set below the Scenario Background so the paint layer sits behind the clouds.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshFilter))]
    [DisallowMultipleComponent]
    internal sealed class PaintingFieldView : MonoBehaviour
    {
        [SerializeField] private Material _displayMaterial;

        [Tooltip("Sorting layer for the quad (should match the cloud backdrop's layer).")]
        [SortingLayerName]
        [SerializeField] private string _sortingLayerName = "Default";

        [Tooltip("Sorting order — set below the cloud backdrop (0) so paint renders behind it.")]
        [SerializeField] private int _sortingOrder = -1;

        private IGameDisplayConfiguration _display;
        private Mesh _quad;

        private void Start()
        {
            if (_displayMaterial == null)
            {
                Log.Warn("PaintingFieldView", "disabled: no display material assigned.", this);
                enabled = false;
                return;
            }

            if (_display == null)
            {
                Log.Warn("PaintingFieldView", "disabled: display configuration not injected.", this);
                enabled = false;
                return;
            }

            BuildQuad();
            ConfigureRenderer();
        }

        private void OnDestroy()
        {
            if (_quad != null)
            {
                Destroy(_quad);
            }
        }

        [Inject]
        private void Construct(IGameDisplayConfiguration display)
        {
            _display = display;
        }

        private void BuildQuad()
        {
            float halfW = _display.ReferenceWorldWidth * 0.5f;
            float halfH = _display.ReferenceWorldHeight * 0.5f;

            _quad = new Mesh
            {
                name = "PaintingFieldQuad",
                vertices = new[]
                {
                    new Vector3(-halfW, -halfH, 0f),
                    new Vector3( halfW, -halfH, 0f),
                    new Vector3( halfW,  halfH, 0f),
                    new Vector3(-halfW,  halfH, 0f)
                },
                uv = new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(1f, 1f),
                    new Vector2(0f, 1f)
                },
                triangles = new[] { 0, 1, 2, 0, 2, 3 }
            };
            _quad.RecalculateBounds();

            GetComponent<MeshFilter>().sharedMesh = _quad;
        }

        private void ConfigureRenderer()
        {
            var meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = _displayMaterial;
            meshRenderer.sortingLayerName = _sortingLayerName;
            meshRenderer.sortingOrder = _sortingOrder;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
        }
    }
}
