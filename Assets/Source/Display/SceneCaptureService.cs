using BalloonParty.Configuration;
using UnityEngine;
using VContainer;

namespace BalloonParty.Display
{
    /// <summary>Shared low-res scene capture, bound globally as <c>_SceneCaptureTex</c>.</summary>
    [RequireComponent(typeof(Camera))]
    public class SceneCaptureService : MonoBehaviour
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
        private int _frameCounter;

        internal RenderTexture CaptureTexture => _texture;

        private void Awake()
        {
            _mainCamera = GetComponent<Camera>();

            if (_capturedLayers.value == 0)
            {
                Debug.LogWarning("SceneCaptureService: captured layers mask is empty — " +
                                 "consumers will sample nothing.", this);
            }

            CreateCaptureCamera();
        }

        private void LateUpdate()
        {
            var shouldRender = _consumers > 0
                               && ++_frameCounter % _displayConfig.SceneCaptureFrameInterval == 0;

            if (shouldRender)
            {
                EnsureTexture();
                _captureCamera.orthographicSize = _mainCamera.orthographicSize;
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

            // No depth buffer needed — sprites only.
            _texture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
            {
                name = "SceneCapture",
                filterMode = FilterMode.Bilinear
            };

            _captureCamera.targetTexture = _texture;
            Shader.SetGlobalTexture(CaptureTexId, _texture);
        }
    }
}
