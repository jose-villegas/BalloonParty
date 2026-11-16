namespace BalloonParty.Balloon.Model
{
    /// <summary>
    ///     A formation member with armour: a tough balloon. Toughs only ever form snakes of their own, never with
    ///     unbreakables, and a tough snake runs head to tail from the most <see cref="Armour" /> to the least.
    /// </summary>
    internal interface IArmouredFormationMember : IWriteableFormationMember
    {
        /// <summary>Hits it still takes before it pops.</summary>
        int Armour { get; }
    }
}
