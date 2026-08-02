using BalloonParty.Balloon.Type;

namespace BalloonParty.Game.Telemetry
{
    internal readonly struct BalloonTypeCount
    {
        public readonly BalloonType Type;
        public readonly int Count;

        public BalloonTypeCount(BalloonType type, int count)
        {
            Type = type;
            Count = count;
        }
    }
}
