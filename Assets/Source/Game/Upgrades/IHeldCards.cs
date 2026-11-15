using BalloonParty.Game.Run;

namespace BalloonParty.Game.Upgrades
{
    /// <summary>The cards a saved run holds, written by the run save and restored when the run is resumed.</summary>
    internal interface IHeldCards
    {
        /// <summary>
        ///     Writes the cards a replay of the level being played starts with into <paramref name="snapshot" />:
        ///     those held when it began (a free card won on it goes back, as on its retry), and the bonus card of
        ///     the attempt when <paramref name="withBonus" />, since a loss ends the attempt the bonus was for.
        /// </summary>
        void RecordHeld(RunSnapshot snapshot, bool withBonus);
    }
}
