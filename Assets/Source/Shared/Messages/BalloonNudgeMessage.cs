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
        public readonly float? NudgeDistance;
        public readonly float? NudgeDuration;

        public BalloonNudgeMessage(
            IBalloonModel balloon,
            Vector3 hitSlotPosition,
            float? nudgeDistance = null,
            float? nudgeDuration = null)
        {
            Balloon = balloon;
            HitSlotPosition = hitSlotPosition;
            NudgeDistance = nudgeDistance;
            NudgeDuration = nudgeDuration;
        }
    }
}
