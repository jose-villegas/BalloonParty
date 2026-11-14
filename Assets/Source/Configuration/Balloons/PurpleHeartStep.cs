using System;
using UnityEngine;

namespace BalloonParty.Configuration.Balloons
{
    /// <summary>
    ///     One heart balloon of a purple level's schedule: rolled once, with <see cref="Chance" />, on the line that
    ///     finds the level <see cref="AtCompletion" /> done.
    /// </summary>
    [Serializable]
    public struct PurpleHeartStep
    {
        [Tooltip("The level's completion, 0 to 1, whose line rolls for this heart balloon.")]
        [SerializeField] [Range(0f, 1f)] private float _atCompletion;

        [Tooltip("The chance this heart balloon comes, rolled once. 1 is sure.")]
        [SerializeField] [Range(0f, 1f)] private float _chance;

        public float AtCompletion => _atCompletion;
        public float Chance => _chance;

        public PurpleHeartStep(float atCompletion, float chance)
        {
            _atCompletion = atCompletion;
            _chance = chance;
        }
    }
}
