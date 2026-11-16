using BalloonParty.Balloon.Model;

namespace BalloonParty.Balloon.Controller
{
    /// <summary>
    ///     Pops one balloon outside the hit path: its pop VFX play (unless asked not to), its slot is freed and it
    ///     returns to its pool, but no hit is published, so nothing scores and no item fires.
    /// </summary>
    internal interface IBalloonPopper
    {
        /// <returns>False when the balloon isn't registered (already leaving the board).</returns>
        bool TryPopSingle(IBalloonModel model, bool playPopVfx = true);
    }
}
