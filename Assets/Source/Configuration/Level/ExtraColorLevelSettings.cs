using System;
using UnityEngine;

namespace BalloonParty.Configuration.Level
{
    /// <summary>
    ///     A rare level that plays one more colour than its step gives (the fourth, purple), with no threats and
    ///     lines arriving faster: the pressure is a colour more to juggle and a busier board, not armour to
    ///     break. More items come with it, to help sort the extra colour out. The first can be pinned to a level
    ///     (<see cref="FirstLevel" />), so every run meets one early; the rest are rolled per level from the run's
    ///     seed, so runs differ and a retry replays it. At least <see cref="MinLevelsBetween" /> ordinary levels come
    ///     between two, so it never feels spammy, and each level after that which misses it raises the next one's
    ///     chance (<see cref="ChanceGrowthPerLevel" />), so a run never goes too long without one.
    /// </summary>
    [Serializable]
    internal class ExtraColorLevelSettings
    {
        // Keeps this roll's stream apart from the other per-level rolls on the same run seed.
        private const int RollSalt = 0x3C6EF372;

        [Tooltip("The level every run plays its first extra colour on; the rolls start after it, and From Level " +
                 "no longer applies. 0 = no fixed first, it is rolled from From Level like the rest.")]
        [SerializeField] [Min(0)] private int _firstLevel = 7;

        [Tooltip("First level that can roll an extra colour, when First Level is 0.")]
        [SerializeField] [Min(1)] private int _fromLevel = 8;

        [Tooltip("Chance the first level that can roll one (From Level, or the first after the gap) plays one more " +
                 "colour, with no threats and faster lines. 0 = never.")]
        [SerializeField] [Range(0f, 1f)] private float _chance = 0.15f;

        [Tooltip("Soft pity: how much each level that could have rolled one and didn't raises the next one's chance. " +
                 "At 0.15 + 0.1 a level, one comes on average every 5 levels, never more than 12 apart.")]
        [SerializeField] [Range(0f, 1f)] private float _chanceGrowthPerLevel = 0.1f;

        [Tooltip("Ordinary levels that always come between two extra-colour levels. 1 = never two in a row.")]
        [SerializeField] [Min(0)] private int _minLevelsBetween = 2;

        [Tooltip("The time between lines on such a level, as a share of the usual: 0.5 = lines twice as often.")]
        [SerializeField] [Range(0.1f, 1f)] private float _spawnIntervalScale = 0.7f;

        [Tooltip("Extra items on such a level: the same roll as the Lucky Find card (each item rolls this times its " +
                 "ExtraItemShare per wave), added to the player's own. 0.1 is one Lucky Find stack.")]
        [SerializeField] [Range(0f, 1f)] private float _extraItemChance = 0.3f;

        internal static ExtraColorLevelSettings Off => new(1, 0f, 1f);

        public float SpawnIntervalScale => _spawnIntervalScale;
        public float ExtraItemChance => _extraItemChance;
        public float ChanceGrowthPerLevel => _chanceGrowthPerLevel;
        public int MinLevelsBetween => Mathf.Max(0, _minLevelsBetween);
        public int FirstLevel => Mathf.Max(0, _firstLevel);

        public ExtraColorLevelSettings()
        {
        }

        // The defaults for the gap and the growth are the rule before the soft pity: never two in a row, a flat chance.
        internal ExtraColorLevelSettings(int fromLevel, float chance, float spawnIntervalScale,
            float extraItemChance = 0f, int minLevelsBetween = 1, float chanceGrowthPerLevel = 0f, int firstLevel = 0)
        {
            _firstLevel = firstLevel;
            _fromLevel = fromLevel;
            _chance = chance;
            _spawnIntervalScale = spawnIntervalScale;
            _extraItemChance = extraItemChance;
            _minLevelsBetween = minLevelsBetween;
            _chanceGrowthPerLevel = chanceGrowthPerLevel;
        }

        /// <summary>
        ///     Whether <paramref name="level" /> plays an extra colour in the run seeded <paramref name="runSeed" />.
        ///     The run's levels are walked from the first that can play one, since each one's chance depends on how
        ///     long ago the last one came; every draw is the level's own hash, so the answer is the same each time
        ///     asked.
        /// </summary>
        internal bool IsExtraColorLevel(int level, int runSeed)
        {
            var start = FirstLevel > 0 ? FirstLevel : _fromLevel;
            if (level < start || _chance <= 0f)
            {
                return false;
            }

            var between = MinLevelsBetween;
            // Primed so From Level rolls at the base chance.
            var sinceLast = between;
            for (var at = start; at < level; at++)
            {
                sinceLast = Plays(at, runSeed, sinceLast, between) ? 0 : sinceLast + 1;
            }

            return Plays(level, runSeed, sinceLast, between);
        }

        /// <summary>The chance a level plays one after <paramref name="missed" /> levels that could have and didn't.</summary>
        internal float ChanceAfter(int missed)
        {
            return Mathf.Min(1f, _chance + Mathf.Max(0f, _chanceGrowthPerLevel) * Mathf.Max(0, missed));
        }

        private bool Plays(int level, int runSeed, int sinceLast, int between)
        {
            if (level == FirstLevel)
            {
                return true;
            }

            if (sinceLast < between)
            {
                return false;
            }

            var chance = ChanceAfter(sinceLast - between);
            return chance >= 1f || LevelSeedHash.Hash(level, runSeed ^ RollSalt) < chance * (double)int.MaxValue;
        }
    }
}
