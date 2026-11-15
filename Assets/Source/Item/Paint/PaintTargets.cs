using BalloonParty.Configuration.Palette;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Capabilities;

namespace BalloonParty.Item.Paint
{
    internal enum PaintVerdict
    {
        // Nothing to paint: not a balloon, or already the paint's colour.
        Ignore,

        // Takes the colour, with the drip.
        Accept,

        // Keeps its colour; the drip still plays and slides off.
        Reject
    }

    /// <summary>
    ///     What paint does to one actor, shared by the Paint item and the disco ball's wave (PLAN-DiscoBalloon) so
    ///     the two agree on who is immune: an <see cref="IResistsPaint" /> balloon, and a rainbow balloon under
    ///     concrete paint.
    /// </summary>
    internal static class PaintTargets
    {
        internal static PaintVerdict Classify(
            ISlotActor actor, string paintColor, IGamePalette palette, out IPaintable paintable)
        {
            paintable = null;
            if (actor is IPaintable candidate)
            {
                if (candidate.Color.Value == paintColor)
                {
                    return PaintVerdict.Ignore;
                }

                // A rainbow balloon's identity is the wildcard; only rainbow paint matches it, above.
                if (palette.IsRainbow(candidate.Color.Value))
                {
                    return PaintVerdict.Reject;
                }

                paintable = candidate;
                return PaintVerdict.Accept;
            }

            return actor is IResistsPaint ? PaintVerdict.Reject : PaintVerdict.Ignore;
        }
    }
}
