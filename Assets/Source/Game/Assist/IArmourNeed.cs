namespace BalloonParty.Game.Assist
{
    /// <summary>
    ///     How much armour has stood on the board too long, 0 (none) to <see cref="Shared.IAssistSettings.ArmourNeedWeight" />:
    ///     a need of its own that only the Snipe roll reads, since a lance is what clears armour.
    /// </summary>
    internal interface IArmourNeed
    {
        float Need { get; }
    }
}
