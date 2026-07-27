using System.Collections.Generic;
using BalloonParty.Game.Run;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Shared.Pause
{
    /// <summary>The only legal writer of <c>Time.timeScale</c> (style-audit rule bans writes elsewhere); lowest active claim wins.</summary>
    internal sealed class TimeScaleService : IStartable, IRunResettable, ITimeScaleClaims
    {
        private readonly Dictionary<TimeScaleSource, float> _claims = new();

        private TimeScaleSource? _exclusiveOwner;

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
            // Dropped with the claims: a reset mid-ceremony would otherwise leave a dead ceremony owning
            // the clock for the rest of the run.
            _exclusiveOwner = null;
            _claims.Clear();
            Apply();
        }

        public void Claim(TimeScaleSource source, float value)
        {
            _claims[source] = Mathf.Max(0f, value);
            Apply();
        }

        public void Release(TimeScaleSource source)
        {
            if (_exclusiveOwner == source)
            {
                _exclusiveOwner = null;
            }

            if (_claims.Remove(source))
            {
                Apply();
            }
        }

        // Last exclusive claimant wins rather than queueing: two at once would mean two ceremonies,
        // which the phase machine already forbids.
        public void ClaimExclusive(TimeScaleSource source, float value)
        {
            _exclusiveOwner = source;
            _claims[source] = Mathf.Max(0f, value);
            Apply();
        }

        public void ReleaseExclusive(TimeScaleSource source)
        {
            if (_exclusiveOwner != source)
            {
                return;
            }

            _exclusiveOwner = null;
            _claims.Remove(source);
            Apply();
        }

        private void Apply()
        {
            var scale = 1f;
            if (_exclusiveOwner.HasValue && _claims.TryGetValue(_exclusiveOwner.Value, out var owned))
            {
                // Owner-only: other sources keep their recorded claims (LastShield re-claims every frame
                // from a curve) and resume applying the moment exclusivity ends.
                scale = Mathf.Min(1f, owned);
            }
            else
            {
                foreach (var value in _claims.Values)
                {
                    scale = Mathf.Min(scale, value);
                }
            }

            Time.timeScale = scale;
        }
    }
}
