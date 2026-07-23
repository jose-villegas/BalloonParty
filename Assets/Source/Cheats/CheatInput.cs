#if UNITY_EDITOR || DEVELOPMENT_BUILD || CHEATS_IN_RELEASE

using UnityEngine;

namespace BalloonParty.Cheats
{
    /// <summary>Shared pointer helpers for the dev cheats, so each one needn't reimplement them.</summary>
    internal static class CheatInput
    {
        /// <summary>The pointer's world position via <see cref="Camera.main"/>, or null if there's no main
        /// camera. 2D orthographic: XY is correct regardless of the z input. The first touch maps to the
        /// mouse position, so this works on device too.</summary>
        public static Vector3? MouseWorldPosition()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                return null;
            }

            var world = cam.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, 0f));
            world.z = 0f;
            return world;
        }
    }
}
#endif
