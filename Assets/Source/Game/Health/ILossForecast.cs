namespace BalloonParty.Game.Health
{
    /// <summary>
    ///     Whether the run's loss is already a certainty, ahead of the loss actually committing. True
    ///     from the moment the pending hit-point charges cover the remaining HP — at reject-queue time,
    ///     seconds before the Nth heart launch drives HP to 0 and <c>EndRun</c> fires. The level-up
    ///     ceremony gates on this (no level-up after a lost run); the loss itself keeps its late timing
    ///     so the heart-drain presentation still plays.
    /// </summary>
    internal interface ILossForecast
    {
        bool LossImminent { get; }
    }
}
