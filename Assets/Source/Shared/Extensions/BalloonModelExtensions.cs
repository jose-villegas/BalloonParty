using BalloonParty.Balloon.Model;
using BalloonParty.Slots.Capabilities;

namespace BalloonParty.Shared.Extensions
{
    internal static class BalloonModelExtensions
    {
        /// <summary>
        /// Returns the balloon's color ID if it implements <see cref="IHasColor"/>,
        /// or empty string otherwise.
        /// </summary>
        internal static string GetColorId(this IBalloonModel model)
        {
            return (model as IHasColor)?.Color.Value ?? "";
        }
    }
}

