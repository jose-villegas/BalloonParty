using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    /// <summary><see cref="RequestId" /> identifies which balloon to pop when the trail lands (see <c>RejectedBalloonEffect.OnHeartArrived</c>).</summary>
    public readonly struct OverflowHeartRequestedMessage
    {
        public readonly int RequestId;
        public readonly Vector3 TargetPosition;

        public OverflowHeartRequestedMessage(int requestId, Vector3 targetPosition)
        {
            RequestId = requestId;
            TargetPosition = targetPosition;
        }
    }
}
