using System;
using UnityEngine;

namespace BalloonParty.Configuration.Level
{
    /// <summary>
    ///     A rare level that plays one more colour than its step gives (the fourth, purple), with no threats and
    ///     lines arriving much faster: the pressure is a colour more to juggle and a busier board, not armour to
    ///     break. Rolled per level from the run's seed, so runs differ and a retry replays it. Never two in a row,
    ///     so the level after one is back to the usual colours.
    /// </summary>
    [Serializable]
    internal class ExtraColorLevelSettings
    {
        // Keeps this roll's stream apart from the other per-level rolls on the same run seed.
        private const int RollSalt = 0x3C6EF372;

        [Tooltip("First level that can roll an extra colour.")]
        [SerializeField] [Min(1)] private int _fromLevel = 13;

        [Tooltip("Chance each level from From Level rolls one more colour, with no threats and faster lines. A " +
                 "level right after one never plays one, so fewer levels get it than rolled it: about 1 in 6 at 0.2. " +
                 "0 = never.")]
        [SerializeField] [Range(0f, 1f)] private float _chance = 0.2f;

        [Tooltip("The time between lines on such a level, as a share of the usual: 0.5 = lines twice as often.")]
        [SerializeField] [Range(0.1f, 1f)] private float _spawnIntervalScale = 0.5f;

        internal static ExtraColorLevelSettings Off => new(1, 0f, 1f);

        public float SpawnIntervalScale => _spawnIntervalScale;

        public ExtraColorLevelSettings()
        {
        }

        internal ExtraColorLevelSettings(int fromLevel, float chance, float spawnIntervalScale)
        {
            _fromLevel = fromLevel;
            _chance = chance;
            _spawnIntervalScale = spawnIntervalScale;
        }

        /// <summary>
        ///     Whether <paramref name="level" /> plays an extra colour in the run seeded <paramref name="runSeed" />:
        ///     it rolled under the chance, and the level before it isn't one.
        /// </summary>
        internal bool IsExtraColorLevel(int level, int runSeed)
        {
            // A run of rolls alternates from its first: each one is out only when the one before it is in.
            var first = level;
            while (Rolls(first - 1, runSeed))
            {
                first--;
            }

            return Rolls(level, runSeed) && (level - first) % 2 == 0;
        }

        private bool Rolls(int level, int runSeed)
        {
            if (level < _fromLevel || _chance <= 0f)
            {
                return false;
            }

            return LevelSeedHash.Hash(level, runSeed ^ RollSalt) < _chance * (double)int.MaxValue;
        }
    }
}
