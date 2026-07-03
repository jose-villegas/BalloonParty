using BalloonParty.Configuration;
using UnityEngine;
using VContainer;

namespace BalloonParty.Display
{
    /// <summary>
    ///     Shared low-res capture of the scene's visuals, bound globally as
    ///     <c>_SceneCaptureTex</c>: a scheduled pass at the main camera's own framing, so any
    ///     effect that wants "what the screen roughly looks like" (the Unbreakable's chrome
    ///     reflection is the first consumer) reads this instead of a GrabPass — whose mid-frame
    ///     framebuffer resolve stalls tile GPUs. Renders only while at least one consumer holds
    ///     an <see cref="Acquire"/>, and only every Nth frame — skipped frames keep the
    ///     previous capture. One shared target on purpose: a second consumer with different
    ///     mask/resolution needs is the moment to generalize further, not before.
    ///     Lives on the main camera so position (camera shake, cinematic pans) is inherited;
    ///     orthographic size is copied every frame so cinematic zooms stay in sync.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class SceneCaptureService : MonoBehaviour
    {
        private static readonly int CaptureTexId = Shader.PropertyToID("_SceneCaptureTex");

        [Tooltip("Layers the capture renders. Consumers sampling the capture should exclude " +
                 "their own layer to avoid feedback (e.g. the Unbreakable's chrome).")]
        [SerializeField] private LayerMask _capturedLayers;

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
            _captureCamera.clearFlags = _mainCamera.clearFlags;
            _captureCamera.backgroundColor = _mainCamera.backgroundColor;

            // Lower depth renders before the main camera, so the capture is ready when
            // consumers sample it in the same frame.
            _captureCamera.depth = _mainCamera.depth - 1f;
            _captureCamera.enabled = false;
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

            // No depth buffer — sprites only. Bilinear filtering smooths the low resolution
            // before the convex warp smears it further.
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
