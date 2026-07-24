using BalloonParty.Shared.Disturbance;
using UnityEngine;

namespace BalloonParty.UI
{
    /// <summary>
    ///     Pokes the disturbance field where this object sits — wire <see cref="Stamp" /> to a UI Button's
    ///     onClick for a cloud ripple on press. General (drop on any button/object) and DI-free, so it works
    ///     even in the launcher, routing through <see cref="DisturbanceStampRequest" />.
    /// </summary>
    public class ButtonDisturbanceStamp : MonoBehaviour
    {
        [Tooltip("World-space radius of the stamp.")]
        [SerializeField] private float _radius = 0.6f;

        [Tooltip("Outward push: >0 shoves clouds away (a repel), <0 pulls them in.")]
        [SerializeField] private float _strength = 0.9f;

        [Tooltip("Seconds the stamp ramps over for a soft shockwave; 0 = instant single-frame pop.")]
        [Range(0f, 5f)]
        [SerializeField] private float _duration;

        [Tooltip("Camera the stamp position resolves against. Empty = Camera.main.")]
        [SerializeField] private Camera _camera;

        public void Stamp()
        {
            var renderCamera = _camera != null ? _camera : Camera.main;
            if (renderCamera == null)
            {
                return;
            }

            // Map this (possibly UI) transform to screen space canvas-aware, then back into the field's world
            // units — so x/y land correctly whether the canvas is Screen Space - Camera or Overlay.
            var canvas = GetComponentInParent<Canvas>();
            var uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            var screen = RectTransformUtility.WorldToScreenPoint(uiCamera, transform.position);
            var world = renderCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 0f));
            world.z = 0f;

            DisturbanceStampRequest.Enqueue(world, _radius, _strength, Vector2.zero, _duration);
        }
    }
}
