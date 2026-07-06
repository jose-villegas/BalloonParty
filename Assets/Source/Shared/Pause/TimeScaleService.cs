using System.Collections.Generic;
using BalloonParty.Game.Run;
using UnityEngine;

namespace BalloonParty.Shared.Pause
{
    /// <summary>The only legal writer of <c>Time.timeScale</c> (style-audit rule bans writes elsewhere); lowest active claim wins.</summary>
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
