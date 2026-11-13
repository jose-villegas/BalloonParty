using System;
using UnityEngine;

namespace BalloonParty.Configuration.Balloons
{
    [Serializable]
    public class RewardBalloonSettings : IRewardBalloonSettings
    {
        [Tooltip("The level's completion (each colour's points, capped at the goal, over all the goals) from which " +
                 "a purple level's next line brings its golden balloon. Once a level.")]
        [SerializeField] [Range(0f, 1f)] private float _goldenAtCompletion = 0.75f;

        [Tooltip("Chance per spawn line of a heart balloon while one heart is missing. Full hearts never roll one.")]
        [SerializeField] [Range(0f, 1f)] private float _heartChanceMissingOne = 0.04f;

        [Tooltip("Chance per spawn line of a heart balloon while two or more hearts are missing.")]
        [SerializeField] [Range(0f, 1f)] private float _heartChanceMissingTwo = 0.12f;

        [Tooltip("Added to the heart chance on a purple level, while a heart is missing.")]
        [SerializeField] [Range(0f, 1f)] private float _heartChanceOnPurple = 0.05f;

        [Tooltip("Spawn lines after a heart balloon is placed before another can roll.")]
        [SerializeField] [Min(0)] private int _heartCooldownLines = 12;

        public float GoldenAtCompletion => _goldenAtCompletion;
        public float HeartChanceMissingOne => _heartChanceMissingOne;
        public float HeartChanceMissingTwo => _heartChanceMissingTwo;
        public float HeartChanceOnPurple => _heartChanceOnPurple;
        public int HeartCooldownLines => _heartCooldownLines;
    }
}
