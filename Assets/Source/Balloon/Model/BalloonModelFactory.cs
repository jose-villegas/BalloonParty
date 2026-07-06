using System;
using System.Collections.Generic;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Palette;

namespace BalloonParty.Balloon.Model
{
    /// <summary>
    ///     Builds the <see cref="IWriteableBalloonModel"/> matching a prefab entry's
    ///     <see cref="BalloonType"/>. Kept in one place so the type switch isn't duplicated between
    ///     the spawner and the rejected-balloon effect.
    /// </summary>
    internal static class BalloonModelFactory
    {
        // allowedColors only matters to Tough/BubbleCluster — they distribute score to a random
        // palette color rather than holding one, so they'd otherwise bypass the color gate.
        internal static IWriteableBalloonModel Create(
            BalloonPrefabEntry entry, IGamePalette palette, IReadOnlyList<string> allowedColors = null)
        {
            var config = BalloonModelConfig.From(entry);

            return entry.BalloonType switch
            {
                BalloonType.Simple => new BalloonModel(config),
                BalloonType.SimpleSilver => new BalloonModel(config),
                BalloonType.SimpleGold => new BalloonModel(config),
                BalloonType.BubbleCluster => new BubbleClusterModel(config, palette, allowedColors),
                BalloonType.Tough => new ToughBalloonModel(config, palette, allowedColors),
                BalloonType.Unbreakable => new UnbreakableBalloonModel(config),
                _ => throw new ArgumentOutOfRangeException(nameof(entry), entry.BalloonType, null)
            };
        }
    }
}
