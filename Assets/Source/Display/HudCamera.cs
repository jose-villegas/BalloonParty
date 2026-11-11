using UnityEngine;

namespace BalloonParty.Display
{
    /// <summary>
    ///     The camera the HUD renders through: an overlay in the main camera's URP stack with post-processing
    ///     off, so the canvases (the UI and Launch layers) and the score, heart and coin trails flying into them
    ///     (the Trail layer) draw over the finished, tonemapped frame in their authored colours. URP applies
    ///     post-processing per camera in a stack, so the main camera's tonemapper never touches them. It mirrors
    ///     the main camera's projection every frame, so the canvases keep the world positions the trails fly to,
    ///     through a cinematic zoom too. <c>CanvasCameraBinder</c> binds the canvases to it.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    internal sealed class HudCamera : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private Camera _mainCamera;

        internal Camera Camera => _camera;

        private void OnEnable()
        {
            MirrorMainCamera();
            Canvas.preWillRenderCanvases += MirrorMainCamera;
        }

        private void OnDisable()
        {
            Canvas.preWillRenderCanvases -= MirrorMainCamera;
        }

        // Just before the canvases lay out for this frame's render, after every script has zoomed the main
        // camera, so a Screen Space - Camera canvas never fits last frame's view.
        private void MirrorMainCamera()
        {
            _camera.orthographic = _mainCamera.orthographic;
            _camera.orthographicSize = _mainCamera.orthographicSize;
            _camera.fieldOfView = _mainCamera.fieldOfView;
            _camera.nearClipPlane = _mainCamera.nearClipPlane;
            _camera.farClipPlane = _mainCamera.farClipPlane;
        }
    }
}
