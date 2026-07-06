using UnityEngine;

namespace BalloonParty.Game.Cinematics
{
    /// <summary>
    ///     Thin View holding the <see cref="Camera" /> reference the shared <see cref="CinematicCameraRig" /> drives.
    /// </summary>
    internal class CinematicCameraView : MonoBehaviour
    {
        [SerializeField] private Camera _camera;

        // Lazy fallback, not Awake — the rig is constructed at container build, before any Awake.
        public Camera Camera => _camera != null ? _camera : _camera = Camera.main;
    }
}
