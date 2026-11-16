using System;
using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Configuration.Balloons
{
    [Serializable]
    public class RewardBalloonSettings : IRewardBalloonSettings
    {
        [Tooltip("The level's completion (each colour's points, capped at the goal, over all the goals) from which " +
                 "an ordinary level that won its golden balloon brings it.")]
        [SerializeField] [Range(0f, 1f)] private float _goldenAtCompletion = 0.5f;

        [Tooltip("Off a purple level, every this many levels from the Sure Golden Level on rolls once for a single " +
                 "golden balloon, which comes at Golden At Completion. 1 = every level. 0 turns the roll off.")]
        [SerializeField] [Min(0)] private int _ordinaryGoldenEveryLevels = 1;

        [Tooltip("The chance, 0 to 1, that such a level gets its golden balloon, at the sure level. It eases off " +
                 "the further the run goes (Ordinary Chance Halved After Levels).")]
        [SerializeField] [Range(0f, 1f)] private float _ordinaryGoldenChance = 0.2f;

        [Tooltip("Hard pity: after this many levels in a row without a golden balloon placed, the next level off " +
                 "purple from the sure level on brings one with no roll. 0 = none.")]
        [SerializeField] [Min(0)] private int _goldenPityLevels = 4;

        [Tooltip("A level that always brings a single golden balloon, the player's first sight of one, with no " +
                 "roll. 0 for none.")]
        [SerializeField] [Min(0)] private int _sureGoldenLevel = 5;

        [Tooltip("How much of the sure level is done when its golden balloon comes, 0 to 1: early, so the " +
                 "player meets it with the level still ahead of them.")]
        [SerializeField] [Range(0f, 1f)] private float _sureGoldenAtCompletion = 0.1f;

        [Tooltip("Spawn lines between a purple level's reward rolls, golden and heart each counted on its own. Each " +
                 "kind's first roll comes on the first line it can; lines while one of that kind is on the board " +
                 "don't count.")]
        [SerializeField] [Min(1)] private int _purpleRollEveryLines = 6;

        [Tooltip("The level's completion, 0 to 1, from which a purple level rolls for golden balloons.")]
        [SerializeField] [Range(0f, 1f)] private float _purpleGoldenAtCompletion = 0.15f;

        [Tooltip("A purple level's golden balloons: the chance each roll brings the next one, the first entry for " +
                 "the first. The count caps the level's golden balloons. Never more than one on the board.")]
        [SerializeField] [Range(0f, 1f)] private float[] _purpleGoldenChances = { 0.35f, 0.2f, 0.1f };

        [Tooltip("A purple level's heart balloons, rolled from the level's start, at full hearts too (a heart popped " +
                 "there goes past the maximum): the chance each roll brings the next one, the first entry for the " +
                 "first. The count caps the level's heart balloons. The roll while hurt and its cooldown are off on a " +
                 "purple level. Never more than one on the board.")]
        [SerializeField] [Range(0f, 1f)] private float[] _purpleHeartChances = { 0.4f, 0.3f, 0.2f, 0.12f, 0.06f };

        [Tooltip("The disco ball (PLAN-DiscoBalloon) follows the golden balloon's rules with its own numbers, a little " +
                 "more often than the golden and less than the heart. The level's completion from which an " +
                 "ordinary level that won its disco ball brings it.")]
        [SerializeField] [Range(0f, 1f)] private float _discoAtCompletion = 0.35f;

        [Tooltip("Off a purple level, every this many levels from the Sure Disco Level on rolls once for a single " +
                 "disco ball, which comes at Disco At Completion. 1 = every level. 0 turns the roll off.")]
        [SerializeField] [Min(0)] private int _ordinaryDiscoEveryLevels = 1;

        [Tooltip("The chance, 0 to 1, that such a level gets its disco ball, at the sure level. It eases off the " +
                 "further the run goes (Ordinary Chance Halved After Levels).")]
        [SerializeField] [Range(0f, 1f)] private float _ordinaryDiscoChance = 0.25f;

        [Tooltip("Hard pity: after this many levels in a row without a disco ball placed, the next level off purple " +
                 "from the sure level on brings one with no roll. 0 = none.")]
        [SerializeField] [Min(0)] private int _discoPityLevels = 4;

        [Tooltip("A level that always brings a single disco ball, the player's first sight of one, with no roll. " +
                 "After the golden balloon's sure level, so the player already knows only a pierce breaks a plated " +
                 "balloon. 0 for none.")]
        [SerializeField] [Min(0)] private int _sureDiscoLevel = 8;

        [Tooltip("How much of the sure level is done when its disco ball comes, 0 to 1: early, so the rainbow board " +
                 "has the level still ahead of it.")]
        [SerializeField] [Range(0f, 1f)] private float _sureDiscoAtCompletion = 0.15f;

        [Tooltip("The level's completion, 0 to 1, from which a purple level rolls for disco balls.")]
        [SerializeField] [Range(0f, 1f)] private float _purpleDiscoAtCompletion = 0.25f;

        [Tooltip("A purple level's disco balls: the chance each roll brings the next one, the first entry for the " +
                 "first. 1 makes it sure: every purple level gets one. The count caps the level's disco balls. Never " +
                 "more than one on the board.")]
        [SerializeField] [Range(0f, 1f)] private float[] _purpleDiscoChances = { 1f, 0.25f, 0.15f };

        [Tooltip("How the golden balloon's and the disco ball's ordinary chances ease off over a run: each is " +
                 "halved this many levels past its sure level, a third at twice that, and so on, so the longer the " +
                 "run the more of them come from the hard pity. 0 = they never ease.")]
        [SerializeField] [Min(0)] private int _ordinaryChanceHalvedAfterLevels = 20;

        [Tooltip("Chance per spawn line of a heart balloon while one heart is missing, off a purple level. Full " +
                 "hearts never roll one.")]
        [SerializeField] [Range(0f, 1f)] private float _heartChanceMissingOne = 0.04f;

        [Tooltip("Chance per spawn line of a heart balloon while two or more hearts are missing.")]
        [SerializeField] [Range(0f, 1f)] private float _heartChanceMissingTwo = 0.12f;

        [Tooltip("Spawn lines after a heart balloon is placed before another can roll.")]
        [SerializeField] [Min(0)] private int _heartCooldownLines = 12;

        public float GoldenAtCompletion => _goldenAtCompletion;
        public int OrdinaryGoldenEveryLevels => _ordinaryGoldenEveryLevels;
        public float OrdinaryGoldenChance => _ordinaryGoldenChance;
        public int GoldenPityLevels => _goldenPityLevels;
        public int SureGoldenLevel => _sureGoldenLevel;
        public float SureGoldenAtCompletion => _sureGoldenAtCompletion;
        public int PurpleRollEveryLines => Mathf.Max(1, _purpleRollEveryLines);
        public float PurpleGoldenAtCompletion => _purpleGoldenAtCompletion;
        public IReadOnlyList<float> PurpleGoldenChances => _purpleGoldenChances;
        public IReadOnlyList<float> PurpleHeartChances => _purpleHeartChances;
        public float DiscoAtCompletion => _discoAtCompletion;
        public int OrdinaryDiscoEveryLevels => _ordinaryDiscoEveryLevels;
        public float OrdinaryDiscoChance => _ordinaryDiscoChance;
        public int DiscoPityLevels => _discoPityLevels;
        public int OrdinaryChanceHalvedAfterLevels => _ordinaryChanceHalvedAfterLevels;
        public int SureDiscoLevel => _sureDiscoLevel;
        public float SureDiscoAtCompletion => _sureDiscoAtCompletion;
        public float PurpleDiscoAtCompletion => _purpleDiscoAtCompletion;
        public IReadOnlyList<float> PurpleDiscoChances => _purpleDiscoChances;
        public float HeartChanceMissingOne => _heartChanceMissingOne;
        public float HeartChanceMissingTwo => _heartChanceMissingTwo;
        public int HeartCooldownLines => _heartCooldownLines;
    }
}
