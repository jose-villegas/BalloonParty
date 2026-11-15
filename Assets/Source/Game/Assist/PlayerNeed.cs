using BalloonParty.Game.Danger;
using BalloonParty.Game.Health;
using BalloonParty.Shared;
using UnityEngine;

namespace BalloonParty.Game.Assist
{
    /// <summary>
    ///     Need from the hearts and the board: the larger of the hearts' (0 at full, 1 on the last) and the danger
    ///     signal's (<see cref="IDangerLevel" />, weighted by <see cref="IAssistSettings.DangerNeedWeight" />). Read
    ///     when a roll asks, so it is never stale.
    /// </summary>
    internal sealed class PlayerNeed : IPlayerNeed
    {
        private readonly IPlayerHealth _health;
        private readonly IDangerLevel _danger;
        private readonly IAssistSettings _settings;

        public float Need => Mathf.Max(HeartsNeed(_health.Current.Value, _health.Max),
            Mathf.Clamp01(_danger.Level.Value) * Mathf.Clamp01(_settings.DangerNeedWeight));

        internal PlayerNeed(IPlayerHealth health, IDangerLevel danger, IAssistSettings settings)
        {
            _health = health;
            _danger = danger;
            _settings = settings;
        }

        // Linear from full to the last heart. Hearts past full are no less safe than full; a run of one heart has
        // none to lose before the end, so it reads no need from them.
        internal static float HeartsNeed(int current, int max)
        {
            if (max <= 1)
            {
                return 0f;
            }

            return Mathf.Clamp01((max - current) / (float)(max - 1));
        }
    }
}
