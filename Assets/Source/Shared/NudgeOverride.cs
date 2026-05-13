using System;
using UnityEngine;

namespace BalloonParty.Shared
{
    [Serializable]
    public class NudgeOverride
    {
        [SerializeField] private NudgeType _appliesTo;
        [SerializeField] private float _distance;
        [SerializeField] private float _duration;

        public NudgeType AppliesTo => _appliesTo;
        public float Distance => _distance;
        public float Duration => _duration;
    }
}

