using BalloonParty.Balloon.Model;
using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    public readonly struct BalloonNudgeMessage
    {
        public readonly IBalloonModel Balloon;
        public readonly Vector3 HitSlotPosition;
        public readonly NudgeType Source;
        public readonly float? NudgeDistance;
        public readonly float? NudgeDuration;

        public BalloonNudgeMessage(
            IBalloonModel balloon,
            Vector3 hitSlotPosition,
            NudgeType source,
            float? nudgeDistance = null,
            float? nudgeDuration = null)
        {
            Balloon = balloon;
            HitSlotPosition = hitSlotPosition;
            Source = source;
            NudgeDistance = nudgeDistance;
            NudgeDuration = nudgeDuration;
        }
    }
}
