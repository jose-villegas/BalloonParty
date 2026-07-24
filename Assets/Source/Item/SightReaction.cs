using System;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

namespace BalloonParty.Item
{
    /// <summary>
    ///     Base for a behaviour that reacts to the loaded shot's aim-prediction line being sighted on this
    ///     item, reading a shared <see cref="PredictionSightProbe"/> (the single per-item trace). Drop a
    ///     concrete subclass on a pooled item visual next to a probe and it self-wires — no host or DI
    ///     changes. Handles the pooled-reuse lifecycle: subscriptions live in a <see cref="CompositeDisposable"/>
    ///     cleared on disable (never <c>AddTo(this)</c>, which never fires for pooled objects), and the look
    ///     resets to neutral on enable/disable so a reused icon never inherits the previous host's state.
    ///     Edge/one-shot reactions (sfx, punches) override <see cref="OnBind"/> and subscribe to the probe's
    ///     hysteretic <see cref="PredictionSightProbe.IsSighted"/>; continuous ones use
    ///     <see cref="SightRampReaction"/>.
    /// </summary>
    internal abstract class SightReaction : MonoBehaviour
    {
        [Tooltip("Sight source. Defaults to a PredictionSightProbe on this object or a parent if left unset.")]
        [SerializeField] private PredictionSightProbe _probe;

        private readonly CompositeDisposable _disposables = new CompositeDisposable();

        protected PredictionSightProbe Probe => _probe;

        protected virtual void Awake()
        {
            if (_probe == null)
            {
                _probe = GetComponentInParent<PredictionSightProbe>();
            }
        }

        protected virtual void OnEnable()
        {
            _disposables.Clear();
            ResetReaction();

            if (_probe != null)
            {
                OnBind(_probe, _disposables);
            }
        }

        protected virtual void OnDisable()
        {
            _disposables.Clear();
            ResetReaction();
        }

        // Subscribe to the probe's streams here (e.g. IsSighted enter/exit edges), adding to `disposables`.
        // Default: nothing — ramp reactions poll in LateUpdate instead of subscribing.
        protected virtual void OnBind(PredictionSightProbe probe, ICollection<IDisposable> disposables)
        {
        }

        // Restore the neutral / off-aim look so a pooled reuse starts clean.
        protected virtual void ResetReaction()
        {
        }
    }
}
