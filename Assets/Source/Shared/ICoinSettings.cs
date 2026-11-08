namespace BalloonParty.Shared
{
    /// <summary>When coins drop. Coins reward reaching the deep levels, where hearts start to go.</summary>
    public interface ICoinSettings
    {
        /// <summary>The first level whose pops can drop a coin.</summary>
        int CoinsFromLevel { get; }

        /// <summary>The chance, 0 to 1, that one pop drops a coin.</summary>
        float DropChance { get; }
    }
}
