using UnityEngine;

namespace BalloonParty.Game.Cinematics
{
    /// <summary>
    ///     The one scene anchor of the cinematic camera: a thin View holding the <see cref="Camera" />
    ///     reference the shared <see cref="CinematicCameraRig" /> drives, so producers never carry
    ///     their own serialized camera refs. Wire <c>_camera</c> in the inspector; falls back to
    ///     <c>Camera.main</c>.
    /// </summary>
    internal class CinematicCameraView : MonoBehaviour
    {
        [SerializeField] private Camera _camera;

        // Lazy fallback rather than Awake: the shared rig is constructed at container build (before any
        // component's Awake), so the resolution must happen on first read.
        public Camera Camera => _camera != null ? _camera : _camera = Camera.main;
    }
}
