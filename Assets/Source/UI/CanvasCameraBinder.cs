using UnityEngine;

namespace BalloonParty.UI
{
    /// <summary>
    ///     Binds a Screen Space - Camera canvas to the single persistent camera at runtime. That camera lives
    ///     in the app-root DontDestroyOnLoad prefab, so it can't be assigned to a canvas's Render Camera at
    ///     edit time; this resolves it via <see cref="Camera.main" /> once it exists. Without it, a
    ///     Screen Space - Camera canvas with an unset Render Camera falls back to overlay-scale world
    ///     positions, which sends world-space trails that target this canvas's rects (score/shield/health)
    ///     far off screen.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    internal sealed class CanvasCameraBinder : MonoBehaviour
    {
        private Canvas _canvas;

        private void Awake()
        {
            _canvas = GetComponent<Canvas>();
        }

        private void OnEnable()
        {
            TryBind();
        }

        private void Update()
        {
            // The persistent camera may not exist on the first enabled frame (it comes from the app root);
            // keep trying until it's bound, then stop paying for Update.
            TryBind();
        }

        private void TryBind()
        {
            var mainCamera = Camera.main;
            if (mainCamera == null)
            {
                // The persistent camera comes from the app root and may not exist yet — keep trying.
                return;
            }

            // Bind the ROOT canvas. A nested Canvas ignores its own render mode/camera and inherits the
            // root's, so a child reads ScreenSpaceOverlay even under a Screen Space - Camera root; binding
            // the root's worldCamera is what actually clears the overlay-scale fallback. A destroyed camera
            // reference compares == null, so this also re-binds a canvas whose old per-scene camera the
            // single-camera migration removed. Setting it on a genuine overlay root is a harmless no-op.
            var root = _canvas.rootCanvas;
            if (root.worldCamera == null)
            {
                root.worldCamera = mainCamera;
            }

            enabled = false;
        }
    }
}
