using UnityEngine;

namespace BalloonParty.Display
{
    /// <summary>
    ///     The one writer of a camera's culling mask: a base mask (navigation's, or the camera's own) less the
    ///     layers claimed away from it by an effects map that draws them instead. A new base keeps the claims, and
    ///     a released claim never brings back a layer the base has dropped meanwhile.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    internal class CameraCullingMask : MonoBehaviour
    {
        private Camera _camera;
        private int _base;
        private int _claimed;

        internal static CameraCullingMask Of(Camera camera)
        {
            var owner = camera.GetComponent<CameraCullingMask>();
            return owner != null ? owner : camera.gameObject.AddComponent<CameraCullingMask>();
        }

        internal void SetBase(int mask)
        {
            EnsureCamera();
            _base = mask;
            Apply();
        }

        internal void Claim(int layers)
        {
            EnsureCamera();
            _claimed |= layers;
            Apply();
        }

        internal void Release(int layers)
        {
            EnsureCamera();
            _claimed &= ~layers;
            Apply();
        }

        // Lazily, since the first caller may run before this component's own Awake (it's added on demand).
        private void EnsureCamera()
        {
            if (_camera != null)
            {
                return;
            }

            _camera = GetComponent<Camera>();
            _base = _camera.cullingMask;
        }

        private void Apply()
        {
            _camera.cullingMask = _base & ~_claimed;
        }
    }
}
