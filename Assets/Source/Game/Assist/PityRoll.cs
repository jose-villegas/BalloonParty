using BalloonParty.Shared;
using UnityEngine;

namespace BalloonParty.Game.Assist
{
    /// <summary>
    ///     A chance roll that forgives a player in trouble. Each roll missed while in need adds that need to its
    ///     pity, and the chance is the base raised by <see cref="IPitySettings.GrowthPerMiss" /> times the pity times
    ///     the need now, up to <see cref="IPitySettings.MaxChance" />. A hit clears the pity. Out of need the chance
    ///     is the base and no pity builds, so the help comes only when it is needed, and the base can stay low.
    /// </summary>
    internal sealed class PityRoll
    {
        private readonly IPitySettings _settings;

        private float _pity;

        internal float Pity => _pity;

        internal PityRoll(IPitySettings settings)
        {
            _settings = settings;
        }

        internal float ChanceFor(float baseChance, float need)
        {
            if (baseChance <= 0f)
            {
                return 0f;
            }

            var raised = baseChance * (1f + _settings.GrowthPerMiss * _pity * Mathf.Clamp01(need));
            return Mathf.Max(baseChance, Mathf.Min(raised, _settings.MaxChance));
        }

        // A draw can be 1 (Random.value's top), so a certain chance hits without it.
        internal bool Roll(float baseChance, float need, float draw)
        {
            var chance = ChanceFor(baseChance, need);
            if (chance <= 0f)
            {
                return false;
            }

            if (chance >= 1f || draw < chance)
            {
                _pity = 0f;
                return true;
            }

            _pity += Mathf.Clamp01(need);
            return false;
        }

        internal void Clear()
        {
            _pity = 0f;
        }
    }
}
