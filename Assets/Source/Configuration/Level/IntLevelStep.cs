using System;
using UnityEngine;

namespace BalloonParty.Configuration.Level
{
    [Serializable]
    internal struct IntLevelStep : ILevelStep
    {
        [SerializeField] [Min(1)] private int _fromLevel;
        [SerializeField] private int _value;

        public int FromLevel => _fromLevel;
        public int Value => _value;

        internal IntLevelStep(int fromLevel, int value)
        {
            _fromLevel = fromLevel;
            _value = value;
        }
    }
}
