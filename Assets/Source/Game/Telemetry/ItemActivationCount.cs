using BalloonParty.Configuration.Items;

namespace BalloonParty.Game.Telemetry
{
    internal readonly struct ItemActivationCount
    {
        public readonly ItemType Type;
        public readonly int Count;

        public ItemActivationCount(ItemType type, int count)
        {
            Type = type;
            Count = count;
        }
    }
}
