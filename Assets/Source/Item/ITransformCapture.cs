using UnityEngine;

namespace BalloonParty.Item
{
    public readonly struct TransformSnapshot
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly Vector3 LocalScale;

        public TransformSnapshot(Transform t)
        {
            Position = t.position;
            Rotation = t.rotation;
            LocalScale = t.localScale;
        }
    }

    /// <summary>
    ///     Implement on components whose transform state must be captured
    /// </summary>
    internal interface ITransformCapture
    {
        /// <summary>
        ///     Returns a readonly snapshot of the component's current transform.
        /// </summary>
        TransformSnapshot CaptureSnapshot();
    }
}



