using System;
using BalloonParty.Shared.Extensions;
using BalloonParty.UI.Binding;
using TMPro;
using UniRx;
using UnityEngine;

namespace BalloonParty.UI
{
    /// <summary>
    ///     Shows an int reactive value as a thousands-separated label, with a <c>"--"</c> placeholder
    ///     until bound and on unbind.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    internal abstract class ReactiveCounterLabel : MonoBehaviour, IReactiveBindable<int>
    {
        private TMP_Text _label;
        private IDisposable _subscription;

        private void Awake()
        {
            _label = GetComponent<TMP_Text>();
            _label.text = "--";
        }

        private void OnDestroy()
        {
            _subscription?.Dispose();
        }

        public void Bind(IReadOnlyReactiveProperty<int> source)
        {
            _subscription?.Dispose();
            _subscription = source.Subscribe(value => _label.text = value.ToString("N0"));
        }

        public void Unbind()
        {
            LifecycleHelper.DisposeAndClear(ref _subscription);
            _label.text = "--";
        }
    }
}
