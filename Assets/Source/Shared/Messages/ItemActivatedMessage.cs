#region

using BalloonParty.Balloon.Model;

#endregion

namespace BalloonParty.Shared.Messages
{
    public readonly struct ItemActivatedMessage
    {
        public readonly IBalloonModel Balloon;

        public ItemActivatedMessage(IBalloonModel balloon)
        {
            Balloon = balloon;
        }
    }
}
