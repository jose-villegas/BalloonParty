using UnityEngine;

namespace BalloonParty.Scenario
{
    /// <summary>
    ///     Owns the shared cloud field: bakes one scrolling cloud-density map into a camera-sized RT once
    ///     per frame and publishes it — plus its world bounds — as global shader properties, so every
    ///     consumer reads the SAME map with a single tap (the BackgroundCloud backdrop, sprite drop-shadows,
    ///     the GI/light smear). This is pure RT plumbing: the cloud roll is tuned entirely on the blit
    ///     material (<see cref="_densityMaterial" />), and the world bounds are read straight from the
    ///     camera each frame — so the component is fully self-contained (no DI). The analogue of
    ///     <c>DisturbanceFieldResources</c> / <c>SceneLightFieldService</c> for the cloud field; the GPU
    ///     side is <c>Shaders/BalloonParty/Include/CloudField.cginc</c> (consumers) + <c>CloudFieldGen.cginc</c>
    ///     and the <c>CloudFieldDensity</c> blit (generation).
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class CloudFieldService : MonoBehaviour
    {
        private static readonly int DensityTexId = Shader.PropertyToID("_CloudDensityTex");
        private static readonly int BoundsMinId = Shader.PropertyToID("_CloudFieldBoundsMin");
        private static readonly int BoundsSizeId = Shader.PropertyToID("_CloudFieldBoundsSize");
        private static readonly int ActiveId = Shader.PropertyToID("_CloudFieldActive");

        [Tooltip("Blit material (BalloonParty/Display/CloudFieldDensity) — the cloud roll's tuning " +
            "surface: noise texture, scale, scroll, thresholds all live here.")]
        [SerializeField] private Material _densityMaterial;

        [Tooltip("Camera whose orthographic view the field covers. Defaults to Camera.main if unset.")]
        [SerializeField] private Camera _camera;

        [Tooltip("Density-RT resolution per world unit.")]
        [SerializeField] private float _texelsPerUnit = 12f;

        private RenderTexture _densityRT;
        private Rect _bounds;

        private void Start()
        {
            if (_densityMaterial == null)
            {
                Debug.LogWarning("CloudFieldService disabled: assign a density material " +
                    "(a material using the BalloonParty/Display/CloudFieldDensity shader) to _densityMaterial.", this);
                enabled = false;
                return;
            }

            if (_camera == null)
            {
                _camera = Camera.main;
            }

            if (_camera == null)
            {
                Debug.LogWarning("CloudFieldService disabled: no camera assigned and no Camera.main found.", this);
                enabled = false;
                return;
            }

            ResolveBounds();
            CreateRenderTexture();

            PushBoundsGlobals();
            Shader.SetGlobalTexture(DensityTexId, _densityRT);
            Shader.SetGlobalFloat(ActiveId, 1f);
            Bake();
        }

        private void LateUpdate()
        {
            // Bounds follow the camera each frame so the field tracks any camera move; the noise is
            // world-space, so the clouds stay put in the world as the view pans.
            ResolveBounds();
            PushBoundsGlobals();
            Bake();
        }

        private void OnDestroy()
        {
            // Disable the gate so consumers stop sampling a released RT once the field is gone.
            Shader.SetGlobalFloat(ActiveId, 0f);

            if (_densityRT != null)
            {
                _densityRT.Release();
                Destroy(_densityRT);
                _densityRT = null;
            }
        }

        // Renders the scrolling cloud density into the RT. The source texture is unused — the blit material
        // computes each texel from its world position (bounds globals) and the built-in clock.
        private void Bake()
        {
            if (_densityRT != null && _densityMaterial != null)
            {
                Graphics.Blit(Texture2D.whiteTexture, _densityRT, _densityMaterial);
            }
        }

        private void ResolveBounds()
        {
            var orthoSize = _camera.orthographicSize;
            var worldHeight = orthoSize * 2f;
            var worldWidth = worldHeight * _camera.aspect;
            var center = _camera.transform.position;
            _bounds = new Rect(center.x - worldWidth * 0.5f, center.y - worldHeight * 0.5f, worldWidth, worldHeight);
        }

        private void CreateRenderTexture()
        {
            var width = Mathf.Max(4, Mathf.RoundToInt(_bounds.width * _texelsPerUnit));
            var height = Mathf.Max(4, Mathf.RoundToInt(_bounds.height * _texelsPerUnit));
            var format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.R8)
                ? RenderTextureFormat.R8
                : RenderTextureFormat.ARGB32;

            _densityRT = new RenderTexture(width, height, 0, format)
            {
                name = "CloudDensity",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            _densityRT.Create();
        }

        private void PushBoundsGlobals()
        {
            Shader.SetGlobalVector(BoundsMinId, new Vector4(_bounds.xMin, _bounds.yMin, 0f, 0f));
            Shader.SetGlobalVector(BoundsSizeId, new Vector4(_bounds.width, _bounds.height, 0f, 0f));
        }
    }
}
