using System;
using BalloonParty.UI.Binding;
using TMPro;
using UniRx;
using UnityEngine;

namespace BalloonParty.UI
{
    /// <summary>
    ///     Shows an int reactive value as a thousands-separated label, with a <c>"--"</c> placeholder
    ///     until bound and on unbind. Concrete subclasses exist only to give each HUD prefab a distinct
    ///     component type so its scope can gather them independently — all behaviour lives here.
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
            _subscription?.Dispose();
            _subscription = null;
            _label.text = "--";
        }
    }
}
