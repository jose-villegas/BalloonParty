namespace BalloonParty.Shared
{
    /// <summary>When coins drop, and how they fly to the counter. Coins reward reaching the deep levels.</summary>
    public interface ICoinSettings
    {
        /// <summary>The first level whose pops can drop a coin.</summary>
        int CoinsFromLevel { get; }

        /// <summary>The chance, 0 to 1, that one pop drops a coin.</summary>
        float DropChance { get; }

        /// <summary>Seconds a dropped coin takes to fly from the pop to the counter.</summary>
        float TrailDuration { get; }

        /// <summary>Coin trails pooled ahead of the first drop, so the first coins never instantiate mid-play.</summary>
        int TrailPrewarm { get; }
    }
}
