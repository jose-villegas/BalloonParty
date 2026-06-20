using System;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration;

namespace BalloonParty.Balloon.Model
{
    /// <summary>
    ///     Builds the <see cref="IWriteableBalloonModel"/> matching a prefab entry's
    ///     <see cref="BalloonType"/>. Kept in one place so the type switch isn't duplicated between
    ///     the spawner and the rejected-balloon effect.
    /// </summary>
    internal static class BalloonModelFactory
    {
        internal static IWriteableBalloonModel Create(BalloonPrefabEntry entry, IGamePalette palette)
        {
            var config = BalloonModelConfig.From(entry);

            return entry.BalloonType switch
            {
                BalloonType.Simple => new BalloonModel(config),
                BalloonType.BubbleCluster => new BubbleClusterModel(config, palette),
                BalloonType.Tough => new ToughBalloonModel(config, palette),
                BalloonType.Unbreakable => new UnbreakableBalloonModel(config),
                _ => throw new ArgumentOutOfRangeException(nameof(entry), entry.BalloonType, null)
            };
        }
    }
}
