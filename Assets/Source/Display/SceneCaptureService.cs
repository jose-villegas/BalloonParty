using BalloonParty.Configuration;
using BalloonParty.Shared.Cadence;
using BalloonParty.Shared.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using VContainer;

namespace BalloonParty.Display
{
    /// <summary>Shared low-res scene capture, bound globally as <c>_SceneCaptureTex</c>.</summary>
    [RequireComponent(typeof(Camera))]
    public class SceneCaptureService : MonoBehaviour, ICadencedEffect
    {
        private static readonly int CaptureTexId = Shader.PropertyToID("_SceneCaptureTex");

        [Tooltip("Layers the capture renders. Consumers sampling the capture should exclude " +
                 "their own layer to avoid feedback (e.g. the Unbreakable's chrome).")]
        [SerializeField] private LayerMask _capturedLayers;

        [Header("Background")]
        [Tooltip("Clear to the main camera's own background color. Disable to author a " +
                 "specific fill instead — the main camera's backgroundColor is only what's " +
                 "actually visible when it clears with Solid Color; on Skybox (or any other) " +
                 "clear mode it isn't, and the capture would inherit whatever fallback that " +
                 "leaves behind.")]
        [SerializeField] private bool _matchMainCameraBackground = true;

        [Tooltip("Used when \"Match Main Camera Background\" is off.")]
        [SerializeField] private Color _backgroundColor = Color.black;

        [Inject] private IGameDisplayConfiguration _displayConfig;

        private Camera _mainCamera;
        private Camera _captureCamera;
        private RenderTexture _texture;
        private int _consumers;
        private float _captureAccumulator;

        internal RenderTexture CaptureTexture => _texture;

        // Bumped once per unique capture; consumers gate rebuild work on this instead of
        // re-deriving from their own cadence.
        internal int ContentVersion { get; private set; }

        private void Awake()
        {
            _mainCamera = GetComponent<Camera>();

            if (_capturedLayers.value == 0)
            {
                Log.Warn("SceneCapture", "captured layers mask is empty — " +
                                 "consumers will sample nothing.", this);
            }

            CreateCaptureCamera();
        }

        private void LateUpdate()
        {
            // The capture camera renders during the render phase, i.e. after last frame's
            // LateUpdate — so "enabled" here (before this frame's toggle below) reflects last
            // frame's decision and its pixels are new as of now. Stamping at the start (rather
            // than alongside the render-trigger below) keeps the signal correct regardless of
            // LateUpdate ordering between this component and its consumers; worst case a
            // consumer sees a new version one frame late, the same staleness the smear already
            // tolerates when reading CaptureTexture.
            if (_captureCamera.enabled)
            {
                ContentVersion++;
            }

            _captureAccumulator += Time.unscaledDeltaTime;

            // SceneCaptureFrameInterval was authored as "every N frames at 60 fps"; reinterpreted
            // here as seconds so capture cadence (and the GI chain it feeds) doesn't scale with
            // display refresh — a 120 Hz panel would otherwise double this chain's GPU cost.
            // Unscaled so cadence keeps refreshing during pause/slow-mo, same as the old frame count did.
            var captureInterval = _displayConfig.SceneCaptureFrameInterval / 60f;
            var shouldRender = _consumers > 0 && _captureAccumulator >= captureInterval;

            if (shouldRender)
            {
                EnsureTexture();
                _captureCamera.orthographicSize = _mainCamera.orthographicSize;

                // Re-matched per capture, not just at setup: the main camera's background is now
                // live-tinted by the scene light (CameraBackgroundTint), and the GI measures its
                // ambient against this clear colour — a stale value would break its neutrality.
                ApplyBackgroundColor();
                _captureAccumulator -= captureInterval;
            }

            _captureCamera.enabled = shouldRender;
        }

        private void OnDestroy()
        {
            if (_texture != null)
            {
                _texture.Release();
                Destroy(_texture);
            }
        }

        int ICadencedEffect.BlitWeight => 3;

        void ICadencedEffect.ApplyPhaseOffset(float offset01)
        {
            var interval = _displayConfig.SceneCaptureFrameInterval / 60f;
            _captureAccumulator = offset01 * interval;
        }

        internal void Acquire()
        {
            _consumers++;
        }

        internal void Release()
        {
            _consumers = Mathf.Max(0, _consumers - 1);
        }

        private void CreateCaptureCamera()
        {
            var go = new GameObject("SceneCaptureCamera");
            go.transform.SetParent(transform, false);

            _captureCamera = go.AddComponent<Camera>();
            _captureCamera.orthographic = true;
            _captureCamera.cullingMask = _capturedLayers;

            // Deterministic solid-color clear, regardless of the main camera's clear mode.
            _captureCamera.clearFlags = CameraClearFlags.SolidColor;
            ApplyBackgroundColor();

            // Lower depth renders before the main camera, so it's ready the same frame.
            _captureCamera.depth = _mainCamera.depth - 1f;
            _captureCamera.enabled = false;

            // Runtime-created cameras carry no serialized URP data; GetUniversalAdditionalCameraData adds it.
            var cameraData = _captureCamera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = false;
        }

        private void ApplyBackgroundColor()
        {
            var background = _matchMainCameraBackground ? _mainCamera.backgroundColor : _backgroundColor;

            // Alpha zeroed so it doubles as a sprite-coverage mask for ScreenSpaceLightService.
            background.a = 0f;
            _captureCamera.backgroundColor = background;
        }

        private void EnsureTexture()
        {
            var downscale = Mathf.Max(2, _displayConfig.SceneCaptureDownscale);
            var width = Mathf.Max(1, Screen.width / downscale);
            var height = Mathf.Max(1, Screen.height / downscale);

            if (_texture != null && _texture.width == width && _texture.height == height)
            {
                return;
            }

            if (_texture != null)
            {
                _texture.Release();
                Destroy(_texture);
            }

            // URP's RenderGraph rejects depthless camera output textures; 2D content never
            // samples the depth, but the attachment still has to exist.
            _texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = "SceneCapture",
                filterMode = FilterMode.Bilinear,
                useMipMap = true,
                // Auto, never a script-side GenerateMips(): that command can reach the gfx worker
                // outside the frame's command buffer and crashes Vulkan release builds (null
                // VkCommandBuffer in GenerateRenderSurfaceMips). Costs nothing extra — the capture
                // camera only renders on cadence, so auto-mips fire at the same rate.
                autoGenerateMips = true
            };

            _captureCamera.targetTexture = _texture;
            Shader.SetGlobalTexture(CaptureTexId, _texture);
        }
    }
}
