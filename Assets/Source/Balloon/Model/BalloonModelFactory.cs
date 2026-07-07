using System;
using System.Collections.Generic;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Palette;

namespace BalloonParty.Balloon.Model
{
    /// <summary>Builds the <see cref="IWriteableBalloonModel"/> matching a prefab entry's <see cref="BalloonType"/>.</summary>
    internal static class BalloonModelFactory
    {
        // allowedColors matters to Tough/BubbleCluster (scatter score to a random palette color) and to
        // every BalloonModel (a plain balloon can be converted to rainbow mode later by Paint, and needs
        // its own colour pool at that point — it isn't re-threaded after spawn).
        internal static IWriteableBalloonModel Create(
            BalloonPrefabEntry entry, IGamePalette palette, IReadOnlyList<string> allowedColors = null)
        {
            var config = BalloonModelConfig.From(entry);

            return entry.BalloonType switch
            {
                BalloonType.Simple => new BalloonModel(config, palette, allowedColors),
                BalloonType.SimpleSilver => new BalloonModel(config, palette, allowedColors),
                BalloonType.SimpleGold => new BalloonModel(config, palette, allowedColors),
                BalloonType.BubbleCluster => new BubbleClusterModel(config, palette, allowedColors),
                BalloonType.Tough => new ToughBalloonModel(config, palette, allowedColors),
                BalloonType.Unbreakable => new UnbreakableBalloonModel(config),
                _ => throw new ArgumentOutOfRangeException(nameof(entry), entry.BalloonType, null)
            };
        }
    }
}
