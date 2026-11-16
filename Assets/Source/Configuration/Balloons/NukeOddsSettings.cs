using System;
using UnityEngine;

namespace BalloonParty.Configuration.Balloons
{
    [Serializable]
    public class NukeOddsSettings : INukeOddsSettings
    {
        [Tooltip("The first level that can roll a nuke. Early levels rarely put a player in trouble.")]
        [SerializeField] [Min(0)] private int _fromLevel = 4;

        [Tooltip("How many nukes a level can bring at most. 0 turns them off.")]
        [SerializeField] [Min(0)] private int _perLevel = 1;

        [Tooltip("Clock lines after a nuke is placed before the next can roll. Lines come every 2 s on the first " +
                 "level and every 0.55 s late in a run, so 150 is about two to five minutes.")]
        [SerializeField] [Min(0)] private int _cooldownLines = 150;

        [Tooltip("The chance on a clock line out of trouble. 0.0005 is about one in 2,000 lines, an hour of play.")]
        [SerializeField] [Range(0f, 1f)] private float _baseChance = 0.0005f;

        [Tooltip("The chance on a clock line in full trouble (every signal's weighted sum at 1 or more). 0.04 is " +
                 "about one in 25 lines.")]
        [SerializeField] [Range(0f, 1f)] private float _maxChance = 0.04f;

        [Tooltip("The board's occupancy (the share of its open slots that balloons fill) at which the fill signal " +
                 "starts to rise from 0.")]
        [SerializeField] [Range(0f, 1f)] private float _fillFrom = 0.7f;

        [Tooltip("The board's occupancy at which the fill signal reaches 1: almost full.")]
        [SerializeField] [Range(0f, 1f)] private float _fillFull = 0.95f;

        [Tooltip("How much an almost-full board adds to the trouble.")]
        [SerializeField] [Range(0f, 1f)] private float _fillWeight = 0.5f;

        [Tooltip("How much the spawn clock's pressure (0 to 1, the clock running up to Pressure Speedup times " +
                 "faster) adds to the trouble. Pressure builds only while the player is comfortable, so it stays a " +
                 "nudge.")]
        [SerializeField] [Range(0f, 1f)] private float _pressureWeight = 0.25f;

        [Tooltip("How much low hearts add to the trouble: the hearts' need is 0 at full hearts and 1 on the last.")]
        [SerializeField] [Range(0f, 1f)] private float _heartsWeight = 0.5f;

        [Tooltip("How much hearts lost in quick succession add to the trouble.")]
        [SerializeField] [Range(0f, 1f)] private float _lossWeight = 0.5f;

        [Tooltip("Recent hearts lost that read as a full signal. At 2, one heart lost reads half, and two on " +
                 "lines close together read nearly full.")]
        [SerializeField] [Min(0.1f)] private float _lossesForFull = 2f;

        [Tooltip("The share of the recent losses each clock line keeps, so a loss fades over the next lines.")]
        [SerializeField] [Range(0f, 1f)] private float _lossKeptPerLine = 0.75f;

        public int FromLevel => _fromLevel;
        public int PerLevel => _perLevel;
        public int CooldownLines => _cooldownLines;
        public float BaseChance => _baseChance;
        public float MaxChance => _maxChance;
        public float FillFrom => _fillFrom;
        public float FillFull => _fillFull;
        public float FillWeight => _fillWeight;
        public float PressureWeight => _pressureWeight;
        public float HeartsWeight => _heartsWeight;
        public float LossWeight => _lossWeight;
        public float LossesForFull => Mathf.Max(0.1f, _lossesForFull);
        public float LossKeptPerLine => _lossKeptPerLine;
    }
}
