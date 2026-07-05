using BalloonParty.Balloon.Model;

namespace BalloonParty.Balloon.Type
{
    public interface IBalloonVariant
    {
        /// <param name="levelAllowedColorsMask">The active level's color gate (bit-per-color, same
        /// convention as PaletteColorMaskAttribute). Only <see cref="ColorableBalloonVariant" /> uses it.</param>
        void Initialize(IWriteableBalloonModel model, int levelAllowedColorsMask);
    }
}
