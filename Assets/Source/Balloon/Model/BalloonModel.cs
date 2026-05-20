using BalloonParty.Slots;

namespace BalloonParty.Balloon.Model
{
    internal class BalloonModel : BalloonModelBase, IHasWriteableColor
    {
        public BalloonModel()
        {
            CanHoldItem = true;
        }
    }
}
