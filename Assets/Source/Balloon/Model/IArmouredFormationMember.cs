namespace BalloonParty.Balloon.Model
{
    /// <summary>
    ///     A formation member with armour: a tough balloon. Toughs only ever walk in a snake of their own, never
    ///     with unbreakables.
    /// </summary>
    internal interface IArmouredFormationMember : IWriteableFormationMember
    {
    }
}
