using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    public readonly struct ItemRotationCapturedMessage
    {
        public readonly Quaternion Rotation;

        public ItemRotationCapturedMessage(Quaternion rotation)
        {
            Rotation = rotation;
        }
    }
}
