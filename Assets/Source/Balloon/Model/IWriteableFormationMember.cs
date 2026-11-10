using BalloonParty.Slots.Actor;

namespace BalloonParty.Balloon.Model
{
    /// <summary>The side of a formation member that only <c>UnbreakableFormationService</c> drives.</summary>
    internal interface IWriteableFormationMember : IFormationMember, IWriteableDynamicSlotActor
    {
        void SetInFormation(bool inFormation);
    }
}
