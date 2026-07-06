namespace BalloonParty.Game.Health
{
    /// <summary>Whether the run's loss is already a certainty, ahead of the loss actually committing.</summary>
    internal interface ILossForecast
    {
        bool LossImminent { get; }
    }
}
