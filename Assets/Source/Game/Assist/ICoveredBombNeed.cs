namespace BalloonParty.Game.Assist
{
    /// <summary>
    ///     How long a bomb has sat on the board under a formation's cover, 0 (none, or just covered) to
    ///     <see cref="Shared.IAssistSettings.CoveredBombNeedWeight" />: a need of its own that only the Snipe roll
    ///     reads, since a lance is what reaches a bomb the unbreakables shield.
    /// </summary>
    internal interface ICoveredBombNeed
    {
        float Need { get; }
    }
}
