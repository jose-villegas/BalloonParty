using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Level;
using BalloonParty.Game.Level;
using BalloonParty.Game.Run;

namespace BalloonParty.Slots.Actor
{
    /// <summary>The live game's <see cref="IMushroomPlacementSource" />: the level being laid out, the run's seed
    /// and the balloon spawner's own settings.</summary>
    internal sealed class MushroomPlacementSource : IMushroomPlacementSource
    {
        // Keeps the layout's roll apart from the others seeded by the same level seed.
        private const int LayoutSeedSalt = 0x4D555348;

        private readonly ILevelPacingConfiguration _pacing;
        private readonly IRunSeed _runSeed;
        private readonly IActiveLevelParameters _level;
        private readonly IBalloonsConfiguration _balloons;

        // The level's own seed, as every per-level roll starts from, mixed with the run's: a retry keeps both,
        // so it replays the layout, and a new run rolls a new one. The level is the one the active parameters
        // were resolved for, which is where the spawner reads the mushrooms' count from: the seed and the count
        // name the same level because they come from the same place.
        public int LayoutSeed => _pacing.SeedForLevel(_level.Level) ^ _runSeed.Value ^ LayoutSeedSalt;

        public int BottomRowsReserved => _balloons.PopSpawnFreeRows;

        internal MushroomPlacementSource(
            ILevelPacingConfiguration pacing, IRunSeed runSeed, IActiveLevelParameters level,
            IBalloonsConfiguration balloons)
        {
            _pacing = pacing;
            _runSeed = runSeed;
            _level = level;
            _balloons = balloons;
        }
    }
}
