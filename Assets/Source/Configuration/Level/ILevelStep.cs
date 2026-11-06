namespace BalloonParty.Configuration.Level
{
    /// <summary>One step of a level-stepped value: it holds from <see cref="FromLevel" /> until the next step.</summary>
    internal interface ILevelStep
    {
        int FromLevel { get; }
    }
}
