namespace BalloonParty.Game.Health
{
    /// <summary>Hit-point charges already committed but not yet applied — nothing cancels one except a run reset.</summary>
    internal interface IPendingHealthCharges
    {
        int PendingCharges { get; }
    }
}
