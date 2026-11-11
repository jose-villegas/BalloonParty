using UnityEngine;

namespace BalloonParty.UI
{
    /// <summary>
    ///     Keeps a World Space canvas over the main camera's view, sized and scaled the way a Screen Space canvas
    ///     with a <see cref="UnityEngine.UI.CanvasScaler" /> would be (scale with screen size, matching between
    ///     width and height), so a layout authored for the game canvas lands in the same place on screen. It is
    ///     world geometry all the same: the main camera draws it among the board's sprites, sorted by its sorting
    ///     layer, so it sits under the screen-space light and goes through the same post-processing, and cameras
    ///     that render its layer (the light's scene capture) see it. Follows the camera's zoom and pan every frame.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    internal sealed class CameraFittedCanvas : MonoBehaviour
    {
        [Tooltip("The layout's reference resolution. Must match the game canvas's scaler; the card reveal's " +
                 "builder copies it from there.")]
        [SerializeField] private Vector2 _referenceResolution = new(225f, 400f);

        [Tooltip("0 matches the width, 1 the height. Must match the game canvas's scaler; the card reveal's " +
                 "builder copies it from there.")]
        [SerializeField] [Range(0f, 1f)] private float _matchWidthOrHeight = 0.5f;

        [Tooltip("How far in front of the camera it sits, in world units.")]
        [SerializeField] [Min(0f)] private float _distance = 10f;

        private RectTransform _rect;
        private Camera _camera;

        private void Awake()
        {
            _rect = (RectTransform)transform;
        }

        private void OnEnable()
        {
            Canvas.preWillRenderCanvases += Fit;
        }

        private void OnDisable()
        {
            Canvas.preWillRenderCanvases -= Fit;
        }

        /// <summary>The scaler's settings this canvas fits by, set by the builder from the game canvas's.</summary>
        internal void MatchScaler(Vector2 referenceResolution, float matchWidthOrHeight)
        {
            _referenceResolution = referenceResolution;
            _matchWidthOrHeight = matchWidthOrHeight;
        }

        // Just before the canvases lay out for this frame's render, after every script (a cinematic's zoom, the
        // camera shake) has moved the camera, as HudCamera mirrors it: never a frame behind the view.
        private void Fit()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
                if (_camera == null)
                {
                    return;
                }
            }

            var size = CanvasSize(new Vector2(_camera.pixelWidth, _camera.pixelHeight), _referenceResolution,
                _matchWidthOrHeight);
            var worldWidth = 2f * _camera.orthographicSize * _camera.aspect;
            var scale = worldWidth / size.x;
            if (_rect.sizeDelta != size)
            {
                _rect.sizeDelta = size;
            }

            _rect.localScale = new Vector3(scale, scale, 1f);
            var view = _camera.transform;
            _rect.SetPositionAndRotation(view.position + view.forward * _distance, view.rotation);
        }

        /// <summary>
        ///     The canvas's size in canvas units on a screen <paramref name="screen" /> pixels big, as a canvas scaler
        ///     set to scale with screen size and match <paramref name="match" /> between width and height sizes it.
        /// </summary>
        internal static Vector2 CanvasSize(Vector2 screen, Vector2 reference, float match)
        {
            var logWidth = Mathf.Log(screen.x / reference.x, 2f);
            var logHeight = Mathf.Log(screen.y / reference.y, 2f);
            var factor = Mathf.Pow(2f, Mathf.Lerp(logWidth, logHeight, match));
            return screen / factor;
        }
    }
}
