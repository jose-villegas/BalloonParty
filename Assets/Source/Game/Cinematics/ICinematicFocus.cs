using UnityEngine;

namespace BalloonParty.Game.Cinematics
{
    /// <summary>
    ///     What the cinematic camera frames this tick — one point (a tracked trail) or a spread (the
    ///     heart trails' bounding box); <see cref="CinematicCameraRig.Frame" /> pans toward
    ///     <paramref name="center" /> and keeps [min,max] in frustum. Return false when there is
    ///     currently nothing to frame (the rig holds its position).
    /// </summary>
    internal interface ICinematicFocus
    {
        bool TryGetFocus(out Vector3 center, out Vector3 min, out Vector3 max);
    }
}
