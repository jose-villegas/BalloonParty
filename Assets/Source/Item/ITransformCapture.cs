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

    internal interface ITransformCapture
    {
        TransformSnapshot CaptureSnapshot();
    }
}
