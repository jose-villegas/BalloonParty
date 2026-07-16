using System.Collections.Generic;
using BalloonParty.Game.Run;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Shared.Pause
{
    /// <summary>The only legal writer of <c>Time.timeScale</c> (style-audit rule bans writes elsewhere); lowest active claim wins.</summary>
    internal sealed class TimeScaleService : IStartable, IRunResettable
    {
        private readonly Dictionary<TimeScaleSource, float> _claims = new();

        public int ResetOrder => RunResetOrder.Counters;

        // Time.timeScale is a global that survives scene loads: a scene torn down mid-warp (e.g. a
        // level-up ceremony holding the scale near zero) would otherwise start the next scope
        // permanently frozen — nothing re-applies until a new claim or a run reset.
        public void Start()
        {
            Apply();
        }

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
