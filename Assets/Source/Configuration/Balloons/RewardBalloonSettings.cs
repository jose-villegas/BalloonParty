using System;
using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Configuration.Balloons
{
    [Serializable]
    public class RewardBalloonSettings : IRewardBalloonSettings
    {
        [Tooltip("The level's completion (each colour's points, capped at the goal, over all the goals) from which " +
                 "a purple level's next line brings its first golden balloon.")]
        [SerializeField] [Range(0f, 1f)] private float _goldenAtCompletion = 0.5f;

        [Tooltip("The chance, 0 to 1, of each further golden balloon on a purple level, rolled once when the one " +
                 "before it pops (a board clear or a level change is no pop): the first entry for the second balloon, " +
                 "the next for the third. Never more than one on the board.")]
        [SerializeField] [Range(0f, 1f)] private float[] _goldenRepeatChances = { 0.25f, 0.1f };

        [Tooltip("Spawn lines after a golden balloon's pop before the further one it won comes.")]
        [SerializeField] [Min(0)] private int _goldenRepeatDelayLines = 3;

        [Tooltip("Off a purple level, every this many levels (the 5th, 10th, ...) rolls once for a single golden " +
                 "balloon, which comes at Golden At Completion like a purple level's first. Its pop rolls for no " +
                 "other. 0 turns it off.")]
        [SerializeField] [Min(0)] private int _ordinaryGoldenEveryLevels = 5;

        [Tooltip("The chance, 0 to 1, that such a level gets its golden balloon.")]
        [SerializeField] [Range(0f, 1f)] private float _ordinaryGoldenChance = 0.4f;

        [Tooltip("A level that always brings a single golden balloon, the player's first sight of one, with no " +
                 "roll (a purple level brings its own anyway). 0 for none.")]
        [SerializeField] [Min(0)] private int _sureGoldenLevel = 5;

        [Tooltip("How much of the sure level is done when its golden balloon comes, 0 to 1: early, so the " +
                 "player meets it with the level still ahead of them.")]
        [SerializeField] [Range(0f, 1f)] private float _sureGoldenAtCompletion = 0.1f;

        [Tooltip("A purple level's heart balloons (32 at most): each rolled once, with its chance, on " +
                 "the line that reaches its completion, at full hearts too (a heart popped there goes past the " +
                 "maximum). The roll while hurt and its cooldown are off on a purple level. Never more than one on " +
                 "the board: a won one waits its turn.")]
        [SerializeField] private PurpleHeartStep[] _purpleHearts =
        {
            new(0.3f, 1f),
            new(0.6f, 0.5f),
            new(0.9f, 0.25f),
        };

        [Tooltip("Chance per spawn line of a heart balloon while one heart is missing, off a purple level. Full " +
                 "hearts never roll one.")]
        [SerializeField] [Range(0f, 1f)] private float _heartChanceMissingOne = 0.04f;

        [Tooltip("Chance per spawn line of a heart balloon while two or more hearts are missing.")]
        [SerializeField] [Range(0f, 1f)] private float _heartChanceMissingTwo = 0.12f;

        [Tooltip("Spawn lines after a heart balloon is placed before another can roll.")]
        [SerializeField] [Min(0)] private int _heartCooldownLines = 12;

        public float GoldenAtCompletion => _goldenAtCompletion;
        public IReadOnlyList<float> GoldenRepeatChances => _goldenRepeatChances;
        public int GoldenRepeatDelayLines => _goldenRepeatDelayLines;
        public int OrdinaryGoldenEveryLevels => _ordinaryGoldenEveryLevels;
        public float OrdinaryGoldenChance => _ordinaryGoldenChance;
        public int SureGoldenLevel => _sureGoldenLevel;
        public float SureGoldenAtCompletion => _sureGoldenAtCompletion;
        public IReadOnlyList<PurpleHeartStep> PurpleHearts => _purpleHearts;
        public float HeartChanceMissingOne => _heartChanceMissingOne;
        public float HeartChanceMissingTwo => _heartChanceMissingTwo;
        public int HeartCooldownLines => _heartCooldownLines;
    }
}
