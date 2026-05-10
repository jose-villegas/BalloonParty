#region

using BalloonParty.Balloon.Model;
using UnityEngine;

#endregion

namespace BalloonParty.Shared.Messages
{
    public readonly struct BalloonNudgeMessage
    {
        public readonly IBalloonModel Balloon;
        public readonly Vector3 HitSlotPosition;

        public BalloonNudgeMessage(IBalloonModel balloon, Vector3 hitSlotPosition)
        {
            Balloon = balloon;
            HitSlotPosition = hitSlotPosition;
        }
    }
}

