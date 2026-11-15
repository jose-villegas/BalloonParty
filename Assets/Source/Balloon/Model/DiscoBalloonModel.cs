namespace BalloonParty.Balloon.Model
{
    /// <summary>
    ///     The disco ball reward balloon (PLAN-DiscoBalloon): a <see cref="PlatedBalloonModel" /> whose pop sends a
    ///     wave across the board that paints every paintable balloon rainbow.
    /// </summary>
    internal sealed class DiscoBalloonModel : PlatedBalloonModel
    {
        internal DiscoBalloonModel(BalloonModelConfig config)
            : base(config)
        {
        }
    }
}
