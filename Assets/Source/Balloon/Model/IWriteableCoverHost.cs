namespace BalloonParty.Balloon.Model
{
    /// <summary>
    ///     A balloon a cover formation can stand round and hold where it is, the side only
    ///     <c>UnbreakableFormationService</c> drives. Held, the balloon is anchored
    ///     (<c>IBalanceAnchor</c>) exactly as a formation's member is, so it stays inside its cover: the balance
    ///     race, a pressure shove, a snake's walk and the shot solver's pulses all leave it in its slot. Every
    ///     balloon model has it (<see cref="BalloonModelBase" />), since a cover's host may be a plain balloon
    ///     carrying an item or any of the reward balloons.
    /// </summary>
    internal interface IWriteableCoverHost
    {
        /// <summary>A cover stands round it now. One cover at most holds a balloon.</summary>
        bool HeldByCover { get; }

        void SetHeldByCover(bool held);
    }
}
