using BalloonParty.Balloon.Model;

namespace BalloonParty.Balloon.Type
{
    public interface IBalloonVariant
    {
        /// <summary>Binds the variant to its model and applies any spawn-time setup (e.g. colour pick).</summary>
        /// <param name="model">The balloon model this variant drives.</param>
        /// <param name="levelAllowedColorsMask">The active level's color gate; only <see cref="ColorableBalloonVariant" /> uses it.</param>
        void Initialize(IWriteableBalloonModel model, int levelAllowedColorsMask);
    }
}
