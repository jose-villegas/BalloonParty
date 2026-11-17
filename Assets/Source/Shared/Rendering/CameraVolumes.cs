using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BalloonParty.Shared.Rendering
{
    internal static class CameraVolumes
    {
        /// <summary>
        ///     Stops URP rebuilding the volume stack of a camera that post-processes nothing, which never reads it.
        ///     The switch builds the stack once, so it needs the volume manager, which isn't up yet during an
        ///     <c>Awake</c>: false until it is, so call it again before each render until it says done.
        /// </summary>
        internal static bool TryFreeze(Camera camera)
        {
            if (!VolumeManager.instance.isInitialized)
            {
                return false;
            }

            camera.SetVolumeFrameworkUpdateMode(VolumeFrameworkUpdateMode.ViaScripting);
            return true;
        }
    }
}
