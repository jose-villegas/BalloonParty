using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    /// <summary>
    ///     An overflow balloon is ready to be drained: a heart trail should fly to it and pop it on
    ///     arrival. <see cref="TargetPosition" /> is the balloon's frozen resting position (known now, so
    ///     the trail can aim there for its whole flight); <see cref="RequestId" /> identifies which
    ///     balloon to pop when the trail lands (see <c>RejectedBalloonEffect.OnHeartArrived</c>).
    /// </summary>
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
