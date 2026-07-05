namespace BalloonParty.Balloon.Type
{
    public enum BalloonType
    {
        Simple,
        Tough,
        Unbreakable,
        BubbleCluster,

        // Same model/behavior as Simple — distinct IDs only so the level-range type gate
        // (BalloonTypeWeight[]) can introduce/withhold each higher-score skin independently.
        SimpleSilver,
        SimpleGold
    }
}
