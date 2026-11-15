using BalloonParty.Shared;
using UnityEngine;
using VContainer;

namespace BalloonParty.Game.Assist
{
    /// <summary>
    ///     Eases the threats that come by chance while the player is in need: a threat's chance scaled down by
    ///     <see cref="IAssistSettings.ThreatEaseAtFullNeed" /> times the need, so the last heart sees fewer of them and
    ///     a healthy run sees them all. The need rides along for the picks that favour help.
    /// </summary>
    internal sealed class ThreatEase
    {
        private readonly IPlayerNeed _need;
        private readonly IAssistSettings _settings;

        internal float Need => Mathf.Clamp01(_need.Need);

        [Inject]
        internal ThreatEase(IPlayerNeed need, IAssistSettings settings)
        {
            _need = need;
            _settings = settings;
        }

        internal float Eased(float chance)
        {
            return chance * (1f - _settings.ThreatEaseAtFullNeed * Need);
        }
    }
}
