using BalloonParty.Configuration;
using BalloonParty.Shared;
using BalloonParty.Shared.Diagnostics;
using UniRx;
using UnityEngine;
using UnityEngine.Rendering;
using VContainer;

namespace BalloonParty.Scenario.View
{
    /// <summary>
    ///     Renders the smoke field RT as animated smoke trails behind the cloud backdrop.
    ///     Place this on a GameObject with a <see cref="MeshRenderer" /> + <see cref="MeshFilter" />;
    ///     it creates a viewport-sized quad at startup and assigns the display material. The sorting
    ///     order is set below the Scenario Background so the paint layer sits behind the clouds. While the field
    ///     draws as ink it keeps the target's alpha: the scene capture reads alpha as the GI's casters, and flat ink
    ///     on the page would cast its own grey copy beside every stroke.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshFilter))]
    [DisallowMultipleComponent]
    internal sealed class SmokeFieldView : MonoBehaviour
    {
        private static readonly int CoverSrcBlendId = Shader.PropertyToID("_CoverSrcBlend");
        private static readonly int CoverDstBlendId = Shader.PropertyToID("_CoverDstBlend");

        [SerializeField] private Material _displayMaterial;

        [Tooltip("Sorting layer for the quad (should match the cloud backdrop's layer).")]
        [SortingLayerName]
        [SerializeField] private string _sortingLayerName = "Default";

        [Tooltip("Sorting order — set below the cloud backdrop (0) so paint renders behind it.")]
        [SerializeField] private int _sortingOrder = -1;

        private IGameDisplayConfiguration _display;
        private SmokeFieldService _service;
        private Mesh _quad;
        private Material _material;
        private System.IDisposable _inkingSubscription;

        private void Start()
        {
            if (_displayMaterial == null)
            {
                Log.Warn("SmokeFieldView", "disabled: no display material assigned.", this);
                enabled = false;
                return;
            }

            if (_display == null)
            {
                Log.Warn("SmokeFieldView", "disabled: display configuration not injected.", this);
                enabled = false;
                return;
            }

            BuildQuad();
            ConfigureRenderer();
            _inkingSubscription = _service?.Inking.Subscribe(SetInking);
        }

        private void OnDestroy()
        {
            _inkingSubscription?.Dispose();
            if (_quad != null)
            {
                Destroy(_quad);
            }

            if (_material != null)
            {
                Destroy(_material);
            }
        }

        [Inject]
        private void Construct(IGameDisplayConfiguration display, SmokeFieldService service)
        {
            _display = display;
            _service = service;
        }

        private void BuildQuad()
        {
            float halfW = _display.ReferenceWorldWidth * 0.5f;
            float halfH = _display.ReferenceWorldHeight * 0.5f;

            _quad = new Mesh
            {
                name = "SmokeFieldQuad",
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
            // An instance, since the alpha blend is render state a property block can't change.
            _material = new Material(_displayMaterial);
            var meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = _material;
            meshRenderer.sortingLayerName = _sortingLayerName;
            meshRenderer.sortingOrder = _sortingOrder;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
        }

        private void SetInking(bool inking)
        {
            _material.SetFloat(CoverSrcBlendId, (float)(inking ? BlendMode.Zero : BlendMode.One));
            _material.SetFloat(CoverDstBlendId, (float)(inking ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
        }
    }
}
