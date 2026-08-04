using System;
using BalloonParty.Game.Telemetry;
using BalloonParty.Shared.Extensions;
using TMPro;
using UniRx;
using UnityEngine;

namespace BalloonParty.UI.Telemetry
{
    /// <summary>
    ///     Renders one telemetry value into this object's <see cref="TMP_Text" />, whose authored text
    ///     is the format template (<c>"Reds popped: {0}"</c>). Pick the value in the inspector; the
    ///     dropdown is generated from <see cref="MetricCatalog" />, so a new metric becomes selectable
    ///     without touching UI code.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    internal sealed class MetricLabel : MonoBehaviour
    {
        [SerializeField] private MetricBinding _binding;

        [Tooltip("Hide the whole object when the value resolves to zero — for optional stat lines " +
            "(\"Toughs cleared: 0\") that are noise rather than information.")]
        [SerializeField] private bool _hideWhenZero;

        private FormattedLabel _label;
        private bool _templateCaptured;
        private IDisposable _subscription;

        private void Awake()
        {
            CaptureTemplate();
            Show(MetricValueResolver.Placeholder, false);
        }

        private void OnDestroy()
        {
            LifecycleHelper.DisposeAndClear(ref _subscription);
        }

        internal void Bind(ILevelMetricsView view, MetricValueResolver resolver)
        {
            LifecycleHelper.DisposeAndClear(ref _subscription);

            switch (_binding.Source)
            {
                case MetricBindingSource.CeremonyLevel:
                    _subscription = view.CeremonyLevel.Subscribe(s => Render(resolver, s));
                    break;
                case MetricBindingSource.LastFlushedLevel:
                    _subscription = view.LastFlushedLevel.Subscribe(s => Render(resolver, s));
                    break;
                case MetricBindingSource.Run:
                    _subscription = view.Run.Subscribe(s => Render(resolver, s));
                    break;
            }
        }

        private void Render(MetricValueResolver resolver, ISealedMetrics snapshot)
        {
            var text = resolver.Resolve(snapshot, _binding, out var isZero);
            Show(text, _hideWhenZero && isZero);
        }

        // Captured before anything overwrites the text — FormattedLabel takes the authored string as
        // its template, and a render that beat this would leave the label showing "{0}" forever. Not
        // only in Awake: a label under an inactive parent has not run Awake when the binder starts, and
        // FormattedLabel is a struct, so its default Set() silently no-ops rather than throwing.
        private void CaptureTemplate()
        {
            if (!_templateCaptured)
            {
                _templateCaptured = true;
                _label = new FormattedLabel(GetComponent<TMP_Text>());
            }
        }

        private void Show(string text, bool hidden)
        {
            // Only a label that opted into hide-when-zero owns its object's active state. Otherwise a
            // label authored inactive — which MetricLabelRegistration deliberately still gathers —
            // would be force-shown the moment the binder runs.
            if (_hideWhenZero && gameObject.activeSelf == hidden)
            {
                gameObject.SetActive(!hidden);
            }

            if (!hidden)
            {
                CaptureTemplate();
                _label.Set(text);
            }
        }
    }
}
