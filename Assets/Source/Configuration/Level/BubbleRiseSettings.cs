using System;
using UnityEngine;

namespace BalloonParty.Configuration.Level
{
    /// <summary>
    ///     The bubbles that come in with the clock's lines on water levels, a growing share of every line the
    ///     longer the level runs (<see cref="BubbleRiseCurve" />).
    /// </summary>
    [Serializable]
    internal class BubbleRiseSettings : IBubbleRiseSettings
    {
        [Tooltip("Seconds of play at a water level's start before any line brings a bubble.")]
        [SerializeField] [Min(0f)] private float _graceSeconds = 6f;

        [Tooltip("Share of a line's places that are bubbles as the grace ends: 0.15 = about one in seven.")]
        [SerializeField] [Range(0f, 1f)] private float _startShare = 0.15f;

        [Tooltip("Share of a line's places that are bubbles once the ramp is done. Above 0.5, a line brings " +
                 "more bubbles than balloons.")]
        [SerializeField] [Range(0f, 1f)] private float _maxShare = 0.65f;

        [Tooltip("Seconds after the grace the share takes to climb from Start Share to Max Share, then holds.")]
        [SerializeField] [Min(0f)] private float _rampSeconds = 60f;

        [Tooltip("Most bubbles on the board at once; past it a line brings the usual balloons. Merges free " +
                 "room. 0 = no cap.")]
        [SerializeField] [Min(0)] private int _maxBubbles = 12;

        [Tooltip("Bubbles in each cluster a line brings at level 1. Low, so merges can grow them into bigger " +
                 "clusters (up to the balloons config's Bubble Merge Max).")]
        [SerializeField] [Min(1)] private int _startBubbles = 1;

        [Tooltip("Every this many levels, a line's clusters start with one more bubble, so deep water levels " +
                 "bring clusters that take several hits, in place of the Tough and Tougher balloons water " +
                 "leaves out. 0 = never.")]
        [SerializeField] [Min(0)] private int _extraBubbleEveryLevels = 15;

        public float GraceSeconds => Mathf.Max(0f, _graceSeconds);
        public float StartShare => Mathf.Clamp01(_startShare);
        public float MaxShare => Mathf.Clamp01(_maxShare);
        public float RampSeconds => Mathf.Max(0f, _rampSeconds);
        public int MaxBubbles => Mathf.Max(0, _maxBubbles);

        public BubbleRiseSettings()
        {
        }

        internal BubbleRiseSettings(float graceSeconds, float startShare, float maxShare, float rampSeconds,
            int maxBubbles = 12, int startBubbles = 1, int extraBubbleEveryLevels = 0)
        {
            _graceSeconds = graceSeconds;
            _startShare = startShare;
            _maxShare = maxShare;
            _rampSeconds = rampSeconds;
            _maxBubbles = maxBubbles;
            _startBubbles = startBubbles;
            _extraBubbleEveryLevels = extraBubbleEveryLevels;
        }

        // Counted from level 1, like the spawn clock's decay; levels below 1 (debug presets) get level 1's.
        public int StartBubblesForLevel(int level)
        {
            var start = Mathf.Max(1, _startBubbles);
            if (_extraBubbleEveryLevels <= 0)
            {
                return start;
            }

            return start + Mathf.Max(0, level - 1) / _extraBubbleEveryLevels;
        }
    }
}
