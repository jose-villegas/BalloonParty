using System;
using UnityEngine;

namespace BalloonParty.Configuration.Level
{
    /// <summary>A level's budget: a base, plus a step per level after the first, never past a cap.</summary>
    [Serializable]
    internal struct BudgetFormula
    {
        [Tooltip("Budget at level 1.")]
        [SerializeField] [Min(0f)] private float _base;

        [Tooltip("Budget added per level after the first.")]
        [SerializeField] [Min(0f)] private float _perLevel;

        [Tooltip("The budget never grows past this.")]
        [SerializeField] [Min(0f)] private float _max;

        internal BudgetFormula(float @base, float perLevel, float max)
        {
            _base = @base;
            _perLevel = perLevel;
            _max = max;
        }

        internal float ForLevel(int level)
        {
            return Mathf.Min(_base + _perLevel * Mathf.Max(0, level - 1), _max);
        }
    }
}
