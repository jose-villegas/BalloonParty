using System;
using UnityEngine;

namespace BalloonParty.Configuration.Level
{
    /// <summary>
    ///     When balloon lines arrive: one line per interval of scaled game time, the interval shrinking
    ///     each level, quickly until it reaches <c>_minInterval</c>, then at a gentler late rate down to a
    ///     floor. The late rate is what keeps deep levels getting harder once the level generator has bought
    ///     every threat. A formula rather than a per-range field, so it reaches every level and survives the
    ///     move to generated levels (PLAN-FastPaced Phase 4).
    /// </summary>
    [Serializable]
    internal class SpawnClockSettings : ISpawnClockSettings
    {
        [Tooltip("Seconds between balloon lines at level 1.")]
        [SerializeField] [Min(0.1f)] private float _firstLevelInterval = 2f;

        [Tooltip("Each level's interval is the previous level's times this: 0.96 = 4% faster per level. " +
                 "1 = the same pace at every level.")]
        [SerializeField] [Range(0.5f, 1f)] private float _intervalDecayPerLevel = 0.96f;

        [Tooltip("Where the early decay ends: from the level the interval reaches this, it shrinks at the late " +
                 "rate instead.")]
        [SerializeField] [Min(0.1f)] private float _minInterval = 1.25f;

        [Tooltip("Past Min Interval, each level's interval is the previous level's times this: 0.98 = 2% " +
                 "faster per level. Keeps pressure growing on deep levels, where every threat is already in " +
                 "the mix. 1 = the interval holds at Min Interval.")]
        [SerializeField] [Range(0.5f, 1f)] private float _lateIntervalDecayPerLevel = 0.98f;

        [Tooltip("The interval never drops below this, however deep the level. If set above Min Interval, the " +
                 "interval just holds at Min Interval.")]
        [SerializeField] [Min(0.1f)] private float _lateMinInterval = 0.55f;

        [Tooltip("Seconds of play after a level starts, or a run restarts, before its first line arrives.")]
        [SerializeField] [Min(0f)] private float _graceSeconds = 4f;

        [Tooltip("Lines grouped as one wave. Wave quotas (each balloon type's count-weights curve), roamer " +
                 "jumps and the item cadence count waves: they were tuned per turn, when a turn brought " +
                 "this many lines. 1 = every line is its own wave.")]
        [SerializeField] [Min(1)] private int _linesPerWave = 3;

        [Tooltip("Share of the current wait a ball fired while another is still flying takes off the next " +
                 "line: 0.35 = it arrives 35% of an interval sooner. One ball at a time costs nothing, so " +
                 "rapid fire trades board space for firepower. 0 = firing never moves the clock.")]
        [SerializeField] [Range(0f, 1f)] private float _overlapShotPull = 0.35f;

        [Tooltip("How much faster the clock runs on an empty board: 8 = eight times the pace. It scales down " +
                 "with the board's fill, back to normal once the board is at the level's starting fill and " +
                 "the target occupancy, so a cleared board refills in moments. 1 = fill never matters.")]
        [SerializeField] [Min(1f)] private float _sparseBoardSpeedup = 8f;

        [Tooltip("Share of the board's open slots (those no grid actor holds) the clock pushes balloons " +
                 "towards: 0.5 = it runs fast until half the board is balloons.")]
        [SerializeField] [Range(0.05f, 1f)] private float _targetOccupancy = 0.5f;

        [Tooltip("How the board's fill is read: 0 = only against the level's starting fill, 1 = only as " +
                 "occupancy against the target, 0.5 = an even blend.")]
        [SerializeField] [Range(0f, 1f)] private float _occupancyWeight = 0.5f;

        [Tooltip("How much balls in the air push the clock: at full flight pressure it runs this many times " +
                 "faster than the board alone would have it, the extra added on top. Pressure grows with " +
                 "every ball's flight time, so long ricochets and rapid fire both bring lines sooner while " +
                 "they clear. 1 = flight never matters.")]
        [SerializeField] [Min(1f)] private float _flightSpeedup = 3f;

        [Tooltip("Summed flight seconds of all balls in the air that reach full flight pressure: 6 = one " +
                 "ball cruising 6 s, or three balls 2 s each.")]
        [SerializeField] [Min(0.1f)] private float _flightPressureSeconds = 6f;

        [Tooltip("Share of the board's open slots that, cleared within Breather Clear Window, earns a breather: " +
                 "the clock drops to Breather Pace and eases back to full pace over Breather Seconds, so a big " +
                 "clear buys a moment of calm before the pressure builds again. 0.2 = a fifth of the board. " +
                 "0 = never.")]
        [SerializeField] [Range(0f, 1f)] private float _breatherClear = 0.2f;

