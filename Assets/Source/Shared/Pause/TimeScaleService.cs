using System.Collections.Generic;
using BalloonParty.Game.Run;
using UnityEngine;

namespace BalloonParty.Shared.Pause
{
    /// <summary>
    ///     The only legal writer of <c>Time.timeScale</c> (a style-audit rule bans writes elsewhere).
    ///     Callers claim a value under their <see cref="TimeScaleSource" /> and release it when done;
    ///     the lowest active claim wins (the popup's freeze beats a cinematic's slow-mo) and no claims
    ///     means normal speed. Releasing — or the run resetting — restores automatically, so no
    ///     caller can forget to set it back to 1.
    /// </summary>
    internal sealed class TimeScaleService : IRunResettable
    {
        private readonly Dictionary<TimeScaleSource, float> _claims = new();

        public int ResetOrder => RunResetOrder.Counters;

        public void ResetRun(int generation)
        {
            _claims.Clear();
            Apply();
        }

        internal void Claim(TimeScaleSource source, float value)
        {
            _claims[source] = Mathf.Max(0f, value);
            Apply();
        }

        internal void Release(TimeScaleSource source)
        {
            if (_claims.Remove(source))
            {
                Apply();
            }
        }

        private void Apply()
        {
            var scale = 1f;
            foreach (var value in _claims.Values)
            {
                scale = Mathf.Min(scale, value);
            }

            Time.timeScale = scale;
        }
    }
}
