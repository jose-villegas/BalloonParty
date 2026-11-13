using BalloonParty.Slots.Actor;
using UnityEngine;

namespace BalloonParty.Balloon.Model
{
    /// <summary>The side of a formation member that only <c>UnbreakableFormationService</c> drives.</summary>
    internal interface IWriteableFormationMember : IFormationMember, IWriteableDynamicSlotActor
    {
        void SetInFormation(bool inFormation);

        void SetFormationHeading(Vector2 heading);

        void SetWalking(bool walking);
    }
}