        [Tooltip("Seconds a clear must happen within to earn a breather. The same clear spread over longer " +
                 "earns nothing.")]
        [SerializeField] [Min(0.1f)] private float _breatherClearWindow = 2f;

        [Tooltip("Seconds a breather lasts. Its pace eases from Breather Pace back to the clock's full pace " +
                 "(the sparse board and flight speedups included), so the board refills ever faster towards " +
                 "its end.")]
        [SerializeField] [Min(0.1f)] private float _breatherSeconds = 6f;

        [Tooltip("The clock's pace as a breather starts, as a share of its full pace: 0.2 = a fifth. A shot " +
                 "fired over another ball pulls the next line by the same share.")]
        [SerializeField] [Range(0f, 1f)] private float _breatherPace = 0.2f;

        public float GraceSeconds => _graceSeconds;
        public float OverlapShotPull => _overlapShotPull;
        public float SparseBoardSpeedup => Mathf.Max(1f, _sparseBoardSpeedup);
        public float TargetOccupancy => Mathf.Clamp(_targetOccupancy, 0.05f, 1f);
        public float OccupancyWeight => Mathf.Clamp01(_occupancyWeight);
        public float FlightSpeedup => Mathf.Max(1f, _flightSpeedup);
        public float FlightPressureSeconds => Mathf.Max(0.1f, _flightPressureSeconds);
        public int LinesPerWave => Mathf.Max(1, _linesPerWave);
        public float BreatherClear => Mathf.Clamp01(_breatherClear);
        public float BreatherClearWindow => Mathf.Max(0.1f, _breatherClearWindow);
        public float BreatherSeconds => Mathf.Max(0.1f, _breatherSeconds);
        public float BreatherPace => Mathf.Clamp01(_breatherPace);

        public SpawnClockSettings()
        {
        }

        internal SpawnClockSettings(float firstLevelInterval, float intervalDecayPerLevel, float minInterval,
            float graceSeconds, int linesPerWave = 1, float overlapShotPull = 0f, float sparseBoardSpeedup = 1f,
            float targetOccupancy = 0.5f, float occupancyWeight = 0f, float flightSpeedup = 1f,
            float flightPressureSeconds = 6f, float lateIntervalDecayPerLevel = 1f, float lateMinInterval = 0.1f,
            float breatherClear = 0f, float breatherClearWindow = 2f, float breatherSeconds = 6f,
            float breatherPace = 0.2f)
        {
            _breatherClear = breatherClear;
            _breatherClearWindow = breatherClearWindow;
            _breatherSeconds = breatherSeconds;
            _breatherPace = breatherPace;
            _firstLevelInterval = firstLevelInterval;
            _intervalDecayPerLevel = intervalDecayPerLevel;
            _minInterval = minInterval;
            _graceSeconds = graceSeconds;
            _linesPerWave = linesPerWave;
            _overlapShotPull = overlapShotPull;
            _sparseBoardSpeedup = sparseBoardSpeedup;
            _targetOccupancy = targetOccupancy;
            _occupancyWeight = occupancyWeight;
            _flightSpeedup = flightSpeedup;
            _flightPressureSeconds = flightPressureSeconds;
            _lateIntervalDecayPerLevel = lateIntervalDecayPerLevel;
            _lateMinInterval = lateMinInterval;
        }

        // Levels below 1 (the negative debug presets) run at level 1's pace.
        public float IntervalForLevel(int level)
        {
            var steps = Mathf.Max(0, level - 1);
            var kneeSteps = StepsToMinInterval();
            if (steps <= kneeSteps)
            {
                return Mathf.Max(_minInterval, _firstLevelInterval * Mathf.Pow(_intervalDecayPerLevel, steps));
            }

            var late = _minInterval * Mathf.Pow(_lateIntervalDecayPerLevel, steps - kneeSteps);
            return Mathf.Max(Mathf.Min(_lateMinInterval, _minInterval), late);
        }

        // Fractional, so the late decay picks up exactly where the early one crosses Min Interval and the
        // interval never jumps between two levels.
        private float StepsToMinInterval()
        {
            if (_firstLevelInterval <= _minInterval)
            {
                return 0f;
            }

            if (_intervalDecayPerLevel >= 1f)
            {
                return float.PositiveInfinity;
            }

            return Mathf.Log(_minInterval / _firstLevelInterval) / Mathf.Log(_intervalDecayPerLevel);
        }
    }
}
