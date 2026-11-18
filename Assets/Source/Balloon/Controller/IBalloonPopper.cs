using BalloonParty.Balloon.Model;

namespace BalloonParty.Balloon.Controller
{
    /// <summary>
    ///     Takes one balloon off the board outside the hit path: popped (<see cref="TryPopSingle" />: its pop VFX play
    ///     unless asked not to) or quietly (<see cref="TryLeave" />). Its slot is freed and it returns to its pool, but
    ///     no hit is published, so nothing scores and no item fires.
    /// </summary>
    internal interface IBalloonPopper
    {
        /// <returns>False when the balloon isn't registered (already leaving the board).</returns>
        bool TryPopSingle(IBalloonModel model, bool playPopVfx = true);

        /// <summary>
        ///     Takes one balloon off the board with no pop at all: its slot and type count are freed at once and nothing
        ///     is published, while its view stays where it is for <paramref name="lingerSeconds" /> (for a look of its
        ///     own to play out) and then returns to its pool.
        /// </summary>
        /// <returns>False when the balloon isn't registered (already leaving the board).</returns>
        bool TryLeave(IBalloonModel model, float lingerSeconds);
    }
}
