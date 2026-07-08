using System;
using UnityEngine;

namespace BalloonParty.Nudge
{
    [Serializable]
    public class NudgeOverride
    {
        [SerializeField] private NudgeType _appliesTo;
        [SerializeField] private float _distance;
        [SerializeField] private float _duration;
        [SerializeField] private float _falloff;

        public NudgeType AppliesTo => _appliesTo;
        public float Distance => _distance;
        public float Duration => _duration;
        public float Falloff => _falloff;

        public NudgeOverride() { }

        public NudgeOverride(NudgeType appliesTo, float distance, float duration = 0f, float falloff = 0f)
        {
            _appliesTo = appliesTo;
            _distance = distance;
            _duration = duration;
            _falloff = falloff;
        }
    }
}
