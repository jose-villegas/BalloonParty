namespace BalloonParty.Balloon.Type
{
    public enum BalloonType
    {
        Simple,
        Tough,
        Unbreakable,
        BubbleCluster,

        // Same as Simple — distinct IDs so the level-range type gate can withhold each skin independently.
        SimpleSilver,
        SimpleGold
    }
}
