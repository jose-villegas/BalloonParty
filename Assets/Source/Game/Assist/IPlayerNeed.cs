namespace BalloonParty.Game.Assist
{
    /// <summary>How much trouble the player is in right now, 0 (none) to 1 (one mistake from losing the run).</summary>
    internal interface IPlayerNeed
    {
        float Need { get; }
    }
}
