using BalloonParty.Balloon.Model;

namespace BalloonParty.Balloon.Type
{
    public interface IBalloonVariant
    {
        /// <param name="levelAllowedColorsMask">
        ///     The active level range's allowed-color gate (see
        ///     <c>IActiveLevelParameters.AllowedColorsMask</c>), same bit-per-color convention as
        ///     <c>PaletteColorMaskAttribute</c> fields. Only <see cref="ColorableBalloonVariant" />
        ///     uses it; other variants ignore the parameter.
        /// </param>
        void Initialize(IWriteableBalloonModel model, int levelAllowedColorsMask);
    }
}
